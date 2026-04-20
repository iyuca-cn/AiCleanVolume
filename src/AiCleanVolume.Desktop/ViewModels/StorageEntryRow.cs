using System.Collections.Generic;
using AiCleanVolume.Core.Models;
using AiCleanVolume.Core.Services;

namespace AiCleanVolume.Desktop.ViewModels
{
    public sealed class StorageEntryRow : AntdUI.NotifyProperty
    {
        public StorageEntryRow(StorageItem item)
        {
            Children = new List<StorageEntryRow>();
            Item = item;
            RefreshFromItem();
        }

        private StorageEntryRow(string placeholderText)
        {
            Children = new List<StorageEntryRow>();
            IsPlaceholder = true;
            name = placeholderText;
            path = string.Empty;
            size = string.Empty;
            bytes = 0;
            files = 0;
            dirs = 0;
            kind = string.Empty;
        }

        public StorageItem Item { get; private set; }
        public List<StorageEntryRow> Children { get; private set; }
        public bool IsPlaceholder { get; private set; }
        public bool IsLoadingChildren { get; set; }

        public void RefreshFromItem()
        {
            if (IsPlaceholder || Item == null) return;

            name = Item.Name;
            path = Item.Path;
            size = StorageFormatting.FormatBytes(Item.Bytes);
            bytes = Item.Bytes;
            files = Item.TotalFileCount;
            dirs = Item.TotalDirectoryCount;
            kind = Item.IsDirectory ? "文件夹" : "文件";
            ReloadChildren();
        }

        public void ShowLoadingPlaceholder()
        {
            if (IsPlaceholder) return;
            IsLoadingChildren = true;
            Children.Clear();
            Children.Add(new StorageEntryRow("正在加载..."));
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
                    Children.Add(new StorageEntryRow(Item.Children[i]));
                }
                return;
            }

            if (Item.HasChildren) Children.Add(new StorageEntryRow("展开后加载"));
        }

        public string name { get; set; }
        public string size { get; set; }
        public long bytes { get; set; }
        public string kind { get; set; }
        public int files { get; set; }
        public int dirs { get; set; }
        public string path { get; set; }
    }
}
