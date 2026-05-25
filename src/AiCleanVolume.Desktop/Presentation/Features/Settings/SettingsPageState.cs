namespace AiCleanVolume.Desktop.Presentation.Features.Settings
{
    public sealed class SettingsPageState
    {
        public SettingsPageState()
        {
            EditingAiProfileIndex = -1;
            SelectedAiProfileIndex = -1;
        }

        public bool SyncingAiProviderPreset { get; set; }
        public bool SyncingAiProfileProviderPreset { get; set; }
        public bool SyncingPrivilegeCheckboxes { get; set; }
        public int EditingAiProfileIndex { get; set; }
        public int SelectedAiProfileIndex { get; set; }
        public string PendingSystemPrompt { get; set; }
    }
}
