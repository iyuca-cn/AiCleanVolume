using AiCleanVolume.Core.Domain.Settings;

namespace AiCleanVolume.Core.Kernel.Ports
{
    public interface ISettingsStore
    {
        ApplicationSettings Load();
        void Save(ApplicationSettings settings);
    }
}
