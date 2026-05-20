using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using AiCleanVolume.Core.Models;
using AiCleanVolume.Core.Services;
using AiCleanVolume.Desktop.Controls;
using AiCleanVolume.Desktop.Services;
using AiCleanVolume.Desktop.ViewModels;


namespace AiCleanVolume.Desktop
{
    public sealed partial class MainWindow : AntdUI.Window
    {
        private void LoadSettingsToUi()
        {
            settings.EnsureDefaults();
            aiEnabledSwitch.Checked = settings.Ai.Enabled;
            recycleSwitch.Checked = settings.Sandbox.UseRecycleBin;
            ApplyPrivilegedCheckboxState(settings.Sandbox.FullyPrivilegedMode);
            aiAccessModeSelect.SelectedValue = settings.Ai.AccessMode;
            endpointInput.Text = settings.Ai.Endpoint;
            apiKeyInput.Text = settings.Ai.ApiKey;
            modelInput.Text = settings.Ai.Model;
            maxSuggestionsInput.Text = settings.Ai.MaxSuggestions.ToString();
            systemPromptInput.Text = settings.Ai.SystemPrompt;
            modelCookieMappingsInput.Text = FormatModelCookieMappings(settings.Ai.ModelCookieMappings, settings.Ai.Model);
            UpdateAiAccessModeUi();
            PopulateAiProfiles();
            SelectAiProviderPresetForSettings(settings.Ai.Endpoint, settings.Ai.Model);
            SelectAiPromptPresetForPrompt(settings.Ai.SystemPrompt);
            minSizeInput.Text = settings.Scan.MinSizeMb.ToString();
            limitInput.Text = settings.Scan.PerLevelLimit.ToString();
            if (suggestionMinSizeInput != null) suggestionMinSizeInput.Text = "128";
            if (suggestionLimitInput != null) suggestionLimitInput.Text = "-1";
            sortSelect.SelectedValue = settings.Scan.SortMode;
            settings.Sandbox.AllowedRoots = SandboxSettings.NormalizeAllowedRoots(settings.Sandbox.AllowedRoots);
            allowRootsInput.Text = string.Join(Environment.NewLine, new List<string>(settings.Sandbox.AllowedRoots).ToArray());
        }

        private void PopulateAiAccessModes()
        {
            PopulateAiAccessModes(aiAccessModeSelect);
        }

        private static void PopulateAiAccessModes(AntdUI.Select select)
        {
            if (select == null) return;

            select.Items.Clear();
            select.Items.Add(new AntdUI.SelectItem("标准 API", AiSettings.StandardApiAccessMode));
            select.Items.Add(new AntdUI.SelectItem("2API", AiSettings.TwoApiAccessMode));
        }

        private void PopulateAiProviderPresets()
        {
            PopulateAiProviderPresets(aiProviderPresetSelect);
        }

        private static void PopulateAiProviderPresets(AntdUI.Select select)
        {
            if (select == null) return;

            select.Items.Clear();
            select.Items.Add(new AntdUI.SelectItem("自定义", CustomAiProviderPresetKey));
            for (int index = 0; index < AiProviderPresets.Length; index++)
            {
                AiProviderPreset preset = AiProviderPresets[index];
                select.Items.Add(new AntdUI.SelectItem(preset.Name, preset.Key));
            }
        }

        private void PopulateAiPromptPresets()
        {
            PopulateAiPromptPresets(aiPromptPresetSelect);
        }

        private static void PopulateAiPromptPresets(AntdUI.Select select)
        {
            if (select == null) return;

            select.Items.Clear();
            select.Items.Add(new AntdUI.SelectItem("自定义", CustomAiPromptPresetKey));
            for (int index = 0; index < AiPromptPresets.Length; index++)
            {
                AiPromptPreset preset = AiPromptPresets[index];
                select.Items.Add(new AntdUI.SelectItem(preset.Name, preset.Key));
            }
        }

        private void SelectAiProviderPresetForSettings(string endpoint, string model)
        {
            if (aiProviderPresetSelect == null) return;

            AiProviderPreset preset = FindAiProviderPreset(endpoint, model);
            syncingAiProviderPreset = true;
            try
            {
                aiProviderPresetSelect.SelectedValue = preset == null ? CustomAiProviderPresetKey : preset.Key;
            }
            finally
            {
                syncingAiProviderPreset = false;
            }
        }

        private void SelectAiPromptPresetForPrompt(string prompt)
        {
            if (aiPromptPresetSelect == null) return;

            AiPromptPreset preset = FindAiPromptPresetByPrompt(prompt);
            syncingAiPromptPreset = true;
            try
            {
                aiPromptPresetSelect.SelectedValue = preset == null ? CustomAiPromptPresetKey : preset.Key;
            }
            finally
            {
                syncingAiPromptPreset = false;
            }
        }

