using AiCleanVolume.Core.Domain.Storage;

namespace AiCleanVolume.Core.Kernel.Ports
{
    public interface IScanProvider
    {
        StorageItem Scan(ScanRequest request);
    }
}
