using System;
using AiCleanVolume.Core.Domain.Storage;

namespace AiCleanVolume.Core.Domain.Settings
{
    public sealed class ScanSettings
    {
        public ScanSettings()
        {
            MinSizeMb = -1;
            PerLevelLimit = -1;
            SortMode = ScanSortMode.Allocated;
        }

        public int MinSizeMb { get; set; }
        public int PerLevelLimit { get; set; }
        public ScanSortMode SortMode { get; set; }

        public void EnsureDefaults()
        {
            if (!Enum.IsDefined(typeof(ScanSortMode), SortMode)) SortMode = ScanSortMode.Allocated;
        }
    }
}