        private static AiProviderPreset FindAiProviderPreset(string endpoint, string model)
        {
            string normalizedEndpoint = NormalizeEndpoint(endpoint);
            string normalizedModel = NormalizeValue(model);
            if (string.IsNullOrWhiteSpace(normalizedEndpoint) || string.IsNullOrWhiteSpace(normalizedModel)) return null;

            for (int index = 0; index < AiProviderPresets.Length; index++)
            {
                AiProviderPreset preset = AiProviderPresets[index];
                if (string.Equals(NormalizeEndpoint(preset.Endpoint), normalizedEndpoint, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(NormalizeValue(preset.Model), normalizedModel, StringComparison.OrdinalIgnoreCase))
                {
                    return preset;
                }
            }

            return null;
        }

        private static AiPromptPreset FindAiPromptPreset(string key)
        {
            if (string.IsNullOrWhiteSpace(key)) return null;
            for (int index = 0; index < AiPromptPresets.Length; index++)
            {
                if (string.Equals(AiPromptPresets[index].Key, key, StringComparison.OrdinalIgnoreCase)) return AiPromptPresets[index];
            }

            return null;
        }

        private static AiPromptPreset FindAiPromptPresetByPrompt(string prompt)
        {
            string normalizedPrompt = NormalizePromptForComparison(prompt);
            if (string.IsNullOrWhiteSpace(normalizedPrompt)) return null;

            for (int index = 0; index < AiPromptPresets.Length; index++)
            {
                if (string.Equals(NormalizePromptForComparison(AiPromptPresets[index].Prompt), normalizedPrompt, StringComparison.Ordinal)) return AiPromptPresets[index];
            }

            return null;
        }

        private static string NormalizePromptForComparison(string prompt)
        {
            string normalized = (prompt ?? string.Empty).Replace("\r\n", "\n").Replace("\r", "\n").Trim();
            normalized = Regex.Replace(normalized, "[A-Za-z]\\s*盘", "{driveLabel}");
            normalized = Regex.Replace(normalized, "[A-Za-z]:\\\\", "{driveRoot}");
            normalized = normalized.Replace("当前重点分析 Windows {driveLabel}（{driveRoot}）下的候选路径。", string.Empty);
            normalized = Regex.Replace(normalized, "Windows\\s*\\{driveLabel\\}\\s*清理助手", "Windows 磁盘清理助手");
            return normalized;
        }

        private static string NormalizeEndpoint(string endpoint)
        {
            string normalized = NormalizeValue(endpoint);
            if (string.IsNullOrWhiteSpace(normalized)) return string.Empty;
            return normalized.TrimEnd('/');
        }

        private static string NormalizeValue(string value)
        {
            return (value ?? string.Empty).Trim();
        }

        private static AiProviderPreset FindAiProviderPresetByKey(string key)
        {
            if (string.IsNullOrWhiteSpace(key)) return null;
            for (int index = 0; index < AiProviderPresets.Length; index++)
            {
                if (string.Equals(AiProviderPresets[index].Key, key, StringComparison.OrdinalIgnoreCase)) return AiProviderPresets[index];
            }

            return null;
        }

        private void PrivilegedCheckbox_CheckedChanged(object sender, AntdUI.BoolEventArgs e)
        {
            if (loadingStartupUi) return;
            if (syncingPrivilegeCheckboxes) return;
            AntdUI.Checkbox source = sender as AntdUI.Checkbox;
            if (source == null) return;
            ApplyPrivilegedCheckboxState(source.Checked);
            if (settings != null && settings.Sandbox != null)
            {
                settings.Sandbox.FullyPrivilegedMode = source.Checked;
                RefreshSuggestionSandboxFromCurrentSettings();
            }
        }

        private void ApplyPrivilegedCheckboxState(bool value)
        {
            syncingPrivilegeCheckboxes = true;
            try
            {
                if (privilegedCheckbox != null) privilegedCheckbox.Checked = value;
                if (privilegedQuickCheckbox != null) privilegedQuickCheckbox.Checked = value;
            }
            finally
            {
                syncingPrivilegeCheckboxes = false;
            }
        }

        private bool IsFullyPrivilegedChecked()
        {
            if (privilegedQuickCheckbox != null) return privilegedQuickCheckbox.Checked;
            return privilegedCheckbox != null && privilegedCheckbox.Checked;
        }

        private void AiAccessModeSelect_SelectedValueChanged(object sender, AntdUI.ObjectNEventArgs e)
        {
            if (loadingStartupUi) return;
            UpdateAiAccessModeUi();
        }

        private void UpdateAiAccessModeUi()
        {
            bool twoApi = string.Equals(ResolveSelectedAiAccessMode(), AiSettings.TwoApiAccessMode, StringComparison.OrdinalIgnoreCase);
            if (apiKeyInput != null)
            {
                apiKeyInput.Enabled = !twoApi;
                apiKeyInput.PlaceholderText = twoApi ? "2API 模式不使用 API Key" : "sk-...";
            }
            if (modelCookieMappingsInput != null)
            {
                modelCookieMappingsInput.Enabled = true;
            }
        }

        private string ResolveSelectedAiAccessMode()
        {
            if (aiAccessModeSelect == null || aiAccessModeSelect.SelectedValue == null) return AiSettings.StandardApiAccessMode;
            return AiSettings.NormalizeAccessMode(aiAccessModeSelect.SelectedValue.ToString());
        }

        private void AiProviderPresetSelect_SelectedValueChanged(object sender, AntdUI.ObjectNEventArgs e)
        {
            if (loadingStartupUi) return;
            if (syncingAiProviderPreset || e.Value == null) return;

            string key = e.Value.ToString();
            if (string.Equals(key, CustomAiProviderPresetKey, StringComparison.OrdinalIgnoreCase)) return;

            AiProviderPreset preset = FindAiProviderPresetByKey(key);
            if (preset == null) return;

            syncingAiProviderPreset = true;
            try
            {
                if (endpointInput != null) endpointInput.Text = preset.Endpoint;
                if (modelInput != null) modelInput.Text = preset.Model;
            }
            finally
            {
                syncingAiProviderPreset = false;
            }
        }

        private void AiEndpointOrModelInput_TextChanged(object sender, EventArgs e)
        {
            if (loadingStartupUi) return;
            if (syncingAiProviderPreset) return;
            SelectAiProviderPresetForSettings(endpointInput == null ? null : endpointInput.Text, modelInput == null ? null : modelInput.Text);
        }

        private void SaveSettings()
        {
            try
            {
                SaveSettingsFromUi();
                SaveCurrentAiProfileAutomatic();
                settingsStore.Save(settings);
                Log("配置已保存。");
                ShowInfo("完成", "配置已保存。");
            }
            catch (Exception ex)
            {
                Log("保存配置失败：" + ex.Message);
                ShowError("保存失败", ex.Message);
            }
        }

        private void TestAiSettings()
        {
            try
            {
                SaveSettingsFromUi();
                settings.Ai.Enabled = IsAiConfigured(settings.Ai);
                aiEnabledSwitch.Checked = settings.Ai.Enabled;
                Log("AI 配置测试开始：Enabled=" + settings.Ai.Enabled + "，AccessMode=" + settings.Ai.AccessMode + "，Endpoint=" + settings.Ai.Endpoint + "，Model=" + settings.Ai.Model + "。");
            }
            catch (Exception ex)
            {
                Log("AI 配置测试准备失败：" + ex.Message);
                ShowError("测试失败", ex.Message);
                return;
            }

            string resultMessage = null;
            bool success = false;
            RunBackground("正在测试 AI 配置…", delegate
            {
                AiConnectionTestResult result = aiAdvisor.TestConnection(settings);
                success = result != null && result.Success;
                resultMessage = result == null ? "AI 配置测试失败：未返回测试结果。" : result.Message;
                LogBackground(resultMessage);
            }, delegate
            {
                ShowNotice(success ? "测试成功" : "测试失败", resultMessage ?? "AI 配置测试完成。", success ? AntdUI.TType.Success : AntdUI.TType.Warn);
            });
        }

        private void SaveSettingsFromUi()
        {
            settings.Ai.Enabled = aiEnabledSwitch.Checked;
            settings.Ai.AccessMode = ResolveSelectedAiAccessMode();
            settings.Ai.Endpoint = endpointInput.Text.Trim();
            settings.Ai.ApiKey = apiKeyInput.Text.Trim();
            settings.Ai.Model = modelInput.Text.Trim();
            settings.Ai.MaxSuggestions = ParsePositiveInt(maxSuggestionsInput.Text, 30);
            settings.Ai.SystemPrompt = systemPromptInput.Text.Trim();
            settings.Ai.ModelCookieMappings = ParseModelCookieMappings(modelCookieMappingsInput.Text, settings.Ai.Model);
            settings.Sandbox.UseRecycleBin = recycleSwitch.Checked;
            settings.Sandbox.FullyPrivilegedMode = IsFullyPrivilegedChecked();
            settings.Sandbox.AllowedRoots = SandboxSettings.NormalizeAllowedRoots(ParseLines(allowRootsInput.Text));
            settings.Scan.MinSizeMb = ParseInt(minSizeInput.Text, -1);
            settings.Scan.PerLevelLimit = ParseInt(limitInput.Text, -1);
            if (sortSelect.SelectedValue is ScanSortMode) settings.Scan.SortMode = (ScanSortMode)sortSelect.SelectedValue;
            settings.EnsureDefaults();
        }

        private static bool IsAiConfigured(AiSettings ai)
        {
            return ai != null && !string.IsNullOrWhiteSpace(ai.Endpoint) && !string.IsNullOrWhiteSpace(ai.Model);
        }

        private void AiPromptPresetSelect_SelectedValueChanged(object sender, AntdUI.ObjectNEventArgs e)
        {
            if (loadingStartupUi) return;
            if (syncingAiPromptPreset || e.Value == null) return;

            string key = e.Value.ToString();
            if (string.Equals(key, CustomAiPromptPresetKey, StringComparison.OrdinalIgnoreCase)) return;

            AiPromptPreset preset = FindAiPromptPreset(key);
            if (preset == null || systemPromptInput == null) return;

            syncingAiPromptPreset = true;
            try
            {
                systemPromptInput.Text = preset.BuildPrompt(GetPromptDriveRoot());
            }
            finally
            {
                syncingAiPromptPreset = false;
            }
        }

        private void SystemPromptInput_TextChanged(object sender, EventArgs e)
        {
            if (loadingStartupUi) return;
            if (syncingAiPromptPreset || systemPromptInput == null) return;
            SelectAiPromptPresetForPrompt(systemPromptInput.Text);
        }

        private static IList<string> ParseLines(string text)
        {
            List<string> result = new List<string>();
            string[] parts = (text ?? string.Empty).Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < parts.Length; i++)
            {
                string value = parts[i].Trim();
                if (!string.IsNullOrWhiteSpace(value)) result.Add(value);
            }
            return result;
        }

