using System.Collections.Generic;
using AiCleanVolume.Core.Models;
using AiCleanVolume.Core.Services;

namespace AiCleanVolume.Desktop.ViewModels
{
    public sealed class StorageEntryRow : AntdUI.NotifyProperty
    {
        public StorageEntryRow(StorageItem item)
            : this(item, 0)
        {
        }

        public StorageEntryRow(StorageItem item, int depth)
        {
            Children = new List<object>();
            Item = item;
            Depth = depth;
            RefreshFromItem();
        }

        public StorageItem Item { get; private set; }
        public List<object> Children { get; private set; }
        public bool IsLoadingChildren { get; set; }
        public int Depth { get; private set; }

        public void RefreshFromItem()
        {
            if (Item == null) return;

            name = Item.Name;
            path = Item.Path;
            size = StorageFormatting.FormatBytes(Item.Bytes);
            bytes = Item.Bytes;
            files = Item.TotalFileCount;
            dirs = Item.TotalDirectoryCount;
            kind = Item.IsDirectory ? "文件夹" : "文件";
            ReloadChildren();
        }

        public void ReloadChildren()
        {
            Children.Clear();
            IsLoadingChildren = false;
            if (Item == null || !Item.IsDirectory) return;

            if (Item.ChildrenLoaded)
            {
                for (int i = 0; i < Item.Children.Count; i++)
                {
                    Children.Add(new StorageEntryRow(Item.Children[i], Depth + 1));
                }
                return;
            }

            if (Item.HasChildren) Children.Add(ExpandMarker.Instance);
        }

        public string name { get; set; }
        public string size { get; set; }
        public long bytes { get; set; }
        public string kind { get; set; }
        public int files { get; set; }
        public int dirs { get; set; }
        public string path { get; set; }

        private sealed class ExpandMarker
        {
            private ExpandMarker()
            {
            }

            public static readonly ExpandMarker Instance = new ExpandMarker();
        }
    }
}
