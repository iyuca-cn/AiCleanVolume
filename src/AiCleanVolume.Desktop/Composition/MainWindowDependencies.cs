using System;
using AiCleanVolume.Core.Application.CleanupPlanning;
using AiCleanVolume.Core.Application.Deletion;
using AiCleanVolume.Core.Application.Scanning;
using AiCleanVolume.Core.Kernel.Ports;
using AiCleanVolume.Desktop.Infrastructure.Ai;

namespace AiCleanVolume.Desktop.Composition
{
    public sealed class MainWindowDependencies
    {
        public ISettingsStore SettingsStore { get; set; }
        public IScanProvider ScanProvider { get; set; }
        public CandidatePlanner CandidatePlanner { get; set; }
        public IAiCleanupAdvisor LocalAdvisor { get; set; }
        public Func<Action<string>, OpenAiCompatibleAdvisor> AiAdvisorFactory { get; set; }
        public CleanupDeletionWorkflow DeletionWorkflow { get; set; }
        public IExplorerService ExplorerService { get; set; }

        public OpenAiCompatibleAdvisor CreateAiAdvisor(Action<string> log)
        {
            if (AiAdvisorFactory == null) throw new InvalidOperationException("AI advisor factory is required.");
            return AiAdvisorFactory(log);
        }
    }
}
