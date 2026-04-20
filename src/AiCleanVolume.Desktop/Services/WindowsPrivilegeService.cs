using System.Security.Principal;
using AiCleanVolume.Core.Services;

namespace AiCleanVolume.Desktop.Services
{
    public sealed class WindowsPrivilegeService : IPrivilegeService
    {
        public bool IsProcessElevated()
        {
            WindowsIdentity identity = WindowsIdentity.GetCurrent();
            WindowsPrincipal principal = new WindowsPrincipal(identity);
            return principal.IsInRole(WindowsBuiltInRole.Administrator);
        }
    }
}
