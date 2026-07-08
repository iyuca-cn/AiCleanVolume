using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using AiCleanVolume.Core.Domain.Cleanup;
using AiCleanVolume.Core.Domain.Sandbox;
using AiCleanVolume.Core.Domain.Settings;
using AiCleanVolume.Core.Domain.Storage;
using AiCleanVolume.Core.Application.CleanupPlanning;
using AiCleanVolume.Core.Application.Deletion;
using AiCleanVolume.Core.Application.Scanning;
using AiCleanVolume.Core.Kernel.Ports;
using AiCleanVolume.Desktop.Controls;
using AiCleanVolume.Desktop.Infrastructure.Ai;
using AiCleanVolume.Desktop.Infrastructure.Scanning;
using AiCleanVolume.Desktop.Infrastructure.Settings;
using AiCleanVolume.Desktop.Infrastructure.Windows;
using AiCleanVolume.Desktop.Presentation.Features.Settings;
using AiCleanVolume.Desktop.Presentation.Shared;
using AiCleanVolume.Desktop.ViewModels;


namespace AiCleanVolume.Desktop
{
    public sealed partial class MainWindow : AntdUI.Window
    {
        // 设置项分两类：常驻隐藏控件（扫描阈值、权限同步）任何时候都灌值；仅存在于设置弹窗的控件在弹窗构建后才有值。
        private void LoadSettingsToUi()
        {
            settings.EnsureDefaults();
            settings.Sandbox.AllowedRoots = SandboxSettings.NormalizeAllowedRoots(settings.Sandbox.AllowedRoots);
            pendingSystemPrompt = settings.Ai.SystemPrompt;

            ApplyPrivilegedCheckboxState(settings.Sandbox.FullyPrivilegedMode);
            if (minSizeInput != null) minSizeInput.Text = settings.Scan.MinSizeMb.ToString();
            if (limitInput != null) limitInput.Text = settings.Scan.PerLevelLimit.ToString();
            if (suggestionMinSizeInput != null) suggestionMinSizeInput.Text = "128";
            if (suggestionLimitInput != null) suggestionLimitInput.Text = "-1";
            UpdateStorageSizeColumnTitle(settings.Scan.SortMode);
            UpdateAiStatusChip();

            if (aiEnabledSwitch != null) aiEnabledSwitch.Checked = settings.Ai.Enabled;
            if (recycleSegmented != null) recycleSegmented.SelectIndex = settings.Sandbox.UseRecycleBin ? 0 : 1;
            if (sortSegmented != null) sortSegmented.SelectIndex = settings.Scan.SortMode == ScanSortMode.Logical ? 1 : 0;
            if (aiAccessModeSelect != null) aiAccessModeSelect.SelectedValue = settings.Ai.AccessMode;
            if (endpointInput != null) endpointInput.Text = settings.Ai.Endpoint;
            if (apiKeyInput != null) apiKeyInput.Text = settings.Ai.ApiKey;
            if (modelInput != null) modelInput.Text = settings.Ai.Model;
            if (maxSuggestionsInput != null) maxSuggestionsInput.Text = settings.Ai.MaxSuggestions.ToString();
            if (modelCookieMappingsInput != null) modelCookieMappingsInput.Text = FormatModelCookieMappings(settings.Ai.ModelCookieMappings, settings.Ai.Model);
            if (allowRootsInput != null) allowRootsInput.Text = string.Join(Environment.NewLine, new List<string>(settings.Sandbox.AllowedRoots).ToArray());
            if (aiAccessModeSelect != null) UpdateAiAccessModeUi();
            PopulateAiProfiles();
            if (aiProviderPresetSelect != null) SelectAiProviderPresetForSettings(settings.Ai.Endpoint, settings.Ai.Model);
        }

        private void PopulateAiAccessModes()
        {
            AiSettingsPresetCatalog.PopulateAccessModes(aiAccessModeSelect);
        }

        private void PopulateAiProviderPresets()
        {
            AiSettingsPresetCatalog.PopulateProviderPresets(aiProviderPresetSelect);
        }

        private static void PopulateAiPromptPresets(AntdUI.Select select)
        {
            AiSettingsPresetCatalog.PopulatePromptPresets(select);
        }

