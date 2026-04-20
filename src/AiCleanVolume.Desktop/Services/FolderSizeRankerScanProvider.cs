using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using AiCleanVolume.Core.Models;
using AiCleanVolume.Core.Services;
using Newtonsoft.Json;

namespace AiCleanVolume.Desktop.Services
{
    public sealed class FolderSizeRankerScanProvider : IScanProvider
    {
        private readonly string executablePath;

        public FolderSizeRankerScanProvider()
        {
            executablePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "tools", "folder-size-ranker-cli.exe");
        }

        public StorageItem Scan(ScanRequest request)
        {
            if (request == null) throw new ArgumentNullException("request");
            if (string.IsNullOrWhiteSpace(request.Location)) throw new InvalidOperationException("扫描位置不能为空。");
            if (!File.Exists(executablePath)) throw new FileNotFoundException("未找到 folder-size-ranker-cli.exe。", executablePath);
            request.Location = NormalizeLocation(request.Location);

            if (request.LoadDepth >= 0) return ScanPartial(request);
            return ScanFull(request);
        }

        private StorageItem ScanFull(ScanRequest request)
        {
            ProcessStartInfo startInfo = CreateStartInfo(request);
            using (Process process = Process.Start(startInfo))
            {
                string output = process.StandardOutput.ReadToEnd();
                string error = process.StandardError.ReadToEnd();
                process.WaitForExit();
                if (process.ExitCode != 0)
                {
                    StorageItem fallback = TryScanWithPlatformApi(request, error);
                    if (fallback != null) return fallback;
                    throw new InvalidOperationException("folder-size-ranker-cli 执行失败：" + error);
                }

                FolderNodeDto root = JsonConvert.DeserializeObject<FolderNodeDto>(output);
                if (root == null) throw new InvalidOperationException("扫描结果为空或 JSON 无法解析。");
                return ConvertFolder(root, true);
            }
        }

        private StorageItem ScanPartial(ScanRequest request)
        {
            ProcessStartInfo startInfo = CreateStartInfo(request);
            using (Process process = Process.Start(startInfo))
            {
                StorageItem root = null;
                Exception parseError = null;

                try
                {
                    using (JsonTextReader reader = new JsonTextReader(process.StandardOutput))
                    {
                        if (!reader.Read() || reader.TokenType != JsonToken.StartObject)
                        {
                            throw new InvalidOperationException("扫描结果为空或 JSON 无法解析。");
                        }

                        root = ParseFolder(reader, true, request.LoadDepth);
                    }
                }
                catch (Exception ex)
                {
                    parseError = ex;
                }

                string error = process.StandardError.ReadToEnd();
                process.WaitForExit();
                if (process.ExitCode != 0)
                {
                    StorageItem fallback = TryScanWithPlatformApi(request, error);
                    if (fallback != null) return fallback;
                    throw new InvalidOperationException("folder-size-ranker-cli 执行失败：" + error);
                }

                if (parseError != null) throw new InvalidOperationException("扫描结果解析失败：" + parseError.Message, parseError);
                if (root == null) throw new InvalidOperationException("扫描结果为空或 JSON 无法解析。");
                return root;
            }
        }

        private ProcessStartInfo CreateStartInfo(ScanRequest request)
        {
            ProcessStartInfo startInfo = new ProcessStartInfo();
            startInfo.FileName = executablePath;
            startInfo.Arguments = BuildArguments(request);
            startInfo.UseShellExecute = false;
            startInfo.RedirectStandardOutput = true;
            startInfo.RedirectStandardError = true;
            startInfo.StandardOutputEncoding = new UTF8Encoding(false, true);
            startInfo.StandardErrorEncoding = new UTF8Encoding(false, true);
            startInfo.CreateNoWindow = true;
            return startInfo;
        }

        private static string BuildArguments(ScanRequest request)
        {
            string sort = request.SortMode == ScanSortMode.Logical ? "logical" : "allocated";
            StringBuilder builder = new StringBuilder();
            builder.Append("--location ");
            builder.Append(QuoteArgument(request.Location));
            builder.Append(" --sort ");
            builder.Append(sort);
            builder.Append(" --all");
            if (request.MinSizeBytes >= 0)
            {
                builder.Append(" --min-size ");
                builder.Append(request.MinSizeBytes);
            }
            if (request.PerLevelLimit >= 0)
            {
                builder.Append(" --limit ");
                builder.Append(request.PerLevelLimit);
            }
            return builder.ToString();
        }

