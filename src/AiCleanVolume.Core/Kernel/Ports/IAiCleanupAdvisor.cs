using System.Collections.Generic;
using AiCleanVolume.Core.Domain.Cleanup;
using AiCleanVolume.Core.Domain.Settings;
using AiCleanVolume.Core.Domain.Storage;

namespace AiCleanVolume.Core.Kernel.Ports
{
    public interface IAiCleanupAdvisor
    {
        IList<CleanupSuggestion> Analyze(StorageItem root, IList<CleanupCandidate> candidates, ApplicationSettings settings);
    }
}
