namespace AiCleanVolume.Core.Domain.Settings
{
    public sealed class UiSettings
    {
        public int SidebarWidth { get; set; }

        public void EnsureDefaults()
        {
            if (SidebarWidth < 0) SidebarWidth = 0;
        }
    }
}
