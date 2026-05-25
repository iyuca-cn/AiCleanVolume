namespace AiCleanVolume.Core.Kernel.Ports
{
    public interface IPrivilegeService
    {
        bool IsProcessElevated();
    }
}
