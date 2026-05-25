using System;
using AiCleanVolume.Desktop.Presentation.Features.Settings;
using AiCleanVolume.Desktop.Presentation.Shared;

namespace AiCleanVolume.Desktop
{
    public sealed partial class MainWindow
    {
        private static string NormalizeValue(string value)
        {
            return AiSettingsText.NormalizeValue(value);
        }

        private static string NormalizeEndpoint(string endpoint)
        {
            return AiSettingsText.NormalizeEndpoint(endpoint);
        }

        private static AiSettingsPresetCatalog.AiPromptPresetOption FindAiPromptPresetByPrompt(string prompt)
        {
            return AiSettingsPresetCatalog.FindPromptPresetByPrompt(prompt);
        }
    }
}
