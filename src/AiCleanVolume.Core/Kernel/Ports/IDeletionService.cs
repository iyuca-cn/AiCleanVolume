using AiCleanVolume.Core.Domain.Cleanup;
using AiCleanVolume.Core.Application.Deletion;

namespace AiCleanVolume.Core.Kernel.Ports
{
    public interface IDeletionService
    {
        CleanupResult Delete(CleanupSuggestion suggestion, bool useRecycleBin, DeletionProgressState progress);
    }
}
