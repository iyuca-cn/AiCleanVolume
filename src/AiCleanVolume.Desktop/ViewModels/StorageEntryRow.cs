using System.Collections.Generic;
using AiCleanVolume.Core.Models;
using AiCleanVolume.Core.Services;

namespace AiCleanVolume.Desktop.ViewModels
{
    public sealed class StorageEntryRow : AntdUI.NotifyProperty
    {
        public StorageEntryRow(StorageItem item)
        {
            Item = item;
            name = item.Name;
            path = item.Path;
            size = StorageFormatting.FormatBytes(item.Bytes);
            bytes = item.Bytes;
            files = item.TotalFileCount;
            dirs = item.TotalDirectoryCount;
            kind = item.IsDirectory ? "文件夹" : "文件";
            Children = new List<StorageEntryRow>();
            for (int i = 0; i < item.Children.Count; i++) Children.Add(new StorageEntryRow(item.Children[i]));
        }

        public StorageItem Item { get; private set; }
        public List<StorageEntryRow> Children { get; private set; }

        public string name { get; set; }
        public string size { get; set; }
        public long bytes { get; set; }
        public string kind { get; set; }
        public int files { get; set; }
        public int dirs { get; set; }
        public string path { get; set; }
    }
}
