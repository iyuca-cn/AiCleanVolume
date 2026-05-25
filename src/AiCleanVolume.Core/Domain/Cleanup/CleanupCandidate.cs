namespace AiCleanVolume.Core.Domain.Cleanup
{
    public sealed class CleanupCandidate
    {
        public string Path { get; set; }
        public string Name { get; set; }
        public long Bytes { get; set; }
        public bool IsDirectory { get; set; }
        public CleanupRisk Risk { get; set; }
        public string ReasonHint { get; set; }
        public string Source { get; set; }
    }
}
