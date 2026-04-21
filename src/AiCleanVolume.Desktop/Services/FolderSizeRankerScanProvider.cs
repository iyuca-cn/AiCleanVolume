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
        private static readonly int[] EmptyChildIds = new int[0];
        private static readonly FileNodeState[] EmptyFiles = new FileNodeState[0];

        private readonly string executablePath;
        private readonly object syncRoot = new object();

        private ScanSession currentTreeSession;

        public FolderSizeRankerScanProvider()
        {
            executablePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "folder-size-ranker-cli.exe");
        }

        public StorageItem Scan(ScanRequest request)
        {
            if (request == null) throw new ArgumentNullException("request");
            if (string.IsNullOrWhiteSpace(request.Location)) throw new InvalidOperationException("扫描位置不能为空。");
            if (!File.Exists(executablePath)) throw new FileNotFoundException("未找到 folder-size-ranker-cli.exe。", executablePath);

            request.Location = NormalizeLocation(request.Location);
            return request.LoadDepth >= 0 ? ScanPartial(request) : ScanFull(request);
        }

        public void ClearCache()
        {
            lock (syncRoot)
            {
                ClearCurrentTreeSessionNoLock();
            }
        }

        internal static StorageItem CloneTree(StorageItem source)
        {
            if (source == null) return null;

            StorageItem clone = new StorageItem();
            clone.Path = source.Path;
            clone.Name = source.Name;
            clone.Bytes = source.Bytes;
            clone.IsDirectory = source.IsDirectory;
            clone.HasChildren = source.HasChildren;
            clone.ChildrenLoaded = source.ChildrenLoaded;
            clone.DirectFileCount = source.DirectFileCount;
            clone.TotalFileCount = source.TotalFileCount;
            clone.TotalDirectoryCount = source.TotalDirectoryCount;
            clone.SessionIdentity = source.SessionIdentity;
            clone.SessionNodeId = source.SessionNodeId;
            for (int i = 0; i < source.Children.Count; i++)
            {
                clone.Children.Add(CloneTree(source.Children[i]));
            }

            return clone;
        }

        private StorageItem ScanFull(ScanRequest request)
        {
            ProcessStartInfo startInfo = CreateStartInfo(request);
            using (Process process = Process.Start(startInfo))
            {
                ScanSession session = null;
                Exception parseError = null;

                try
                {
                    using (JsonTextReader reader = new JsonTextReader(process.StandardOutput))
                    {
                        session = BuildCompactSession(reader, request.Location, BuildTreeTemplateKey(request));
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
                if (session == null) throw new InvalidOperationException("扫描结果为空或 JSON 无法解析。");
                return MaterializeDirectory(session, session.RootNodeId, int.MaxValue, true);
            }
        }

        private StorageItem ScanPartial(ScanRequest request)
        {
            try
            {
                ScanSession session = EnsureTreeSession(request);
                int nodeId = ResolveNodeId(session, request);
                if (nodeId < 0)
                {
                    throw new InvalidOperationException("目录树会话未包含路径：" + request.Location);
                }

                return MaterializeDirectory(session, nodeId, request.LoadDepth, IsSamePath(session.RootPath, request.Location));
            }
            catch (CliExecutionException ex)
            {
                StorageItem fallback = TryScanWithPlatformApi(request, ex.Message);
                if (fallback != null) return fallback;
                throw;
            }
        }

        private ScanSession EnsureTreeSession(ScanRequest request)
        {
            string templateKey = BuildTreeTemplateKey(request);

            lock (syncRoot)
            {
                if (IsCompatibleTreeSession(currentTreeSession, templateKey, request))
                {
                    return currentTreeSession;
                }

                ScanSession session = null;
                try
                {
                    session = BuildTreeSession(request, templateKey);
                    ClearCurrentTreeSessionNoLock();
                    currentTreeSession = session;
                    return currentTreeSession;
                }
                catch
                {
                    if (session != null) session.Dispose();
                    throw;
                }
            }
        }

        private ScanSession BuildTreeSession(ScanRequest request, string templateKey)
        {
            ProcessStartInfo startInfo = CreateStartInfo(request);
            using (Process process = Process.Start(startInfo))
            {
                ScanSession session = null;
                Exception parseError = null;

                try
                {
                    using (JsonTextReader reader = new JsonTextReader(process.StandardOutput))
                    {
                        session = BuildCompactSession(reader, request.Location, templateKey);
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
                    throw new CliExecutionException(error);
                }

                if (parseError != null) throw new InvalidOperationException("扫描结果解析失败：" + parseError.Message, parseError);
                if (session == null) throw new InvalidOperationException("扫描结果为空或 JSON 无法解析。");
                return session;
            }
        }

        private static ScanSession BuildCompactSession(JsonTextReader reader, string requestedLocation, string templateKey)
        {
            if (!reader.Read() || reader.TokenType != JsonToken.StartObject)
            {
                throw new InvalidOperationException("扫描结果为空或 JSON 无法解析。");
            }

            ScanSession session = new ScanSession();
            session.RootPath = NormalizeLocation(requestedLocation);
            session.TemplateKey = templateKey;
            session.SessionIdentity = Guid.NewGuid().ToString("N");
            session.Directories = new List<DirectoryNodeState>();
            session.RootNodeId = ParseDirectoryNode(reader, session, -1, true);
            if (string.IsNullOrWhiteSpace(session.RootPath)) throw new InvalidOperationException("扫描结果根路径为空。");
            return session;
        }

        private static int ParseDirectoryNode(JsonTextReader reader, ScanSession session, int parentNodeId, bool isRoot)
        {
            DirectoryNodeState node = new DirectoryNodeState();
            node.NodeId = session.Directories.Count;
            node.ParentNodeId = parentNodeId;
            node.Name = string.Empty;
            node.DirectFiles = EmptyFiles;
            node.DirectChildNodeIds = EmptyChildIds;
            session.Directories.Add(node);

            List<int> directChildNodeIds = null;
            FileNodeState[] directFiles = EmptyFiles;

            while (reader.Read())
            {
                if (reader.TokenType == JsonToken.PropertyName)
                {
                    string propertyName = reader.Value == null ? string.Empty : reader.Value.ToString();
                    if (!reader.Read()) throw new InvalidOperationException("扫描结果不完整。");

                    switch (propertyName)
                    {
                        case "root_path":
                            if (isRoot) session.RootPath = NormalizeLocation(ReadStringValue(reader.Value));
                            else reader.Skip();
                            break;
                        case "path":
                            ApplyPathProperty(session, node, ReadStringValue(reader.Value), isRoot);
                            break;
                        case "name":
                            node.Name = ReadStringValue(reader.Value);
                            break;
                        case "bytes":
                            node.Bytes = ReadInt64(reader.Value);
                            break;
                        case "files":
                            directFiles = ParseCompactFiles(reader);
                            break;
                        case "children":
                            directChildNodeIds = ParseCompactChildren(reader, session, node.NodeId);
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

            node.DirectFiles = directFiles ?? EmptyFiles;
            node.DirectChildNodeIds = directChildNodeIds == null ? EmptyChildIds : directChildNodeIds.ToArray();
            node.DirectFileCount = node.DirectFiles.Length;

            int totalFiles = node.DirectFileCount;
            int totalDirs = 0;
            for (int i = 0; i < node.DirectChildNodeIds.Length; i++)
            {
                DirectoryNodeState child = session.Directories[node.DirectChildNodeIds[i]];
                totalFiles += child.TotalFileCount;
                totalDirs += 1 + child.TotalDirectoryCount;
            }

            node.TotalFileCount = totalFiles;
            node.TotalDirectoryCount = totalDirs;
            return node.NodeId;
        }

        private static FileNodeState[] ParseCompactFiles(JsonTextReader reader)
        {
            if (reader.TokenType == JsonToken.Null) return EmptyFiles;
            if (reader.TokenType != JsonToken.StartArray) throw new InvalidOperationException("扫描结果文件数组格式错误。");

            List<FileNodeState> files = new List<FileNodeState>();
            while (reader.Read())
            {
                if (reader.TokenType == JsonToken.EndArray) break;
                if (reader.TokenType != JsonToken.StartObject)
                {
                    reader.Skip();
                    continue;
                }

                files.Add(ParseFileState(reader));
            }

            return files.Count == 0 ? EmptyFiles : files.ToArray();
        }

        private static List<int> ParseCompactChildren(JsonTextReader reader, ScanSession session, int parentNodeId)
        {
            if (reader.TokenType == JsonToken.Null) return null;
            if (reader.TokenType != JsonToken.StartArray) throw new InvalidOperationException("扫描结果目录数组格式错误。");

            List<int> childNodeIds = new List<int>();
            while (reader.Read())
            {
                if (reader.TokenType == JsonToken.EndArray) break;
                if (reader.TokenType != JsonToken.StartObject)
                {
                    reader.Skip();
                    continue;
                }

                childNodeIds.Add(ParseDirectoryNode(reader, session, parentNodeId, false));
            }

            return childNodeIds;
        }

        private static void ApplyPathProperty(ScanSession session, DirectoryNodeState node, string path, bool isRoot)
        {
            if (isRoot)
            {
                session.RootPath = NormalizeLocation(path);
                return;
            }

            node.Name = StorageFormatting.GetDisplayName(path, true);
        }

        private static int ResolveNodeId(ScanSession session, ScanRequest request)
        {
            if (session == null || request == null) return -1;

            if (!string.IsNullOrWhiteSpace(request.SessionIdentity) &&
                string.Equals(session.SessionIdentity, request.SessionIdentity, StringComparison.Ordinal) &&
                request.SessionNodeId >= 0 &&
                session.Directories != null &&
                request.SessionNodeId < session.Directories.Count)
            {
                return request.SessionNodeId;
            }

            if (IsSamePath(session.RootPath, request.Location)) return session.RootNodeId;
            return -1;
        }

        private static StorageItem MaterializeDirectory(ScanSession session, int nodeId, int remainingDepth, bool isRoot)
        {
            return MaterializeDirectory(session, nodeId, remainingDepth, isRoot, BuildDirectoryPath(session, nodeId));
        }

        private static StorageItem MaterializeDirectory(ScanSession session, int nodeId, int remainingDepth, bool isRoot, string directoryPath)
        {
            DirectoryNodeState node = session.Directories[nodeId];
            StorageItem item = CreateStorageDirectoryItem(session, node, directoryPath, remainingDepth > 0, isRoot);
            if (remainingDepth <= 0) return item;

            for (int i = 0; i < node.DirectFiles.Length; i++)
            {
                item.Children.Add(CreateStorageFileItem(node.DirectFiles[i], directoryPath));
            }

            int nextDepth = remainingDepth == int.MaxValue ? int.MaxValue : remainingDepth - 1;
            for (int i = 0; i < node.DirectChildNodeIds.Length; i++)
            {
                DirectoryNodeState child = session.Directories[node.DirectChildNodeIds[i]];
                string childPath = CombinePath(directoryPath, child.Name);
                if (remainingDepth == 1)
                {
                    item.Children.Add(CreateStorageDirectoryItem(session, child, childPath, false, false));
                    continue;
                }

                item.Children.Add(MaterializeDirectory(session, child.NodeId, nextDepth, false, childPath));
            }

            return item;
        }

        private static StorageItem CreateStorageDirectoryItem(ScanSession session, DirectoryNodeState node, string path, bool childrenLoaded, bool isRoot)
        {
            StorageItem item = new StorageItem();
            item.Path = path;
            item.Name = isRoot ? path : (string.IsNullOrEmpty(node.Name) ? StorageFormatting.GetDisplayName(path, true) : node.Name);
            item.Bytes = node.Bytes;
            item.IsDirectory = true;
            item.HasChildren = node.DirectFileCount > 0 || node.TotalDirectoryCount > 0;
            item.ChildrenLoaded = childrenLoaded;
            item.DirectFileCount = node.DirectFileCount;
            item.TotalFileCount = node.TotalFileCount;
            item.TotalDirectoryCount = node.TotalDirectoryCount;
            item.SessionIdentity = session == null ? null : session.SessionIdentity;
            item.SessionNodeId = node.NodeId;
            return item;
        }

        private static StorageItem CreateStorageFileItem(FileNodeState state, string parentPath)
        {
            StorageItem item = new StorageItem();
            item.Name = state.Name;
            item.Path = CombinePath(parentPath, state.Name);
            item.Bytes = state.Bytes;
            item.IsDirectory = false;
            item.HasChildren = false;
            item.ChildrenLoaded = true;
            item.DirectFileCount = 0;
            item.TotalFileCount = 1;
            item.TotalDirectoryCount = 0;
            return item;
        }

        private static string BuildDirectoryPath(ScanSession session, int nodeId)
        {
            if (session == null || nodeId == session.RootNodeId) return session == null ? string.Empty : session.RootPath;

            Stack<string> segments = new Stack<string>();
            int currentNodeId = nodeId;
            while (session.Directories != null && currentNodeId >= 0 && currentNodeId < session.Directories.Count)
            {
                DirectoryNodeState node = session.Directories[currentNodeId];
                if (node.NodeId == session.RootNodeId) break;
                if (!string.IsNullOrEmpty(node.Name)) segments.Push(node.Name);
                currentNodeId = node.ParentNodeId;
            }

            string path = session.RootPath;
            while (segments.Count > 0) path = CombinePath(path, segments.Pop());
            return path;
        }

        private static string CombinePath(string parent, string name)
        {
            if (string.IsNullOrEmpty(parent)) return name ?? string.Empty;
            if (string.IsNullOrEmpty(name)) return parent;

            try
            {
                return Path.Combine(parent, name);
            }
            catch
            {
                return parent.TrimEnd('\\', '/') + "\\" + name;
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

        private static string NormalizePathKey(string path)
        {
            string value = (path ?? string.Empty).Trim().Trim('"');
            if (value.Length == 2 && value[1] == ':') return char.ToUpperInvariant(value[0]) + @":\";
            return value.TrimEnd('\\', '/');
        }

        private static bool IsSamePath(string left, string right)
        {
            return string.Equals(NormalizePathKey(left), NormalizePathKey(right), StringComparison.OrdinalIgnoreCase);
        }

        private static string BuildTreeTemplateKey(ScanRequest request)
        {
            return string.Join("|",
                request.SortMode.ToString(),
                request.MinSizeBytes.ToString(),
                request.PerLevelLimit.ToString());
        }

        private static bool IsCompatibleTreeSession(ScanSession session, string templateKey, ScanRequest request)
        {
            if (session == null) return false;
            if (!string.Equals(session.TemplateKey, templateKey, StringComparison.Ordinal)) return false;
            if (!string.IsNullOrWhiteSpace(request.SessionIdentity))
            {
                return string.Equals(session.SessionIdentity, request.SessionIdentity, StringComparison.Ordinal);
            }

            return IsSamePath(session.RootPath, request.Location);
        }

        private void ClearCurrentTreeSessionNoLock()
        {
            if (currentTreeSession == null) return;
            currentTreeSession.Dispose();
            currentTreeSession = null;
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

        private static FileNodeState ParseFileState(JsonTextReader reader)
        {
            FileNodeState item = new FileNodeState();

            while (reader.Read())
            {
                if (reader.TokenType == JsonToken.PropertyName)
                {
                    string propertyName = reader.Value == null ? string.Empty : reader.Value.ToString();
                    if (!reader.Read()) throw new InvalidOperationException("扫描结果不完整。");

                    switch (propertyName)
                    {
                        case "name":
                            item.Name = ReadStringValue(reader.Value);
                            break;
                        case "path":
                            item.Name = StorageFormatting.GetDisplayName(ReadStringValue(reader.Value), false);
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

        private static string ReadStringValue(object value)
        {
            return value == null ? string.Empty : value.ToString();
        }

        private static long ReadInt64(object value)
        {
            if (value == null) return 0;
            return Convert.ToInt64(value);
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

        private sealed class ScanSession : IDisposable
        {
            public string RootPath { get; set; }
            public string TemplateKey { get; set; }
            public string SessionIdentity { get; set; }
            public int RootNodeId { get; set; }
            public List<DirectoryNodeState> Directories { get; set; }

            public void Dispose()
            {
                Directories = null;
            }
        }

        private sealed class DirectoryNodeState
        {
            public int NodeId { get; set; }
            public int ParentNodeId { get; set; }
            public string Name { get; set; }
            public long Bytes { get; set; }
            public int DirectFileCount { get; set; }
            public int TotalFileCount { get; set; }
            public int TotalDirectoryCount { get; set; }
            public FileNodeState[] DirectFiles { get; set; }
            public int[] DirectChildNodeIds { get; set; }
        }

        private struct FileNodeState
        {
            public string Name { get; set; }
            public long Bytes { get; set; }
        }

        private sealed class CliExecutionException : Exception
        {
            public CliExecutionException(string cliError)
                : base(cliError)
            {
                CliError = cliError;
            }

            public string CliError { get; private set; }
        }
    }
}
