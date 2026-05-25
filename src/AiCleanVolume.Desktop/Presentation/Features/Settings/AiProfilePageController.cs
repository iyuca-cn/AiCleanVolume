using System;
using System.Windows.Forms;
using AiCleanVolume.Desktop.Presentation.Shared;

namespace AiCleanVolume.Desktop.Presentation.Features.Settings
{
    public sealed class AiProfilePageController
    {
        public AiProfilePageController(
            IMainWindowShell shell,
            AiProfilePageView view,
            Action backRequested,
            Action cancelRequested,
            Action saveRequested,
            Action<object, AntdUI.ObjectNEventArgs> accessModeChanged,
            Action<object, AntdUI.ObjectNEventArgs> providerPresetChanged,
            EventHandler endpointOrModelChanged)
        {
            if (shell == null) throw new ArgumentNullException("shell");
            if (view == null) throw new ArgumentNullException("view");

            if (view.BackButton != null && backRequested != null) view.BackButton.Click += delegate { backRequested(); };
            if (view.CancelButton != null && cancelRequested != null) view.CancelButton.Click += delegate { cancelRequested(); };
            if (view.SaveButton != null && saveRequested != null) view.SaveButton.Click += delegate { saveRequested(); };
            if (view.AccessModeSelect != null && accessModeChanged != null) view.AccessModeSelect.SelectedValueChanged += delegate(object sender, AntdUI.ObjectNEventArgs e) { accessModeChanged(sender, e); };
            if (view.ProviderPresetSelect != null && providerPresetChanged != null) view.ProviderPresetSelect.SelectedValueChanged += delegate(object sender, AntdUI.ObjectNEventArgs e) { providerPresetChanged(sender, e); };
            if (view.EndpointInput != null && endpointOrModelChanged != null) view.EndpointInput.TextChanged += endpointOrModelChanged;
            if (view.ModelInput != null && endpointOrModelChanged != null) view.ModelInput.TextChanged += endpointOrModelChanged;
        }
    }
}
