using AiCleanVolume.Core.Application.CleanupPlanning;
using AiCleanVolume.Core.Application.Deletion;
using AiCleanVolume.Core.Kernel.Ports;
using AiCleanVolume.Desktop.Infrastructure.Ai;
using AiCleanVolume.Desktop.Infrastructure.Scanning;
using AiCleanVolume.Desktop.Infrastructure.Settings;
using AiCleanVolume.Desktop.Infrastructure.Windows;

namespace AiCleanVolume.Desktop.Composition
{
    internal static class DesktopCompositionRoot
    {
        public static MainWindowDependencies CreateDependencies()
        {
            IAiCleanupAdvisor localAdvisor = new HeuristicCleanupAdvisor();
            IDeletionSandbox deletionSandbox = new DeletionSandbox();
            IDeletionService deletionService = new RecycleBinDeletionService();
            IPrivilegeService privilegeService = new WindowsPrivilegeService();

            return new MainWindowDependencies
            {
                SettingsStore = new JsonSettingsStore(),
                ScanProvider = new FolderSizeRankerScanProvider(),
                CandidatePlanner = new CandidatePlanner(),
                LocalAdvisor = localAdvisor,
                AiAdvisorFactory = log => new OpenAiCompatibleAdvisor(localAdvisor, log),
                DeletionWorkflow = new CleanupDeletionWorkflow(deletionSandbox, deletionService, privilegeService),
                ExplorerService = new ShellExplorerService()
            };
        }
    }
}
