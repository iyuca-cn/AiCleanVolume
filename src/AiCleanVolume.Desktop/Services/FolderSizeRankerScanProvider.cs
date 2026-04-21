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
        private static readonly int UnicodePreambleLength = Encoding.Unicode.GetPreamble().Length;

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
            try
            {
                ScanSession session = EnsureTreeSession(request);
                DirectoryNodeIndex entry;
                if (!session.DirectoryIndex.TryGetValue(NormalizePathKey(request.Location), out entry))
                {
                    throw new InvalidOperationException("目录树会话未包含路径：" + request.Location);
                }

                return MaterializeDirectory(session, entry, request.LoadDepth, IsSamePath(session.RootPath, request.Location));
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
            string locationKey = NormalizePathKey(request.Location);
            string templateKey = BuildTreeTemplateKey(request);

            lock (syncRoot)
            {
                if (IsCompatibleTreeSession(currentTreeSession, templateKey, locationKey))
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
                DirectoryNodeIndex rootEntry = null;
                Exception parseError = null;

                try
                {
                    using (JsonTextReader reader = new JsonTextReader(process.StandardOutput))
                    {
                        Dictionary<string, DirectoryNodeIndex> directoryIndex = BuildParsedDirectoryIndex(reader, out rootEntry);
                        if (rootEntry == null)
                        {
                            throw new InvalidOperationException("目录树会话根节点为空。");
                        }

                        ScanSession session = new ScanSession();
                        session.RootPath = rootEntry.Path;
                        session.TemplateKey = templateKey;
                        session.DirectoryIndex = directoryIndex;
                        return session;
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
                throw new InvalidOperationException("扫描结果为空或 JSON 无法解析。");
            }
        }

        private static Dictionary<string, DirectoryNodeIndex> BuildParsedDirectoryIndex(JsonTextReader reader, out DirectoryNodeIndex rootEntry)
        {
            if (!reader.Read() || reader.TokenType != JsonToken.StartObject)
            {
                throw new InvalidOperationException("扫描结果为空或 JSON 无法解析。");
            }

            Dictionary<string, DirectoryNodeIndex> index = new Dictionary<string, DirectoryNodeIndex>(StringComparer.OrdinalIgnoreCase);
            rootEntry = ParseDirectoryIndex(reader, index);
            return index;
        }

        private static DirectoryNodeIndex ParseDirectoryIndex(JsonTextReader reader, IDictionary<string, DirectoryNodeIndex> index)
        {
            DirectoryNodeIndex entry = new DirectoryNodeIndex();
            entry.DirectFiles = new List<StorageItem>();
            entry.DirectDirectoryPaths = new List<string>();

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
                            entry.Path = reader.Value == null ? string.Empty : reader.Value.ToString();
                            break;
                        case "bytes":
                            entry.Bytes = ReadInt64(reader.Value);
                            break;
                        case "files":
                            ParseIndexedFiles(reader, entry.DirectFiles, ref directFileCount);
                            break;
                        case "children":
                            ParseIndexedChildren(reader, index, entry.DirectDirectoryPaths, ref totalFiles, ref totalDirs);
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

            entry.DirectFileCount = directFileCount;
            entry.TotalFileCount = directFileCount + totalFiles;
            entry.TotalDirectoryCount = totalDirs;
            index[NormalizePathKey(entry.Path)] = entry;
            return entry;
        }

        private static void ParseIndexedFiles(JsonTextReader reader, IList<StorageItem> target, ref int directFileCount)
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
                target.Add(file);
            }
        }

        private static void ParseIndexedChildren(
            JsonTextReader reader,
            IDictionary<string, DirectoryNodeIndex> index,
            IList<string> directDirectoryPaths,
            ref int totalFiles,
            ref int totalDirs)
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

                DirectoryNodeIndex child = ParseDirectoryIndex(reader, index);
                directDirectoryPaths.Add(child.Path);
                totalFiles += child.TotalFileCount;
                totalDirs += 1 + child.TotalDirectoryCount;
            }
        }

        private static Dictionary<string, DirectoryNodeIndex> BuildDirectoryIndex(string tempFilePath)
        {
            using (StreamReader reader = new StreamReader(tempFilePath, Encoding.Unicode, false))
            {
                TextReaderCursor cursor = new TextReaderCursor(reader);
                Dictionary<string, DirectoryNodeIndex> index = new Dictionary<string, DirectoryNodeIndex>(StringComparer.OrdinalIgnoreCase);

                SkipWhitespace(cursor);
                DirectoryNodeIndex root = IndexDirectoryObject(cursor, index);
                if (root == null) throw new InvalidOperationException("目录树缓存索引构建失败。");
                SkipWhitespace(cursor);
                if (cursor.Peek() != -1) throw new InvalidOperationException("目录树缓存包含多余内容。");
                return index;
            }
        }

        private static DirectoryNodeIndex IndexDirectoryObject(IJsonCursor cursor, IDictionary<string, DirectoryNodeIndex> index)
        {
            long startChar = cursor.Position;
            Expect(cursor, '{');

            string path = string.Empty;
            long bytes = 0;
            int directFileCount = 0;
            int totalFiles = 0;
            int totalDirs = 0;
            long filesStartChar = -1;
            int filesLengthChars = 0;
            List<string> directDirectoryPaths = new List<string>();

            SkipWhitespace(cursor);
            if (TryConsume(cursor, '}'))
            {
                return RegisterDirectoryIndex(index, path, bytes, directFileCount, totalFiles, totalDirs, filesStartChar, filesLengthChars, directDirectoryPaths, startChar, cursor.Position);
            }

            while (true)
            {
                string propertyName = ReadString(cursor);
                SkipWhitespace(cursor);
                Expect(cursor, ':');
                SkipWhitespace(cursor);

                switch (propertyName)
                {
                    case "path":
                        path = ReadString(cursor);
                        break;
                    case "bytes":
                        bytes = ReadInt64(cursor);
                        break;
                    case "files":
                        filesStartChar = cursor.Position;
                        directFileCount = SkipAndCountFilesArray(cursor);
                        filesLengthChars = (int)(cursor.Position - filesStartChar);
                        break;
                    case "children":
                        List<DirectoryNodeIndex> children = IndexChildrenArray(cursor, index);
                        for (int i = 0; i < children.Count; i++)
                        {
                            DirectoryNodeIndex child = children[i];
                            directDirectoryPaths.Add(child.Path);
                            totalFiles += child.TotalFileCount;
                            totalDirs += 1 + child.TotalDirectoryCount;
                        }
                        break;
                    default:
                        SkipValue(cursor);
                        break;
                }

                SkipWhitespace(cursor);
                if (TryConsume(cursor, ',')) continue;
                Expect(cursor, '}');
                break;
            }

            return RegisterDirectoryIndex(index, path, bytes, directFileCount, totalFiles, totalDirs, filesStartChar, filesLengthChars, directDirectoryPaths, startChar, cursor.Position);
        }

        private static DirectoryNodeIndex RegisterDirectoryIndex(
            IDictionary<string, DirectoryNodeIndex> index,
            string path,
            long bytes,
            int directFileCount,
            int totalFiles,
            int totalDirs,
            long filesStartChar,
            int filesLengthChars,
            IList<string> directDirectoryPaths,
            long startChar,
            long endChar)
        {
            DirectoryNodeIndex entry = new DirectoryNodeIndex();
            entry.Path = path;
            entry.Bytes = bytes;
            entry.DirectFileCount = directFileCount;
            entry.TotalFileCount = directFileCount + totalFiles;
            entry.TotalDirectoryCount = totalDirs;
            entry.FilesStartChar = filesStartChar;
            entry.FilesLengthChars = filesLengthChars;
            entry.StartChar = startChar;
            entry.LengthChars = (int)(endChar - startChar);
            entry.DirectDirectoryPaths = new List<string>(directDirectoryPaths);

            string key = NormalizePathKey(path);
            index[key] = entry;
            return entry;
        }

        private static List<DirectoryNodeIndex> IndexChildrenArray(IJsonCursor cursor, IDictionary<string, DirectoryNodeIndex> index)
        {
            List<DirectoryNodeIndex> children = new List<DirectoryNodeIndex>();

            if (TryConsumeLiteral(cursor, "null")) return children;

            Expect(cursor, '[');
            SkipWhitespace(cursor);
            if (TryConsume(cursor, ']')) return children;

            while (true)
            {
                DirectoryNodeIndex child = IndexDirectoryObject(cursor, index);
                children.Add(child);

                SkipWhitespace(cursor);
                if (TryConsume(cursor, ',')) continue;
                Expect(cursor, ']');
                break;
            }

            return children;
        }

        private static int SkipAndCountFilesArray(IJsonCursor cursor)
        {
            if (TryConsumeLiteral(cursor, "null")) return 0;

            int count = 0;
            Expect(cursor, '[');
            SkipWhitespace(cursor);
            if (TryConsume(cursor, ']')) return 0;

            while (true)
            {
                SkipValue(cursor);
                count++;

                SkipWhitespace(cursor);
                if (TryConsume(cursor, ',')) continue;
                Expect(cursor, ']');
                break;
            }

            return count;
        }

        private static void SkipValue(IJsonCursor cursor)
        {
            SkipWhitespace(cursor);
            int token = cursor.Peek();
            if (token == -1) throw new InvalidOperationException("JSON 提前结束。");

            switch ((char)token)
            {
                case '{':
                    Expect(cursor, '{');
                    SkipWhitespace(cursor);
                    if (TryConsume(cursor, '}')) return;
                    while (true)
                    {
                        ReadString(cursor);
                        SkipWhitespace(cursor);
                        Expect(cursor, ':');
                        SkipWhitespace(cursor);
                        SkipValue(cursor);
                        SkipWhitespace(cursor);
                        if (TryConsume(cursor, ',')) continue;
                        Expect(cursor, '}');
                        return;
                    }
                case '[':
                    Expect(cursor, '[');
                    SkipWhitespace(cursor);
                    if (TryConsume(cursor, ']')) return;
                    while (true)
                    {
                        SkipValue(cursor);
                        SkipWhitespace(cursor);
                        if (TryConsume(cursor, ',')) continue;
                        Expect(cursor, ']');
                        return;
                    }
                case '"':
                    ReadString(cursor);
                    return;
                case 'n':
                    ReadLiteral(cursor, "null");
                    return;
                case 't':
                    ReadLiteral(cursor, "true");
                    return;
                case 'f':
                    ReadLiteral(cursor, "false");
                    return;
                default:
                    ReadNumber(cursor);
                    return;
            }
        }

        private static StorageItem MaterializeDirectory(ScanSession session, DirectoryNodeIndex entry, int remainingDepth, bool isRoot)
        {
            StorageItem item = new StorageItem();
            item.Path = entry.Path;
            item.Name = isRoot ? entry.Path : StorageFormatting.GetDisplayName(entry.Path, true);
            item.Bytes = entry.Bytes;
            item.IsDirectory = true;
            item.HasChildren = entry.DirectFileCount > 0 || entry.TotalDirectoryCount > 0;
            item.DirectFileCount = entry.DirectFileCount;
            item.TotalFileCount = entry.TotalFileCount;
            item.TotalDirectoryCount = entry.TotalDirectoryCount;
            item.ChildrenLoaded = remainingDepth > 0;

            if (remainingDepth <= 0) return item;

            for (int i = 0; i < entry.DirectFiles.Count; i++)
            {
                item.Children.Add(CloneTree(entry.DirectFiles[i]));
            }

            for (int i = 0; i < entry.DirectDirectoryPaths.Count; i++)
            {
                string childPath = entry.DirectDirectoryPaths[i];
                DirectoryNodeIndex childEntry;
                if (!session.DirectoryIndex.TryGetValue(NormalizePathKey(childPath), out childEntry)) continue;
                item.Children.Add(MaterializeDirectory(session, childEntry, remainingDepth - 1, false));
            }

            return item;
        }

        private static IList<StorageItem> ParseFilesSlice(ScanSession session, DirectoryNodeIndex entry)
        {
            List<StorageItem> result = new List<StorageItem>();
            if (entry.FilesStartChar < 0 || entry.FilesLengthChars <= 0) return result;

            string json = ReadUnicodeSlice(session.TempFilePath, entry.FilesStartChar, entry.FilesLengthChars);
            if (string.IsNullOrEmpty(json)) return result;

            StringCursor cursor = new StringCursor(json);
            SkipWhitespace(cursor);
            if (TryConsumeLiteral(cursor, "null")) return result;

            Expect(cursor, '[');
            SkipWhitespace(cursor);
            if (TryConsume(cursor, ']')) return result;

            while (true)
            {
                result.Add(ParseFileObject(cursor));
                SkipWhitespace(cursor);
                if (TryConsume(cursor, ',')) continue;
                Expect(cursor, ']');
                break;
            }

            return result;
        }

        private static StorageItem ParseFileObject(IJsonCursor cursor)
        {
            StorageItem item = new StorageItem();
            item.IsDirectory = false;
            item.HasChildren = false;
            item.ChildrenLoaded = true;
            item.DirectFileCount = 0;
            item.TotalFileCount = 1;
            item.TotalDirectoryCount = 0;

            Expect(cursor, '{');
            SkipWhitespace(cursor);
            if (TryConsume(cursor, '}')) return item;

            while (true)
            {
                string propertyName = ReadString(cursor);
                SkipWhitespace(cursor);
                Expect(cursor, ':');
                SkipWhitespace(cursor);

                switch (propertyName)
                {
                    case "path":
                        item.Path = ReadString(cursor);
                        item.Name = StorageFormatting.GetDisplayName(item.Path, false);
                        break;
                    case "bytes":
                        item.Bytes = ReadInt64(cursor);
                        break;
                    default:
                        SkipValue(cursor);
                        break;
                }

                SkipWhitespace(cursor);
                if (TryConsume(cursor, ',')) continue;
                Expect(cursor, '}');
                break;
            }

            if (string.IsNullOrEmpty(item.Name)) item.Name = StorageFormatting.GetDisplayName(item.Path, false);
            return item;
        }

        private static string ReadUnicodeSlice(string path, long startChar, int lengthChars)
        {
            if (lengthChars <= 0) return string.Empty;

            byte[] buffer = new byte[lengthChars * 2];
            int totalRead = 0;
            using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                stream.Seek(UnicodePreambleLength + startChar * 2L, SeekOrigin.Begin);
                while (totalRead < buffer.Length)
                {
                    int read = stream.Read(buffer, totalRead, buffer.Length - totalRead);
                    if (read <= 0) break;
                    totalRead += read;
                }
            }

            if (totalRead == 0) return string.Empty;
            if (totalRead != buffer.Length)
            {
                byte[] resized = new byte[totalRead];
                Buffer.BlockCopy(buffer, 0, resized, 0, totalRead);
                buffer = resized;
            }

            return Encoding.Unicode.GetString(buffer);
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

        private static bool IsCompatibleTreeSession(ScanSession session, string templateKey, string locationKey)
        {
            if (session == null) return false;
            if (!string.Equals(session.TemplateKey, templateKey, StringComparison.Ordinal)) return false;
            return session.DirectoryIndex.ContainsKey(locationKey);
        }

        private void ClearCurrentTreeSessionNoLock()
        {
            if (currentTreeSession == null) return;
            currentTreeSession.Dispose();
            currentTreeSession = null;
        }

        private static void CopyText(TextReader source, TextWriter target)
        {
            char[] buffer = new char[8192];
            while (true)
            {
                int read = source.Read(buffer, 0, buffer.Length);
                if (read <= 0) break;
                target.Write(buffer, 0, read);
            }
        }

        private static void TryDeleteFile(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return;
            try
            {
                if (File.Exists(path)) File.Delete(path);
            }
            catch
            {
            }
        }

        private static void SkipWhitespace(IJsonCursor cursor)
        {
            while (true)
            {
                int value = cursor.Peek();
                if (value == -1) return;

                char ch = (char)value;
                if (ch != ' ' && ch != '\t' && ch != '\r' && ch != '\n') return;
                cursor.Read();
            }
        }

        private static bool TryConsume(IJsonCursor cursor, char expected)
        {
            SkipWhitespace(cursor);
            if (cursor.Peek() != expected) return false;
            cursor.Read();
            return true;
        }

        private static void Expect(IJsonCursor cursor, char expected)
        {
            SkipWhitespace(cursor);
            int value = cursor.Read();
            if (value != expected)
            {
                throw new InvalidOperationException("JSON 格式错误，期待 '" + expected + "'。");
            }
        }

        private static bool TryConsumeLiteral(IJsonCursor cursor, string value)
        {
            SkipWhitespace(cursor);
            int token = cursor.Peek();
            if (token == -1 || token != value[0]) return false;
            ReadLiteral(cursor, value);
            return true;
        }

        private static void ReadLiteral(IJsonCursor cursor, string value)
        {
            for (int i = 0; i < value.Length; i++)
            {
                int read = cursor.Read();
                if (read != value[i])
                {
                    throw new InvalidOperationException("JSON 字面量格式错误。");
                }
            }
        }

        private static long ReadInt64(IJsonCursor cursor)
        {
            SkipWhitespace(cursor);
            bool negative = false;
            if (cursor.Peek() == '-')
            {
                negative = true;
                cursor.Read();
            }

            long value = 0;
            bool hasDigit = false;
            while (true)
            {
                int token = cursor.Peek();
                if (token < '0' || token > '9') break;
                hasDigit = true;
                value = value * 10 + (cursor.Read() - '0');
            }

            if (!hasDigit) throw new InvalidOperationException("JSON 数字格式错误。");
            return negative ? -value : value;
        }

        private static void ReadNumber(IJsonCursor cursor)
        {
            ReadInt64(cursor);
        }

        private static string ReadString(IJsonCursor cursor)
        {
            SkipWhitespace(cursor);
            if (cursor.Read() != '"') throw new InvalidOperationException("JSON 字符串格式错误。");

            StringBuilder builder = new StringBuilder();
            while (true)
            {
                int value = cursor.Read();
                if (value == -1) throw new InvalidOperationException("JSON 字符串提前结束。");
                if (value == '"') break;

                if (value != '\\')
                {
                    builder.Append((char)value);
                    continue;
                }

                int escaped = cursor.Read();
                if (escaped == -1) throw new InvalidOperationException("JSON 转义序列提前结束。");

                switch ((char)escaped)
                {
                    case '"':
                    case '\\':
                    case '/':
                        builder.Append((char)escaped);
                        break;
                    case 'b':
                        builder.Append('\b');
                        break;
                    case 'f':
                        builder.Append('\f');
                        break;
                    case 'n':
                        builder.Append('\n');
                        break;
                    case 'r':
                        builder.Append('\r');
                        break;
                    case 't':
                        builder.Append('\t');
                        break;
                    case 'u':
                        builder.Append((char)ReadHex16(cursor));
                        break;
                    default:
                        throw new InvalidOperationException("JSON 转义序列无效。");
                }
            }

            return builder.ToString();
        }

        private static int ReadHex16(IJsonCursor cursor)
        {
            int value = 0;
            for (int i = 0; i < 4; i++)
            {
                int digit = cursor.Read();
                if (digit == -1) throw new InvalidOperationException("JSON Unicode 转义提前结束。");

                value <<= 4;
                if (digit >= '0' && digit <= '9') value += digit - '0';
                else if (digit >= 'a' && digit <= 'f') value += digit - 'a' + 10;
                else if (digit >= 'A' && digit <= 'F') value += digit - 'A' + 10;
                else throw new InvalidOperationException("JSON Unicode 转义格式错误。");
            }

            return value;
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
                            if (remainingDepth > 0) ParseChildren(reader, remainingDepth, childDirectories, ref totalFiles, ref totalDirs);
                            else ParseChildrenSummary(reader, ref totalDirs);
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

        private static void ParseChildrenSummary(JsonTextReader reader, ref int totalDirs)
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

                totalDirs++;
                reader.Skip();
            }
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

        private static long ReadInt64(object value)
        {
            if (value == null) return 0;
            return Convert.ToInt64(value);
        }

        private static int CompareByBytesDescending(StorageItem left, StorageItem right)
        {
            return right.Bytes.CompareTo(left.Bytes);
        }

        private static void AddAll(IList<StorageItem> target, IList<StorageItem> source)
        {
            for (int i = 0; i < source.Count; i++) target.Add(source[i]);
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

        private sealed class ScanSession : IDisposable
        {
            public string RootPath { get; set; }
            public string TemplateKey { get; set; }
            public string TempFilePath { get; set; }
            public Dictionary<string, DirectoryNodeIndex> DirectoryIndex { get; set; }

            public void Dispose()
            {
                DirectoryIndex = null;
                TempFilePath = null;
            }
        }

        private sealed class DirectoryNodeIndex
        {
            public string Path { get; set; }
            public long Bytes { get; set; }
            public int DirectFileCount { get; set; }
            public int TotalFileCount { get; set; }
            public int TotalDirectoryCount { get; set; }
            public long FilesStartChar { get; set; }
            public int FilesLengthChars { get; set; }
            public long StartChar { get; set; }
            public int LengthChars { get; set; }
            public List<StorageItem> DirectFiles { get; set; }
            public List<string> DirectDirectoryPaths { get; set; }
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

        private sealed class CliExecutionException : Exception
        {
            public CliExecutionException(string cliError)
                : base(cliError)
            {
                CliError = cliError;
            }

            public string CliError { get; private set; }
        }

        private interface IJsonCursor
        {
            long Position { get; }
            int Peek();
            int Read();
        }

        private sealed class StringCursor : IJsonCursor
        {
            private readonly string text;
            private int index;

            public StringCursor(string text)
            {
                this.text = text ?? string.Empty;
                index = 0;
            }

            public long Position
            {
                get { return index; }
            }

            public int Peek()
            {
                return index < text.Length ? text[index] : -1;
            }

            public int Read()
            {
                return index < text.Length ? text[index++] : -1;
            }
        }

        private sealed class TextReaderCursor : IJsonCursor
        {
            private readonly TextReader reader;
            private int buffered;
            private bool hasBuffered;
            private long position;

            public TextReaderCursor(TextReader reader)
            {
                this.reader = reader;
            }

            public long Position
            {
                get { return position; }
            }

            public int Peek()
            {
                if (!hasBuffered)
                {
                    buffered = reader.Read();
                    hasBuffered = true;
                }

                return buffered;
            }

            public int Read()
            {
                int value = Peek();
                hasBuffered = false;
                if (value != -1) position++;
                return value;
            }
        }
    }
}
