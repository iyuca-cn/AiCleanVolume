using AiCleanVolume.Core.Domain.Cleanup;
using AiCleanVolume.Core.Domain.Sandbox;

namespace AiCleanVolume.Core.Application.Deletion
{
    public sealed class CleanupDeletionWorkflowResult
    {
        public CleanupSuggestion Suggestion { get; set; }
        public SandboxEvaluation Sandbox { get; set; }
        public CleanupResult Result { get; set; }
    }
}