        private static string QuoteArgument(string value)
        {
            if (value == null) return "\"\"";
            StringBuilder result = new StringBuilder();
            result.Append('"');

            int backslashes = 0;
            for (int i = 0; i < value.Length; i++)
            {
                char current = value[i];
                if (current == '\\')
                {
                    backslashes++;
                    continue;
                }

                if (current == '"')
                {
                    result.Append('\\', backslashes * 2 + 1);
                    result.Append('"');
                    backslashes = 0;
                    continue;
                }

                if (backslashes > 0)
                {
                    result.Append('\\', backslashes);
                    backslashes = 0;
                }
                result.Append(current);
            }

            result.Append('\\', backslashes * 2);
            result.Append('"');
            return result.ToString();
        }

        private static string NormalizeLocation(string location)
        {
            string value = Environment.ExpandEnvironmentVariables((location ?? string.Empty).Trim().Trim('"'));
            if (value.Length == 2 && value[1] == ':') return char.ToUpperInvariant(value[0]) + @":\";
            try
            {
                return Path.GetFullPath(value);
            }
            catch
            {
                return value;
            }
        }

        private static StorageItem TryScanWithPlatformApi(ScanRequest request, string cliError)
        {
            if (!Directory.Exists(request.Location)) return null;
            try
            {
                int depth = request.LoadDepth < 0 ? int.MaxValue : request.LoadDepth;
                DirectoryInfo root = new DirectoryInfo(request.Location);
                return ScanDirectory(root, request, true, depth);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("folder-size-ranker-cli 执行失败：" + cliError + Environment.NewLine + "平台 API 降级扫描也失败：" + ex.Message, ex);
            }
        }

        private static StorageItem ScanDirectory(DirectoryInfo directory, ScanRequest request, bool isRoot, int remainingDepth)
        {
            StorageItem item = new StorageItem();
            item.Path = directory.FullName;
            item.Name = isRoot ? directory.FullName : StorageFormatting.GetDisplayName(directory.FullName, true);
            item.IsDirectory = true;

            List<StorageItem> directFiles = remainingDepth > 0 ? new List<StorageItem>() : null;
            List<StorageItem> childDirectories = new List<StorageItem>();
            long totalBytes = 0;
            int directFileCount = 0;

            FileInfo[] files = SafeGetFiles(directory);
            for (int i = 0; i < files.Length; i++)
            {
                FileInfo file = files[i];
                if (IsReparsePoint(file)) continue;
                long fileBytes = SafeGetLength(file);
                totalBytes += fileBytes;
                if (fileBytes < request.MinSizeBytes) continue;
                directFileCount++;

                if (directFiles == null) continue;

                StorageItem child = new StorageItem();
                child.Path = file.FullName;
                child.Name = StorageFormatting.GetDisplayName(file.FullName, false);
                child.Bytes = fileBytes;
                child.IsDirectory = false;
                child.HasChildren = false;
                child.ChildrenLoaded = true;
                child.DirectFileCount = 0;
                child.TotalFileCount = 1;
                child.TotalDirectoryCount = 0;
                directFiles.Add(child);
            }

            DirectoryInfo[] directories = SafeGetDirectories(directory);
            for (int i = 0; i < directories.Length; i++)
            {
                DirectoryInfo childDirectory = directories[i];
                if (IsReparsePoint(childDirectory)) continue;
                StorageItem child = ScanDirectory(childDirectory, request, false, remainingDepth > 0 ? remainingDepth - 1 : 0);
                totalBytes += child.Bytes;
                if (child.Bytes >= request.MinSizeBytes) childDirectories.Add(child);
            }

            if (directFiles != null) directFiles.Sort(CompareByBytesDescending);
            childDirectories.Sort(CompareByBytesDescending);

            int limit = request.PerLevelLimit < 0 ? int.MaxValue : request.PerLevelLimit;
            int limitedFileCount = Math.Min(directFileCount, limit);
            int limitedDirectoryCount = Math.Min(childDirectories.Count, limit);

            if (directFiles != null) AddLimited(item.Children, directFiles, limit);
            if (remainingDepth > 0) AddLimited(item.Children, childDirectories, limit);

            item.Bytes = totalBytes;
            item.DirectFileCount = limitedFileCount;

            int totalFiles = limitedFileCount;
            int totalDirs = 0;
            for (int i = 0; i < limitedDirectoryCount; i++)
            {
                StorageItem child = childDirectories[i];
                totalFiles += child.TotalFileCount;
                totalDirs += 1 + child.TotalDirectoryCount;
            }

            item.TotalFileCount = totalFiles;
            item.TotalDirectoryCount = totalDirs;
            item.HasChildren = item.DirectFileCount > 0 || item.TotalDirectoryCount > 0;
            item.ChildrenLoaded = remainingDepth > 0;
            return item;
        }

