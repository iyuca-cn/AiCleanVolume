namespace AiCleanVolume.Core.Domain.Storage
{
    public sealed class ScanRequest
    {
        public ScanRequest()
        {
            SortMode = ScanSortMode.Allocated;
            MinSizeBytes = -1;
            PerLevelLimit = -1;
            LoadDepth = -1;
            SessionNodeId = -1;
            ChildStart = 0;
            ChildCount = 512;
        }

        public string Location { get; set; }
        public ScanSortMode SortMode { get; set; }
        public long MinSizeBytes { get; set; }
        public int PerLevelLimit { get; set; }
        public int LoadDepth { get; set; }
        public string SessionIdentity { get; set; }
        public int SessionNodeId { get; set; }
        public int ChildStart { get; set; }
        public int ChildCount { get; set; }
    }
}
