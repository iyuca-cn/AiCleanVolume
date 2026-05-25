using AiCleanVolume.Core.Domain.Cleanup;

namespace AiCleanVolume.Core.Kernel.Ports
{
    public interface IDeletionService
    {
        CleanupResult Delete(CleanupSuggestion suggestion, bool useRecycleBin);
    }
}
