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
        private readonly object syncRoot = new object();

        private ScanSession currentTreeSession;

        public FolderSizeRankerScanProvider()
        {
        }

        public StorageItem Scan(ScanRequest request)
        {
            if (request == null) throw new ArgumentNullException("request");
            if (string.IsNullOrWhiteSpace(request.Location)) throw new InvalidOperationException("扫描位置不能为空。");

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
            clone.ChildStart = source.ChildStart;
            clone.ChildCount = source.ChildCount;
            clone.LoadedChildCount = source.LoadedChildCount;
            clone.TotalChildCount = source.TotalChildCount;
            for (int i = 0; i < source.Children.Count; i++)
            {
                clone.Children.Add(CloneTree(source.Children[i]));
            }

            return clone;
        }

        private StorageItem ScanFull(ScanRequest request)
        {
            ScanSession session = EnsureTreeSession(request);
            return MaterializeDirectory(session, session.RootNodeId, 1, true, session.RootPath, request.ChildStart, ResolveChildCount(request));
        }

        private StorageItem ScanPartial(ScanRequest request)
        {
            ScanSession session = EnsureTreeSession(request);
            int nodeId = ResolveNodeId(session, request);
            if (nodeId < 0)
            {
                throw new InvalidOperationException("目录树会话未包含路径：" + request.Location);
            }

            return MaterializeDirectory(
                session,
                nodeId,
                request.LoadDepth,
                IsSamePath(session.RootPath, request.Location),
                request.Location,
                request.ChildStart,
                ResolveChildCount(request));
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
            NativeScanOptions options = CreateNativeOptions(request);
            NativeMftScanSession nativeSession = null;
            try
            {
                nativeSession = NativeMftScanSession.Scan(options);
                NativeNodeInfo rootNode = nativeSession.GetRootNode();

                ScanSession session = new ScanSession();
                session.RootPath = NormalizeLocation(string.IsNullOrWhiteSpace(rootNode.Path) ? request.Location : rootNode.Path);
                session.TemplateKey = templateKey;
                session.SessionIdentity = Guid.NewGuid().ToString("N");
                session.RootNodeId = rootNode.NodeId;
                session.NativeSession = nativeSession;
                nativeSession = null;
                return session;
            }
            finally
            {
                if (nativeSession != null) nativeSession.Dispose();
            }
        }
    }
}
