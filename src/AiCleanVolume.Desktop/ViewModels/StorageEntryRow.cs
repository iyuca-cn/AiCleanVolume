using System;
using System.Collections.Generic;
using AiCleanVolume.Core.Domain.Cleanup;
using AiCleanVolume.Core.Domain.Sandbox;
using AiCleanVolume.Core.Domain.Settings;
using AiCleanVolume.Core.Domain.Storage;
using AiCleanVolume.Core.Application.CleanupPlanning;
using AiCleanVolume.Core.Application.Deletion;
using AiCleanVolume.Core.Application.Scanning;
using AiCleanVolume.Core.Kernel.Ports;

namespace AiCleanVolume.Desktop.ViewModels
{
    public sealed class StorageEntryRow : AntdUI.NotifyProperty
    {
        public StorageEntryRow(StorageItem item)
            : this(item, 0, false)
        {
        }

        public StorageEntryRow(StorageItem item, bool materializeLoadedChildren)
            : this(item, 0, materializeLoadedChildren)
        {
        }

        public StorageEntryRow(StorageItem item, int depth)
            : this(item, depth, false)
        {
        }

        public StorageEntryRow(StorageItem item, int depth, bool materializeLoadedChildren)
            : this(item, depth, null, materializeLoadedChildren)
        {
        }

        private StorageEntryRow(StorageItem item, int depth, StorageEntryRow parent, bool materializeLoadedChildren)
        {
            Children = new List<object>();
            Item = item;
            Depth = depth;
            Parent = parent;
            RefreshFromItem(materializeLoadedChildren);
        }

        public StorageItem Item { get; private set; }
        public StorageEntryRow Parent { get; private set; }
        public List<object> Children { get; private set; }
        public bool IsLoadingChildren { get; set; }
        public int Depth { get; private set; }
        public bool AreChildRowsMaterialized { get; private set; }

        public void RefreshFromItem()
        {
            RefreshFromItem(false);
        }

        public void RefreshFromItem(bool materializeLoadedChildren)
        {
            if (Item == null) return;

            RefreshDisplayValues();
            ReloadChildren(materializeLoadedChildren);
        }

        public void RefreshDisplayValues()
        {
            if (Item == null) return;

            name = Item.Name;
            path = Item.Path;
            size = StorageFormatting.FormatBytes(Item.Bytes);
            bytes = Item.Bytes;
            files = Item.TotalFileCount;
            dirs = Item.TotalDirectoryCount;
            kind = Item.IsDirectory ? "文件夹" : "文件";
        }

        public void ReloadChildren()
        {
            ReloadChildren(false);
        }

        private void ReloadChildren(bool materializeLoadedChildren)
        {
            Children.Clear();
            IsLoadingChildren = false;
            AreChildRowsMaterialized = false;
            if (Item == null || !Item.IsDirectory) return;

            if (Item.ChildrenLoaded && materializeLoadedChildren)
            {
                MaterializeLoadedChildren();
                return;
            }

            if (Item.HasChildren || Item.Children.Count > 0) Children.Add(ExpandMarker.Instance);
        }

        public bool MaterializeLoadedChildren()
        {
            if (Item == null || !Item.IsDirectory || !Item.ChildrenLoaded) return false;
            if (AreChildRowsMaterialized) return false;

            Children.Clear();
            for (int i = 0; i < Item.Children.Count; i++)
            {
                Children.Add(new StorageEntryRow(Item.Children[i], Depth + 1, this, false));
            }

            if (Children.Count > 1) Children.Sort(CompareChildRowsByDisplayOrder);

            AreChildRowsMaterialized = true;
            return true;
        }

        private static int CompareChildRowsByDisplayOrder(object leftValue, object rightValue)
        {
            StorageEntryRow left = (StorageEntryRow)leftValue;
            StorageEntryRow right = (StorageEntryRow)rightValue;

            int result = right.Item.Bytes.CompareTo(left.Item.Bytes);
            if (result != 0) return result;

            result = string.Compare(left.Item.Name, right.Item.Name, StringComparison.OrdinalIgnoreCase);
            if (result != 0) return result;

            return string.Compare(left.Item.Path, right.Item.Path, StringComparison.OrdinalIgnoreCase);
        }

        public bool ReleaseChildRows()
        {
            if (Item == null || !Item.IsDirectory || !Item.ChildrenLoaded || !AreChildRowsMaterialized) return false;

            Children.Clear();
            AreChildRowsMaterialized = false;
            if (Item.Children.Count > 0) Children.Add(ExpandMarker.Instance);
            return true;
        }

        public bool ReleaseLoadedChildren()
        {
            if (Item == null || !Item.IsDirectory || !Item.ChildrenLoaded) return false;

            Children.Clear();
            AreChildRowsMaterialized = false;
            IsLoadingChildren = false;
            Item.Children.Clear();
            Item.ChildrenLoaded = false;
            if (Item.HasChildren) Children.Add(ExpandMarker.Instance);
            return true;
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