        private static StorageItem ParseFolder(JsonTextReader reader, bool isRoot, int remainingDepth)
        {
            StorageItem item = new StorageItem();
            item.IsDirectory = true;

            List<StorageItem> directFiles = remainingDepth > 0 ? new List<StorageItem>() : null;
            List<StorageItem> childDirectories = remainingDepth > 0 ? new List<StorageItem>() : null;
            int directFileCount = 0;
            int totalFiles = 0;
            int totalDirs = 0;

            while (reader.Read())
            {
                if (reader.TokenType == JsonToken.PropertyName)
                {
                    string propertyName = reader.Value == null ? string.Empty : reader.Value.ToString();
                    if (!reader.Read()) throw new InvalidOperationException("扫描结果不完整。");

                    switch (propertyName)
                    {
                        case "path":
                            item.Path = reader.Value == null ? string.Empty : reader.Value.ToString();
                            item.Name = isRoot ? item.Path : StorageFormatting.GetDisplayName(item.Path, true);
                            break;
                        case "bytes":
                            item.Bytes = ReadInt64(reader.Value);
                            break;
                        case "files":
                            ParseFiles(reader, directFiles, ref directFileCount);
                            break;
                        case "children":
                            ParseChildren(reader, remainingDepth, childDirectories, ref totalFiles, ref totalDirs);
                            break;
                        default:
                            reader.Skip();
                            break;
                    }
                }
                else if (reader.TokenType == JsonToken.EndObject)
                {
                    break;
                }
            }

            item.DirectFileCount = directFileCount;
            item.TotalFileCount = directFileCount + totalFiles;
            item.TotalDirectoryCount = totalDirs;
            item.HasChildren = item.DirectFileCount > 0 || item.TotalDirectoryCount > 0;
            item.ChildrenLoaded = remainingDepth > 0;
            if (string.IsNullOrEmpty(item.Name)) item.Name = isRoot ? item.Path : StorageFormatting.GetDisplayName(item.Path, true);

            if (directFiles != null) AddAll(item.Children, directFiles);
            if (childDirectories != null) AddAll(item.Children, childDirectories);
            return item;
        }

        private static void ParseFiles(JsonTextReader reader, IList<StorageItem> target, ref int directFileCount)
        {
            if (reader.TokenType == JsonToken.Null) return;
            if (reader.TokenType != JsonToken.StartArray) throw new InvalidOperationException("扫描结果文件数组格式错误。");

            while (reader.Read())
            {
                if (reader.TokenType == JsonToken.EndArray) break;
                if (reader.TokenType != JsonToken.StartObject)
                {
                    reader.Skip();
                    continue;
                }

                StorageItem file = ParseFile(reader);
                directFileCount++;
                if (target != null) target.Add(file);
            }
        }

        private static StorageItem ParseFile(JsonTextReader reader)
        {
            StorageItem item = new StorageItem();
            item.IsDirectory = false;
            item.HasChildren = false;
            item.ChildrenLoaded = true;
            item.DirectFileCount = 0;
            item.TotalFileCount = 1;
            item.TotalDirectoryCount = 0;

            while (reader.Read())
            {
                if (reader.TokenType == JsonToken.PropertyName)
                {
                    string propertyName = reader.Value == null ? string.Empty : reader.Value.ToString();
                    if (!reader.Read()) throw new InvalidOperationException("扫描结果不完整。");

                    switch (propertyName)
                    {
                        case "path":
                            item.Path = reader.Value == null ? string.Empty : reader.Value.ToString();
                            item.Name = StorageFormatting.GetDisplayName(item.Path, false);
                            break;
                        case "bytes":
                            item.Bytes = ReadInt64(reader.Value);
                            break;
                        default:
                            reader.Skip();
                            break;
                    }
                }
                else if (reader.TokenType == JsonToken.EndObject)
                {
                    break;
                }
            }

            if (string.IsNullOrEmpty(item.Name)) item.Name = StorageFormatting.GetDisplayName(item.Path, false);
            return item;
        }