        private void SelectAiProviderPresetForSettings(string endpoint, string model)
        {
            if (aiProviderPresetSelect == null) return;

            AiSettingsPresetCatalog.AiProviderPresetOption preset = AiSettingsPresetCatalog.FindProviderPreset(endpoint, model);
            syncingAiProviderPreset = true;
            try
            {
                aiProviderPresetSelect.SelectedValue = preset == null ? AiSettingsPresetCatalog.CustomProviderPresetKey : preset.Key;
            }
            finally
            {
                syncingAiProviderPreset = false;
            }
        }

        private static void SelectAiPromptPresetForPrompt(AntdUI.Select select, string prompt)
        {
            if (select == null) return;

            AiSettingsPresetCatalog.SelectPromptPresetForPrompt(select, prompt);
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
            if (string.Equals(key, AiSettingsPresetCatalog.CustomProviderPresetKey, StringComparison.OrdinalIgnoreCase)) return;

            AiSettingsPresetCatalog.AiProviderPresetOption preset = AiSettingsPresetCatalog.FindProviderPresetByKey(key);
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

        private void TestAiSettings()
        {
            try
            {
                SaveSettingsFromUi();
                settings.Ai.Enabled = IsAiConfigured(settings.Ai);
                if (aiEnabledSwitch != null) aiEnabledSwitch.Checked = settings.Ai.Enabled;
                UpdateAiStatusChip();
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

        // 仅从当前存活的控件回写；设置弹窗关闭后其控件引用被置空，对应字段沿用已持久化的值。
        private void SaveSettingsFromUi()
        {
            if (aiEnabledSwitch != null) settings.Ai.Enabled = aiEnabledSwitch.Checked;
            if (aiAccessModeSelect != null) settings.Ai.AccessMode = ResolveSelectedAiAccessMode();
            if (endpointInput != null) settings.Ai.Endpoint = endpointInput.Text.Trim();
            if (apiKeyInput != null) settings.Ai.ApiKey = apiKeyInput.Text.Trim();
            if (modelInput != null) settings.Ai.Model = modelInput.Text.Trim();
            if (maxSuggestionsInput != null) settings.Ai.MaxSuggestions = ParsePositiveInt(maxSuggestionsInput.Text, 30);
            settings.Ai.SystemPrompt = NormalizeValue(pendingSystemPrompt);
            if (modelCookieMappingsInput != null) settings.Ai.ModelCookieMappings = ParseModelCookieMappings(modelCookieMappingsInput.Text, settings.Ai.Model);
            if (recycleSegmented != null) settings.Sandbox.UseRecycleBin = recycleSegmented.SelectIndex == 0;
            settings.Sandbox.FullyPrivilegedMode = IsFullyPrivilegedChecked();
            if (allowRootsInput != null) settings.Sandbox.AllowedRoots = SandboxSettings.NormalizeAllowedRoots(ParseLines(allowRootsInput.Text));
            if (minSizeInput != null) settings.Scan.MinSizeMb = ParseInt(minSizeInput.Text, -1);
            if (limitInput != null) settings.Scan.PerLevelLimit = ParseInt(limitInput.Text, -1);
            if (sortSegmented != null) settings.Scan.SortMode = sortSegmented.SelectIndex == 1 ? ScanSortMode.Logical : ScanSortMode.Allocated;
            settings.EnsureDefaults();
        }

        private static bool IsAiConfigured(AiSettings ai)
        {
            return ai != null && !string.IsNullOrWhiteSpace(ai.Endpoint) && !string.IsNullOrWhiteSpace(ai.Model);
        }

        private string GetCurrentSystemPromptText()
        {
            string prompt = NormalizeValue(pendingSystemPrompt);
            if (string.IsNullOrWhiteSpace(prompt) && settings != null && settings.Ai != null)
            {
                prompt = NormalizeValue(settings.Ai.SystemPrompt);
            }

            return string.IsNullOrWhiteSpace(prompt) ? AiSettingsPresetCatalog.DefaultSystemPrompt : prompt;
        }

        private static IList<string> ParseLines(string text)
        {
            return AiSettingsText.ParseLines(text);
        }

        private static IList<AiModelCookieMapping> ParseModelCookieMappings(string text, string currentModel)
        {
            return AiSettingsText.ParseModelCookieMappings(text, currentModel);
        }

        private static string FormatModelCookieMappings(IEnumerable<AiModelCookieMapping> mappings, string currentModel)
        {
            return AiSettingsText.FormatModelCookieMappings(mappings, currentModel);
        }

        private static int ParsePositiveInt(string text, int fallback)
        {
            return AiSettingsText.ParsePositiveInt(text, fallback);
        }
    }
}
