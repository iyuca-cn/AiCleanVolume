using AiCleanVolume.Core.Domain.Sandbox;
using AiCleanVolume.Core.Domain.Settings;

namespace AiCleanVolume.Core.Kernel.Ports
{
    public interface IDeletionSandbox
    {
        SandboxEvaluation Evaluate(string path, SandboxSettings settings, bool processIsElevated);
    }
}
