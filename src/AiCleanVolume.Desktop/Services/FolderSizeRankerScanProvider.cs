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

            ProcessStartInfo startInfo = new ProcessStartInfo();
            startInfo.FileName = executablePath;
            startInfo.Arguments = BuildArguments(request);
            startInfo.UseShellExecute = false;
            startInfo.RedirectStandardOutput = true;
            startInfo.RedirectStandardError = true;
            startInfo.StandardOutputEncoding = new UTF8Encoding(false, true);
            startInfo.StandardErrorEncoding = new UTF8Encoding(false, true);
            startInfo.CreateNoWindow = true;

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
                DirectoryInfo root = new DirectoryInfo(request.Location);
                return ScanDirectory(root, request, true);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("folder-size-ranker-cli 执行失败：" + cliError + Environment.NewLine + "平台 API 降级扫描也失败：" + ex.Message, ex);
            }
        }

        private static StorageItem ScanDirectory(DirectoryInfo directory, ScanRequest request, bool isRoot)
        {
            StorageItem item = new StorageItem();
            item.Path = directory.FullName;
            item.Name = isRoot ? directory.FullName : StorageFormatting.GetDisplayName(directory.FullName, true);
            item.IsDirectory = true;

            List<StorageItem> directFiles = new List<StorageItem>();
            long totalBytes = 0;
            FileInfo[] files = SafeGetFiles(directory);
            for (int i = 0; i < files.Length; i++)
            {
                FileInfo file = files[i];
                if (IsReparsePoint(file)) continue;
                long fileBytes = SafeGetLength(file);
                totalBytes += fileBytes;
                if (fileBytes < request.MinSizeBytes) continue;

                directFiles.Add(new StorageItem
                {
                    Path = file.FullName,
                    Name = StorageFormatting.GetDisplayName(file.FullName, false),
                    Bytes = fileBytes,
                    IsDirectory = false,
                    DirectFileCount = 0,
                    TotalFileCount = 1,
                    TotalDirectoryCount = 0
                });
            }

            List<StorageItem> childDirectories = new List<StorageItem>();
            DirectoryInfo[] directories = SafeGetDirectories(directory);
            for (int i = 0; i < directories.Length; i++)
            {
                DirectoryInfo childDirectory = directories[i];
                if (IsReparsePoint(childDirectory)) continue;
                StorageItem child = ScanDirectory(childDirectory, request, false);
                totalBytes += child.Bytes;
                if (child.Bytes >= request.MinSizeBytes) childDirectories.Add(child);
            }

            directFiles.Sort(CompareByBytesDescending);
            childDirectories.Sort(CompareByBytesDescending);
            int limit = request.PerLevelLimit < 0 ? int.MaxValue : request.PerLevelLimit;
            AddLimited(item.Children, directFiles, limit);
            AddLimited(item.Children, childDirectories, limit);

            item.Bytes = totalBytes;
            item.DirectFileCount = Math.Min(directFiles.Count, limit);
            int totalFiles = item.DirectFileCount;
            int totalDirs = 0;
            for (int i = 0; i < item.Children.Count; i++)
            {
                StorageItem child = item.Children[i];
                if (!child.IsDirectory) continue;
                totalFiles += child.TotalFileCount;
                totalDirs += 1 + child.TotalDirectoryCount;
            }
            item.TotalFileCount = totalFiles;
            item.TotalDirectoryCount = totalDirs;
            return item;
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
