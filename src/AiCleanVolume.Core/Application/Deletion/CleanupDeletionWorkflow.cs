using AiCleanVolume.Core.Domain.Cleanup;
using AiCleanVolume.Core.Domain.Sandbox;
using AiCleanVolume.Core.Domain.Settings;
using AiCleanVolume.Core.Kernel.Ports;

namespace AiCleanVolume.Core.Application.Deletion
{
    public sealed class CleanupDeletionWorkflow
    {
        private readonly IDeletionSandbox sandbox;
        private readonly IDeletionService deletionService;
        private readonly IPrivilegeService privilegeService;

        public CleanupDeletionWorkflow(IDeletionSandbox sandbox, IDeletionService deletionService, IPrivilegeService privilegeService)
        {
            this.sandbox = sandbox;
            this.deletionService = deletionService;
            this.privilegeService = privilegeService;
        }

        public SandboxEvaluation Evaluate(string path, SandboxSettings settings)
        {
            return sandbox.Evaluate(path, settings, privilegeService.IsProcessElevated());
        }

        public CleanupDeletionWorkflowResult Delete(CleanupSuggestion suggestion, SandboxSettings settings)
        {
            if (suggestion != null)
            {
                suggestion.Sandbox = Evaluate(suggestion.Path, settings);
            }

            bool useRecycleBin = settings != null && settings.UseRecycleBin;
            CleanupResult result = deletionService.Delete(suggestion, useRecycleBin);
            return new CleanupDeletionWorkflowResult
            {
                Suggestion = suggestion,
                Sandbox = suggestion == null ? null : suggestion.Sandbox,
                Result = result
            };
        }
    }
}