        private static void ParseChildren(JsonTextReader reader, int remainingDepth, IList<StorageItem> target, ref int totalFiles, ref int totalDirs)
        {
            if (reader.TokenType == JsonToken.Null) return;
            if (reader.TokenType != JsonToken.StartArray) throw new InvalidOperationException("扫描结果目录数组格式错误。");

            while (reader.Read())
            {
                if (reader.TokenType == JsonToken.EndArray) break;
                if (reader.TokenType != JsonToken.StartObject)
                {
                    reader.Skip();
                    continue;
                }

                StorageItem child = ParseFolder(reader, false, remainingDepth > 0 ? remainingDepth - 1 : 0);
                totalFiles += child.TotalFileCount;
                totalDirs += 1 + child.TotalDirectoryCount;
                if (target != null) target.Add(child);
            }
        }

        private static long ReadInt64(object value)
        {
            if (value == null) return 0;
            return Convert.ToInt64(value);
        }

        private static FileInfo[] SafeGetFiles(DirectoryInfo directory)
        {
            try { return directory.GetFiles(); }
            catch { return new FileInfo[0]; }
        }

        private static DirectoryInfo[] SafeGetDirectories(DirectoryInfo directory)
        {
            try { return directory.GetDirectories(); }
            catch { return new DirectoryInfo[0]; }
        }

        private static long SafeGetLength(FileInfo file)
        {
            try { return file.Length; }
            catch { return 0; }
        }

        private static bool IsReparsePoint(FileSystemInfo info)
        {
            try { return (info.Attributes & FileAttributes.ReparsePoint) == FileAttributes.ReparsePoint; }
            catch { return true; }
        }

        private static int CompareByBytesDescending(StorageItem left, StorageItem right)
        {
            return right.Bytes.CompareTo(left.Bytes);
        }

        private static void AddLimited(IList<StorageItem> target, IList<StorageItem> source, int limit)
        {
            int count = Math.Min(source.Count, limit);
            for (int i = 0; i < count; i++) target.Add(source[i]);
        }

        private static void AddAll(IList<StorageItem> target, IList<StorageItem> source)
        {
            for (int i = 0; i < source.Count; i++) target.Add(source[i]);
        }

        private static StorageItem ConvertFolder(FolderNodeDto dto, bool isRoot)
        {
            StorageItem item = new StorageItem();
            item.Path = dto.path;
            item.Name = isRoot ? dto.path : StorageFormatting.GetDisplayName(dto.path, true);
            item.Bytes = dto.bytes;
            item.IsDirectory = true;

            if (dto.files != null)
            {
                item.DirectFileCount = dto.files.Count;
                for (int i = 0; i < dto.files.Count; i++)
                {
                    FileNodeDto file = dto.files[i];
                    StorageItem child = new StorageItem();
                    child.Path = file.path;
                    child.Name = StorageFormatting.GetDisplayName(file.path, false);
                    child.Bytes = file.bytes;
                    child.IsDirectory = false;
                    child.HasChildren = false;
                    child.ChildrenLoaded = true;
                    child.DirectFileCount = 0;
                    child.TotalFileCount = 1;
                    child.TotalDirectoryCount = 0;
                    item.Children.Add(child);
                }
            }

            if (dto.children != null)
            {
                for (int i = 0; i < dto.children.Count; i++)
                {
                    item.Children.Add(ConvertFolder(dto.children[i], false));
                }
            }

            int totalFiles = item.DirectFileCount;
            int totalDirs = 0;
            for (int i = 0; i < item.Children.Count; i++)
            {
                StorageItem child = item.Children[i];
                totalFiles += child.IsDirectory ? child.TotalFileCount : 0;
                if (child.IsDirectory) totalDirs += 1 + child.TotalDirectoryCount;
            }

            item.TotalFileCount = totalFiles;
            item.TotalDirectoryCount = totalDirs;
            item.HasChildren = item.Children.Count > 0;
            item.ChildrenLoaded = true;
            return item;
        }

        private sealed class FolderNodeDto
        {
            public string path { get; set; }
            public long bytes { get; set; }
            public List<FileNodeDto> files { get; set; }
            public List<FolderNodeDto> children { get; set; }
        }

        private sealed class FileNodeDto
        {
            public string path { get; set; }
            public long bytes { get; set; }
        }
    }
}
