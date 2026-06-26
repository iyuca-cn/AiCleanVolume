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
using AiCleanVolume.NativeBridge;
using Newtonsoft.Json;


namespace AiCleanVolume.Desktop.Infrastructure.Scanning
{
    public sealed partial class FolderSizeRankerScanProvider : IScanProvider
    {
        private static NativeScanOptions CreateNativeOptions(ScanRequest request)
        {
            NativeScanOptions options = new NativeScanOptions();
            options.Location = request.Location;
            options.SortMode = request.SortMode == ScanSortMode.Logical ? 0 : 1;
            options.MinSizeBytes = request.MinSizeBytes;
            options.PerLevelLimit = request.PerLevelLimit;
            return options;
        }

        private static int ResolveChildCount(ScanRequest request)
        {
            return request.ChildCount > 0 ? request.ChildCount : DefaultChildWindowSize;
        }

        private static int ResolveNodeId(ScanSession session, ScanRequest request)
        {
            if (session == null || request == null) return -1;

            if (!string.IsNullOrWhiteSpace(request.SessionIdentity) &&
                string.Equals(session.SessionIdentity, request.SessionIdentity, StringComparison.Ordinal) &&
                request.SessionNodeId >= 0)
            {
                return request.SessionNodeId;
            }

            if (IsSamePath(session.RootPath, request.Location)) return session.RootNodeId;
            return -1;
        }

        private static StorageItem MaterializeDirectory(
            ScanSession session,
            int nodeId,
            int remainingDepth,
            bool isRoot,
            string directoryPath,
            int childStart,
            int childCount)
        {
            NativeNodeInfo node = session.NativeSession.GetNode(nodeId);
            string path = isRoot
                ? (string.IsNullOrWhiteSpace(node.Path) ? session.RootPath : node.Path)
                : (string.IsNullOrWhiteSpace(directoryPath) ? BuildDirectoryPath(session, nodeId) : directoryPath);
            StorageItem item = CreateStorageDirectoryItem(session, node, path, remainingDepth > 0, isRoot);
            if (remainingDepth <= 0) return item;

            NativeChildPage page = session.NativeSession.GetChildren(nodeId, childStart, childCount);
            item.ChildStart = childStart;
            item.ChildCount = childCount;
            item.LoadedChildCount = page.Items == null ? 0 : page.Items.Length;
            item.TotalChildCount = page.TotalCount;

            if (page.Items != null)
            {
                int nextDepth = remainingDepth == int.MaxValue ? int.MaxValue : remainingDepth - 1;
                for (int i = 0; i < page.Items.Length; i++)
                {
                    NativeChildInfo child = page.Items[i];
                    if (!child.IsDirectory)
                    {
                        item.Children.Add(CreateStorageFileItem(child, path));
                        continue;
                    }

                    string childPath = CombinePath(path, child.Name);
                    if (remainingDepth == 1)
                    {
                        item.Children.Add(CreateStorageDirectoryItem(session, child, childPath, false));
                        continue;
                    }

                    item.Children.Add(MaterializeDirectory(session, child.NodeId, nextDepth, false, childPath, 0, childCount));
                }
            }

            item.ChildrenLoaded = true;
            return item;
        }

        private static StorageItem CreateStorageDirectoryItem(
            ScanSession session,
            NativeNodeInfo node,
            string path,
            bool childrenLoaded,
            bool isRoot)
        {
            StorageItem item = new StorageItem();
            item.Path = path;
            item.Name = isRoot ? path : (string.IsNullOrEmpty(node.Name) ? StorageFormatting.GetDisplayName(path, true) : node.Name);
            item.Bytes = node.Bytes;
            item.IsDirectory = true;
            item.HasChildren = node.HasChildren;
            item.ChildrenLoaded = childrenLoaded;
            item.DirectFileCount = node.DirectFileCount;
            item.TotalFileCount = node.TotalFileCount;
            item.TotalDirectoryCount = node.TotalDirectoryCount;
            item.SessionIdentity = session == null ? null : session.SessionIdentity;
            item.SessionNodeId = node.NodeId;
            item.TotalChildCount = node.DirectFileCount + node.DirectChildDirectoryCount;
            return item;
        }

        private static StorageItem CreateStorageDirectoryItem(
            ScanSession session,
            NativeChildInfo node,
            string path,
            bool childrenLoaded)
        {
            StorageItem item = new StorageItem();
            item.Path = path;
            item.Name = string.IsNullOrEmpty(node.Name) ? StorageFormatting.GetDisplayName(path, true) : node.Name;
            item.Bytes = node.Bytes;
            item.IsDirectory = true;
            item.HasChildren = node.HasChildren;
            item.ChildrenLoaded = childrenLoaded;
            item.DirectFileCount = node.DirectFileCount;
            item.TotalFileCount = node.TotalFileCount;
            item.TotalDirectoryCount = node.TotalDirectoryCount;
            item.SessionIdentity = session == null ? null : session.SessionIdentity;
            item.SessionNodeId = node.NodeId;
            item.TotalChildCount = node.DirectFileCount + node.DirectChildDirectoryCount;
            return item;
        }

        private static StorageItem CreateStorageFileItem(NativeChildInfo state, string parentPath)
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
            item.SessionNodeId = -1;
            return item;
        }

        private static string BuildDirectoryPath(ScanSession session, int nodeId)
        {
            if (session == null || nodeId == session.RootNodeId) return session == null ? string.Empty : session.RootPath;

            Stack<string> segments = new Stack<string>();
            int currentNodeId = nodeId;
            while (currentNodeId >= 0)
            {
                NativeNodeInfo node = session.NativeSession.GetNode(currentNodeId);
                if (node.NodeId == session.RootNodeId) break;
                if (!string.IsNullOrEmpty(node.Name)) segments.Push(node.Name);
                currentNodeId = node.ParentNodeId;
            }

            string path = session.RootPath;
            while (segments.Count > 0) path = CombinePath(path, segments.Pop());
            return path;
        }
    }
}
