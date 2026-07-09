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

        // 每种模板（排序 / 最小尺寸 / 每层上限）+ 根路径缓存一个原生扫描会话，
        // 按最近使用顺序保留至多 MaxCachedSessions 个，便于在多个磁盘间来回切换时复用已扫描结果。
        private const int MaxCachedSessions = 3;
        private readonly Dictionary<string, ScanSession> treeSessions = new Dictionary<string, ScanSession>(StringComparer.Ordinal);
        private readonly List<string> sessionUsageOrder = new List<string>();

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
                ClearAllTreeSessionsNoLock();
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
                ScanSession cached = FindCachedSessionNoLock(templateKey, request);
                if (cached != null)
                {
                    TouchSessionNoLock(cached.CacheKey);
                    return cached;
                }

                ScanSession session = null;
                try
                {
                    session = BuildTreeSession(request, templateKey);
                    session.CacheKey = BuildSessionCacheKey(templateKey, session.RootPath);
                    StoreSessionNoLock(session);
                    return session;
                }
                catch
                {
                    if (session != null) session.Dispose();
                    throw;
                }
            }
        }

        // 懒加载子目录（scan.children）带着会话标识，按标识在所有缓存会话中定位；
        // 首次扫描（scan.start）无标识，按模板 + 根路径命中已扫描过的同一磁盘。
        private ScanSession FindCachedSessionNoLock(string templateKey, ScanRequest request)
        {
            if (!string.IsNullOrWhiteSpace(request.SessionIdentity))
            {
                foreach (ScanSession session in treeSessions.Values)
                {
                    if (string.Equals(session.TemplateKey, templateKey, StringComparison.Ordinal) &&
                        string.Equals(session.SessionIdentity, request.SessionIdentity, StringComparison.Ordinal))
                    {
                        return session;
                    }
                }
                return null;
            }

            ScanSession match;
            return treeSessions.TryGetValue(BuildSessionCacheKey(templateKey, request.Location), out match) ? match : null;
        }

        private void StoreSessionNoLock(ScanSession session)
        {
            treeSessions[session.CacheKey] = session;
            TouchSessionNoLock(session.CacheKey);

            while (sessionUsageOrder.Count > MaxCachedSessions)
            {
                string oldestKey = sessionUsageOrder[0];
                sessionUsageOrder.RemoveAt(0);

                ScanSession evicted;
                if (treeSessions.TryGetValue(oldestKey, out evicted))
                {
                    treeSessions.Remove(oldestKey);
                    evicted.Dispose();
                }
            }
        }

        private void TouchSessionNoLock(string cacheKey)
        {
            sessionUsageOrder.Remove(cacheKey);
            sessionUsageOrder.Add(cacheKey);
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
