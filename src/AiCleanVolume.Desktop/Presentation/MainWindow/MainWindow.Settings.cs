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
            pendingSystemPrompt = settings.Ai.SystemPrompt;
            modelCookieMappingsInput.Text = FormatModelCookieMappings(settings.Ai.ModelCookieMappings, settings.Ai.Model);
            UpdateAiAccessModeUi();
            PopulateAiProfiles();
            SelectAiProviderPresetForSettings(settings.Ai.Endpoint, settings.Ai.Model);
            minSizeInput.Text = settings.Scan.MinSizeMb.ToString();
            limitInput.Text = settings.Scan.PerLevelLimit.ToString();
            if (suggestionMinSizeInput != null) suggestionMinSizeInput.Text = "128";
            if (suggestionLimitInput != null) suggestionLimitInput.Text = "-1";
            UpdateStorageSizeColumnTitle(settings.Scan.SortMode);
            settings.Sandbox.AllowedRoots = SandboxSettings.NormalizeAllowedRoots(settings.Sandbox.AllowedRoots);
            allowRootsInput.Text = string.Join(Environment.NewLine, new List<string>(settings.Sandbox.AllowedRoots).ToArray());
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
            settings.Ai.SystemPrompt = NormalizeValue(pendingSystemPrompt);
            settings.Ai.ModelCookieMappings = ParseModelCookieMappings(modelCookieMappingsInput.Text, settings.Ai.Model);
            settings.Sandbox.UseRecycleBin = recycleSwitch.Checked;
            settings.Sandbox.FullyPrivilegedMode = IsFullyPrivilegedChecked();
            settings.Sandbox.AllowedRoots = SandboxSettings.NormalizeAllowedRoots(ParseLines(allowRootsInput.Text));
            settings.Scan.MinSizeMb = ParseInt(minSizeInput.Text, -1);
            settings.Scan.PerLevelLimit = ParseInt(limitInput.Text, -1);
            settings.EnsureDefaults();
        }

        private static bool IsAiConfigured(AiSettings ai)
        {
            return ai != null && !string.IsNullOrWhiteSpace(ai.Endpoint) && !string.IsNullOrWhiteSpace(ai.Model);
        }

        private void ShowSuggestionPromptEditor()
        {
            if (settings == null || settings.Ai == null) return;

            AntdUI.Panel content = CreateFlatPanel();
            content.Width = 680;
            content.Height = 402;
            content.Padding = new Padding(0, 4, 0, 0);
            content.BackColor = Color.Transparent;

            AntdUI.GridPanel form = CreateGridPanel("44:92 fill;328:92 fill");
            form.Dock = DockStyle.Fill;
            form.BackColor = Color.Transparent;

            AntdUI.Select presetSelect = CreateSettingsSelect();
            PopulateAiPromptPresets(presetSelect);
            AntdUI.Input promptInput = CreateInput("系统提示词");
            promptInput.Multiline = true;
            promptInput.AutoScroll = true;
            promptInput.MaxLength = int.MaxValue;

            bool syncingPreset = true;
            promptInput.Text = GetCurrentSystemPromptText();
            SelectAiPromptPresetForPrompt(presetSelect, promptInput.Text);
            syncingPreset = false;

            presetSelect.SelectedValueChanged += delegate(object sender, AntdUI.ObjectNEventArgs e)
            {
                if (syncingPreset || e.Value == null) return;

                string key = e.Value.ToString();
                if (string.Equals(key, AiSettingsPresetCatalog.CustomPromptPresetKey, StringComparison.OrdinalIgnoreCase)) return;

                AiSettingsPresetCatalog.AiPromptPresetOption preset = AiSettingsPresetCatalog.FindPromptPreset(key);
                if (preset == null) return;

                syncingPreset = true;
                try
                {
                    promptInput.Text = preset.BuildPrompt(GetPromptDriveRoot());
                }
                finally
                {
                    syncingPreset = false;
                }
            };

            promptInput.TextChanged += delegate
            {
                if (syncingPreset) return;
                syncingPreset = true;
                try
                {
                    SelectAiPromptPresetForPrompt(presetSelect, promptInput.Text);
                }
                finally
                {
                    syncingPreset = false;
                }
            };

            AddWideProfileField(form, "AI 预设", presetSelect, 0);
            AddWideProfileField(form, "系统提示词", promptInput, 1);
            content.Controls.Add(form);

            AntdUI.Modal.Config config = AntdUI.Modal.config(this, "AI 提示词", content, AntdUI.TType.Info);
            config.OkText = "保存";
            config.CancelText = "取消";
            config.OkType = AntdUI.TTypeMini.Primary;
            config.Width = 740;
            config.MaskClosable = false;
            config.Resizable = true;
            config.MinimumSize = new Size(640, 430);
            config.OnOk = delegate
            {
                string prompt = NormalizeValue(promptInput.Text);
                if (string.IsNullOrWhiteSpace(prompt))
                {
                    ShowWarning("提示", "系统提示词不能为空。");
                    return false;
                }

                try
                {
                    SaveSettingsFromUi();
                    pendingSystemPrompt = prompt;
                    settings.Ai.SystemPrompt = prompt;
                    settings.EnsureDefaults();
                    pendingSystemPrompt = settings.Ai.SystemPrompt;
                    settingsStore.Save(settings);
                    Log("AI 提示词已保存。");
                    return true;
                }
                catch (Exception ex)
                {
                    Log("保存 AI 提示词失败：" + ex.Message);
                    ShowError("保存失败", ex.Message);
                    return false;
                }
            };

            if (AntdUI.Modal.open(config) == DialogResult.OK)
            {
                ShowInfo("完成", "AI 提示词已保存。");
            }
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

        private static void AddWideProfileField(AntdUI.GridPanel form, string caption, Control control, int row)
        {
            AntdUI.Label label = CreateCaption(caption);
            label.Margin = new Padding(0, 0, 8, 8);
            control.Margin = new Padding(0, 0, 0, 8);
            int index = row * 2;
            AddGridControl(form, label, index);
            AddGridControl(form, control, index + 1);
        }
    }
}
