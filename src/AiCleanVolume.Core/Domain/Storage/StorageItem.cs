using System.Collections.Generic;

namespace AiCleanVolume.Core.Domain.Storage
{
    public sealed class StorageItem
    {
        public StorageItem()
        {
            Children = new List<StorageItem>();
            ChildrenLoaded = true;
            SessionNodeId = -1;
            ChildStart = 0;
            ChildCount = 0;
            LoadedChildCount = 0;
            TotalChildCount = 0;
        }

        public string Path { get; set; }
        public string Name { get; set; }
        public long Bytes { get; set; }
        public bool IsDirectory { get; set; }
        public bool HasChildren { get; set; }
        public bool ChildrenLoaded { get; set; }
        public int DirectFileCount { get; set; }
        public int TotalFileCount { get; set; }
        public int TotalDirectoryCount { get; set; }
        public IList<StorageItem> Children { get; private set; }
        public string SessionIdentity { get; set; }
        public int SessionNodeId { get; set; }
        public int ChildStart { get; set; }
        public int ChildCount { get; set; }
        public int LoadedChildCount { get; set; }
        public int TotalChildCount { get; set; }
    }
}
