using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using AiCleanVolume.Core.Domain.Cleanup;
using AiCleanVolume.Core.Domain.Sandbox;
using AiCleanVolume.Core.Domain.Settings;
using AiCleanVolume.Core.Domain.Storage;
using AiCleanVolume.Core.Application.CleanupPlanning;
using AiCleanVolume.Core.Application.Deletion;
using AiCleanVolume.Core.Application.Scanning;
using AiCleanVolume.Core.Kernel.Ports;
using Newtonsoft.Json;


namespace AiCleanVolume.Desktop.Infrastructure.Scanning
{
    public sealed partial class FolderSizeRankerScanProvider : IScanProvider
    {
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

        private static string ReadStringValue(object value)
        {
            return value == null ? string.Empty : value.ToString();
        }

        private static long ReadInt64(object value)
        {
            if (value == null) return 0;
            return Convert.ToInt64(value);
        }
    }
}