        private static IList<AiModelCookieMapping> ParseModelCookieMappings(string text, string currentModel)
        {
            List<AiModelCookieMapping> mappings = new List<AiModelCookieMapping>();
            string[] parts = (text ?? string.Empty).Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < parts.Length; i++)
            {
                string line = parts[i].Trim();
                if (string.IsNullOrWhiteSpace(line)) continue;

                int separatorIndex = line.IndexOf('=');
                string model;
                string cookie;
                if (separatorIndex > 0 && separatorIndex < line.Length - 1 && LooksLikeModelCookieMapping(line, separatorIndex))
                {
                    model = line.Substring(0, separatorIndex).Trim();
                    cookie = line.Substring(separatorIndex + 1).Trim();
                }
                else
                {
                    model = currentModel;
                    cookie = line;
                }
                if (string.IsNullOrWhiteSpace(model) || string.IsNullOrWhiteSpace(cookie)) continue;

                mappings.Add(new AiModelCookieMapping
                {
                    Model = model,
                    Cookie = cookie
                });
            }

            return AiSettings.NormalizeModelCookieMappings(mappings);
        }

        private static bool LooksLikeModelCookieMapping(string line, int separatorIndex)
        {
            string left = line.Substring(0, separatorIndex).Trim();
            if (string.IsNullOrWhiteSpace(left)) return false;
            if (left.IndexOf(';') >= 0 || left.IndexOf(' ') >= 0 || left.IndexOf('\t') >= 0) return false;
            return left.IndexOf('/') >= 0 || left.IndexOf(':') >= 0 || left.IndexOf('.') >= 0 || left.StartsWith("gpt", StringComparison.OrdinalIgnoreCase) || left.StartsWith("claude", StringComparison.OrdinalIgnoreCase);
        }

        private static string FormatModelCookieMappings(IEnumerable<AiModelCookieMapping> mappings, string currentModel)
        {
            IList<AiModelCookieMapping> normalized = AiSettings.NormalizeModelCookieMappings(mappings);
            string model = (currentModel ?? string.Empty).Trim();
            if (!string.IsNullOrWhiteSpace(model))
            {
                for (int i = normalized.Count - 1; i >= 0; i--)
                {
                    if (string.Equals(normalized[i].Model, model, StringComparison.OrdinalIgnoreCase)) return normalized[i].Cookie;
                }
            }

            List<string> lines = new List<string>();
            for (int i = 0; i < normalized.Count; i++)
            {
                lines.Add(normalized[i].Model + "=" + normalized[i].Cookie);
            }

            return string.Join(Environment.NewLine, lines.ToArray());
        }

        private static int ParsePositiveInt(string text, int fallback)
        {
            int parsed;
            return int.TryParse(text, out parsed) && parsed > 0 ? parsed : fallback;
        }
    }
}
