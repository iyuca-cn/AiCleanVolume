using System.Collections.Generic;
using AiCleanVolume.Core.Models;

namespace AiCleanVolume.Core.Services
{
    public interface IScanProvider
    {
        StorageItem Scan(ScanRequest request);
    }

    public interface IAiCleanupAdvisor
    {
        IList<CleanupSuggestion> Analyze(StorageItem root, IList<CleanupCandidate> candidates, ApplicationSettings settings);
    }

    public interface IDeletionSandbox
    {
        SandboxEvaluation Evaluate(string path, SandboxSettings settings, bool processIsElevated);
    }

    public interface IDeletionService
    {
        CleanupResult Delete(CleanupSuggestion suggestion, bool useRecycleBin);
    }

    public interface IExplorerService
    {
        void OpenPath(string path, bool selectItem);
    }

    public interface IPrivilegeService
    {
        bool IsProcessElevated();
    }
}
