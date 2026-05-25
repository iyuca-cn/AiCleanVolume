using AiCleanVolume.Core.Domain.Sandbox;

namespace AiCleanVolume.Core.Domain.Cleanup
{
    public sealed class CleanupSuggestion
    {
        public CleanupSuggestion()
        {
            Selected = true;
            Status = CleanupStatus.Pending;
        }

        public string Path { get; set; }
        public string Name { get; set; }
        public long Bytes { get; set; }
        public bool IsDirectory { get; set; }
        public CleanupRisk Risk { get; set; }
        public double Score { get; set; }
        public bool Selected { get; set; }
        public string Reason { get; set; }
        public string Source { get; set; }
        public CleanupStatus Status { get; set; }
        public string ErrorMessage { get; set; }
        public SandboxEvaluation Sandbox { get; set; }
    }
}
