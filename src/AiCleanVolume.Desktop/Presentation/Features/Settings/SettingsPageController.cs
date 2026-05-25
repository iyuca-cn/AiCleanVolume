using System;
using System.Windows.Forms;
using AiCleanVolume.Desktop.Presentation.Shared;

namespace AiCleanVolume.Desktop.Presentation.Features.Settings
{
    public sealed class SettingsPageController
    {
        public SettingsPageController(
            IMainWindowShell shell,
            SettingsPageView view,
            Action saveSettingsRequested,
            Action testAiRequested,
            Action applyAiProfileRequested,
            Action addAiProfileRequested,
            Action<object, AntdUI.ObjectNEventArgs> aiAccessModeChanged,
            Action<object, AntdUI.ObjectNEventArgs> aiProviderPresetChanged,
            EventHandler endpointOrModelChanged,
            Action<object, AntdUI.BoolEventArgs> privilegedChanged,
            Action refreshAiProfileCards)
        {
            if (shell == null) throw new ArgumentNullException("shell");
            if (view == null) throw new ArgumentNullException("view");

            if (view.SaveSettingsButton != null && saveSettingsRequested != null) view.SaveSettingsButton.Click += delegate { saveSettingsRequested(); };
            if (view.TestAiSettingsButton != null && testAiRequested != null) view.TestAiSettingsButton.Click += delegate { testAiRequested(); };
            if (view.ApplyAiProfileButton != null && applyAiProfileRequested != null) view.ApplyAiProfileButton.Click += delegate { applyAiProfileRequested(); };
            if (view.AddAiProfileButton != null && addAiProfileRequested != null) view.AddAiProfileButton.Click += delegate { addAiProfileRequested(); };
            if (view.AiAccessModeSelect != null && aiAccessModeChanged != null) view.AiAccessModeSelect.SelectedValueChanged += delegate(object sender, AntdUI.ObjectNEventArgs e) { aiAccessModeChanged(sender, e); };
            if (view.AiProviderPresetSelect != null && aiProviderPresetChanged != null) view.AiProviderPresetSelect.SelectedValueChanged += delegate(object sender, AntdUI.ObjectNEventArgs e) { aiProviderPresetChanged(sender, e); };
            if (view.EndpointInput != null && endpointOrModelChanged != null) view.EndpointInput.TextChanged += endpointOrModelChanged;
            if (view.ModelInput != null && endpointOrModelChanged != null) view.ModelInput.TextChanged += endpointOrModelChanged;
            if (view.PrivilegedCheckbox != null && privilegedChanged != null) view.PrivilegedCheckbox.CheckedChanged += delegate(object sender, AntdUI.BoolEventArgs e) { privilegedChanged(sender, e); };
            if (view.AiProfileListPanel != null && refreshAiProfileCards != null) view.AiProfileListPanel.Resize += delegate { refreshAiProfileCards(); };
            if (view.SettingsScrollHost != null && refreshAiProfileCards != null) view.SettingsScrollHost.Resize += delegate { refreshAiProfileCards(); };
        }
    }
}
