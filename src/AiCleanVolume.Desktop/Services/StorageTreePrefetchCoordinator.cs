using System;
using System.Collections.Generic;
using System.Threading;
using AiCleanVolume.Core.Models;
using AiCleanVolume.Core.Services;

namespace AiCleanVolume.Desktop.Services
{
    public sealed class StorageTreePrefetchCoordinator
    {
        private const int MaxPrefetchDepth = 5;
        private const int MaxDirectoriesPerLevel = 6;
        private const int MaxCacheEntries = 256;

        private readonly IScanProvider scanProvider;
        private readonly object syncRoot = new object();
        private readonly Dictionary<string, StorageItem> cache;
        private readonly Queue<string> cacheOrder;
        private readonly Queue<PrefetchWorkItem> queue;
        private readonly HashSet<string> scheduled;

        private ScanRequest sessionTemplate;
        private Action<string> logger;
        private int generation;
        private bool workerRunning;

        public StorageTreePrefetchCoordinator(IScanProvider scanProvider)
        {
            if (scanProvider == null) throw new ArgumentNullException("scanProvider");
            this.scanProvider = scanProvider;
            cache = new Dictionary<string, StorageItem>(StringComparer.OrdinalIgnoreCase);
            cacheOrder = new Queue<string>();
            queue = new Queue<PrefetchWorkItem>();
            scheduled = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }

        public int BeginSession(StorageItem root, ScanRequest template, Action<string> log)
        {
            if (template == null) throw new ArgumentNullException("template");

            lock (syncRoot)
            {
                generation++;
                cache.Clear();
                cacheOrder.Clear();
                queue.Clear();
                scheduled.Clear();
                sessionTemplate = CloneTemplate(template);
                logger = log;
                EnqueueHotDirectories(root, 1);
                EnsureWorker();
                return generation;
            }
        }

        public int Invalidate()
        {
            lock (syncRoot)
            {
                generation++;
                cache.Clear();
                cacheOrder.Clear();
                queue.Clear();
                scheduled.Clear();
                sessionTemplate = null;
                logger = null;
                return generation;
            }
        }

        public bool TryGetCached(string path, out StorageItem item)
        {
            string normalized = Normalize(path);
            lock (syncRoot)
            {
                StorageItem cached;
                if (!cache.TryGetValue(normalized, out cached))
                {
                    item = null;
                    return false;
                }

                item = FolderSizeRankerScanProvider.CloneTree(cached);
                return true;
            }
        }

        public void Remember(StorageItem item)
        {
            if (item == null || string.IsNullOrWhiteSpace(item.Path)) return;

            string normalized = Normalize(item.Path);
            lock (syncRoot)
            {
                StoreCacheItem(normalized, item);
            }
        }

        public void PredictFrom(StorageItem item, int currentDepth)
        {
            if (item == null || currentDepth >= MaxPrefetchDepth) return;

            lock (syncRoot)
            {
                if (sessionTemplate == null) return;
                EnqueueHotDirectories(item, currentDepth + 1);
                EnsureWorker();
            }
        }

        private void EnsureWorker()
        {
            if (workerRunning || queue.Count == 0 || sessionTemplate == null) return;

            workerRunning = true;
            ThreadPool.QueueUserWorkItem(delegate
            {
                WorkerLoop();
            });
        }

        private void WorkerLoop()
        {
            while (true)
            {
                PrefetchWorkItem workItem;
                ScanRequest template;
                Action<string> log;
                int currentGeneration;

                lock (syncRoot)
                {
                    if (queue.Count == 0 || sessionTemplate == null)
                    {
                        workerRunning = false;
                        return;
                    }

                    currentGeneration = generation;
                    workItem = queue.Dequeue();
                    template = CloneTemplate(sessionTemplate);
                    log = logger;
                }

                StorageItem cachedItem;
                if (TryGetCached(workItem.Path, out cachedItem)) continue;

                StorageItem result;
                try
                {
                    result = scanProvider.Scan(CreateScanRequest(workItem.Path, template));
                }
                catch (Exception ex)
                {
                    if (log != null) log("目录预取失败：" + workItem.Path + "，" + ex.Message);
                    continue;
                }

                bool sessionChanged;
                lock (syncRoot)
                {
                    sessionChanged = generation != currentGeneration || sessionTemplate == null;
                    if (!sessionChanged)
                    {
                        StoreCacheItem(workItem.Path, result);
                        if (workItem.Depth < MaxPrefetchDepth) EnqueueHotDirectories(result, workItem.Depth + 1);
                    }
                }

                if (sessionChanged) continue;
            }
        }

        private void EnqueueHotDirectories(StorageItem source, int depth)
        {
            if (depth > MaxPrefetchDepth || source == null || source.Children == null || source.Children.Count == 0) return;

            List<StorageItem> directories = new List<StorageItem>();
            for (int i = 0; i < source.Children.Count; i++)
            {
                StorageItem child = source.Children[i];
                if (child == null || !child.IsDirectory || string.IsNullOrWhiteSpace(child.Path)) continue;
                directories.Add(child);
            }

            directories.Sort(CompareByBytesDescending);
            int count = Math.Min(directories.Count, MaxDirectoriesPerLevel);
            for (int i = 0; i < count; i++)
            {
                string normalized = Normalize(directories[i].Path);
                if (cache.ContainsKey(normalized)) continue;
                if (!scheduled.Add(normalized)) continue;
                queue.Enqueue(new PrefetchWorkItem(directories[i].Path, depth));
            }
        }

        private void StoreCacheItem(string path, StorageItem item)
        {
            string normalized = Normalize(path);
            StorageItem clone = FolderSizeRankerScanProvider.CloneTree(item);
            if (cache.ContainsKey(normalized))
            {
                cache[normalized] = clone;
                return;
            }

            while (cache.Count >= MaxCacheEntries && cacheOrder.Count > 0)
            {
                string oldest = cacheOrder.Dequeue();
                if (cache.Remove(oldest)) break;
            }

            if (cache.Count >= MaxCacheEntries) return;
            cache.Add(normalized, clone);
            cacheOrder.Enqueue(normalized);
        }

        private static ScanRequest CloneTemplate(ScanRequest template)
        {
            ScanRequest request = new ScanRequest();
            request.Location = template.Location;
            request.SortMode = template.SortMode;
            request.MinSizeBytes = template.MinSizeBytes;
            request.PerLevelLimit = template.PerLevelLimit;
            request.LoadDepth = template.LoadDepth;
            return request;
        }

        private static ScanRequest CreateScanRequest(string location, ScanRequest template)
        {
            ScanRequest request = CloneTemplate(template);
            request.Location = location;
            request.LoadDepth = 1;
            return request;
        }

        private static int CompareByBytesDescending(StorageItem left, StorageItem right)
        {
            return right.Bytes.CompareTo(left.Bytes);
        }

        private static string Normalize(string path)
        {
            return (path ?? string.Empty).Trim().TrimEnd('\\', '/');
        }

        private sealed class PrefetchWorkItem
        {
            public PrefetchWorkItem(string path, int depth)
            {
                Path = path;
                Depth = depth;
            }

            public string Path { get; private set; }
            public int Depth { get; private set; }
        }
    }
}
