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
        private void SelectAiProfileProviderPresetForValues(string endpoint, string model)
        {
            if (aiProfileProviderPresetSelect == null) return;

            AiSettingsPresetCatalog.AiProviderPresetOption preset = AiSettingsPresetCatalog.FindProviderPreset(endpoint, model);
            syncingAiProfileProviderPreset = true;
            try
            {
                aiProfileProviderPresetSelect.SelectedValue = preset == null ? AiSettingsPresetCatalog.CustomProviderPresetKey : preset.Key;
            }
            finally
            {
                syncingAiProfileProviderPreset = false;
            }
        }

        private void AiProfileProviderPresetSelect_SelectedValueChanged(object sender, AntdUI.ObjectNEventArgs e)
        {
            if (loadingStartupUi) return;
            if (syncingAiProfileProviderPreset || e.Value == null) return;

            string key = e.Value.ToString();
            if (string.Equals(key, AiSettingsPresetCatalog.CustomProviderPresetKey, StringComparison.OrdinalIgnoreCase)) return;

            AiSettingsPresetCatalog.AiProviderPresetOption preset = AiSettingsPresetCatalog.FindProviderPresetByKey(key);
            if (preset == null) return;

            syncingAiProfileProviderPreset = true;
            try
            {
                if (aiProfileEndpointInput != null) aiProfileEndpointInput.Text = preset.Endpoint;
                if (aiProfileModelInput != null) aiProfileModelInput.Text = preset.Model;
            }
            finally
            {
                syncingAiProfileProviderPreset = false;
            }
        }

        private void AiProfileEndpointOrModelInput_TextChanged(object sender, EventArgs e)
        {
            if (loadingStartupUi) return;
            if (syncingAiProfileProviderPreset) return;
            SelectAiProfileProviderPresetForValues(aiProfileEndpointInput == null ? null : aiProfileEndpointInput.Text, aiProfileModelInput == null ? null : aiProfileModelInput.Text);
        }

        private void AiProfileAccessModeSelect_SelectedValueChanged(object sender, AntdUI.ObjectNEventArgs e)
        {
            UpdateAiProfileAccessModeUi();
        }

        private string ResolveSelectedAiProfileAccessMode()
        {
            if (aiProfileAccessModeSelect == null || aiProfileAccessModeSelect.SelectedValue == null) return AiSettings.StandardApiAccessMode;
            return AiSettings.NormalizeAccessMode(aiProfileAccessModeSelect.SelectedValue.ToString());
        }

        private void UpdateAiProfileAccessModeUi()
        {
            bool twoApi = string.Equals(ResolveSelectedAiProfileAccessMode(), AiSettings.TwoApiAccessMode, StringComparison.OrdinalIgnoreCase);
            if (aiProfileApiKeyInput != null)
            {
                aiProfileApiKeyInput.Enabled = !twoApi;
                aiProfileApiKeyInput.PlaceholderText = twoApi ? "2API 模式不使用 API Key" : "sk-...";
            }
            if (aiProfileCookieMappingsInput != null)
            {
                aiProfileCookieMappingsInput.Enabled = true;
            }
        }

        private void PopulateAiProfiles()
        {
            PopulateAiProfiles(selectedAiProfileIndex);
        }

        private void PopulateAiProfiles(int preferredIndex)
        {
            if (settings == null || settings.Ai == null)
            {
                selectedAiProfileIndex = -1;
                RefreshAiProfileCards();
                return;
            }

            settings.Ai.Profiles = AiSettings.NormalizeProfiles(settings.Ai.Profiles);
            if (settings.Ai.Profiles.Count == 0)
            {
                selectedAiProfileIndex = -1;
                RefreshAiProfileCards();
                return;
            }

            selectedAiProfileIndex = ClampAiProfileIndex(preferredIndex);
            RefreshAiProfileCards();
        }

        private void SaveCurrentAiProfileAutomatic()
        {
            AiProfile profile = CreateCurrentAiProfile(null);
            profile.Name = AiSettings.BuildProfileAutoName(profile.Model, profile.SavedAt);
            UpsertAiProfile(profile, false);
            PopulateAiProfiles(0);
        }

        private void OpenAiProfileCreatePage()
        {
            editingAiProfileIndex = -1;
            InitializeAiProfilePageValues();
            UpdateAiProfilePageHeader(false);
            SetActivePage(PageAiProfileCreate);
        }

        private void UpdateAiProfilePageHeader(bool editing)
        {
            if (aiProfilePageTitle != null) aiProfilePageTitle.Text = editing ? "编辑 AI 配置" : "新增 AI 配置";
            if (aiProfilePageDesc != null) aiProfilePageDesc.Text = editing ? "修改接入参数，保存后会更新原有的配置卡片。" : "填写接入参数并保存为配置卡片。";
            if (saveAiProfilePageButton != null) saveAiProfilePageButton.Text = editing ? "更新" : "保存";
        }

        private void InitializeAiProfilePageValues()
        {
            string model = settings == null || settings.Ai == null ? AiSettings.DefaultModel : settings.Ai.Model;
            aiProfileNameInput.Text = AiSettings.BuildProfileAutoName(model, DateTime.Now);
            aiProfileAccessModeSelect.SelectedValue = settings == null || settings.Ai == null ? AiSettings.StandardApiAccessMode : settings.Ai.AccessMode;
            aiProfileEndpointInput.Text = settings == null || settings.Ai == null ? string.Empty : NormalizeValue(settings.Ai.Endpoint);
            aiProfileApiKeyInput.Text = settings == null || settings.Ai == null ? string.Empty : NormalizeValue(settings.Ai.ApiKey);
            aiProfileModelInput.Text = string.IsNullOrWhiteSpace(model) ? AiSettings.DefaultModel : NormalizeValue(model);
            aiProfileMaxSuggestionsInput.Text = settings == null || settings.Ai == null ? "30" : settings.Ai.MaxSuggestions.ToString();
            aiProfileCookieMappingsInput.Text = settings == null || settings.Ai == null ? string.Empty : FormatModelCookieMappings(settings.Ai.ModelCookieMappings, settings.Ai.Model);
            UpdateAiProfileAccessModeUi();
            SelectAiProfileProviderPresetForValues(aiProfileEndpointInput.Text, aiProfileModelInput.Text);
        }

        private void SaveCurrentAiProfileWithPrompt()
        {
            try
            {
                SaveSettingsFromUi();
                string defaultName = AiSettings.BuildProfileAutoName(settings.Ai.Model, DateTime.Now);
                string name = PromptForAiProfileName(defaultName);
                if (string.IsNullOrWhiteSpace(name)) return;

                AiProfile profile = CreateCurrentAiProfile(name);
                UpsertAiProfile(profile, true);
                settingsStore.Save(settings);
                PopulateAiProfiles(0);
                Log("AI 配置方案已保存：" + profile.Name + "。");
                ShowInfo("完成", "AI 配置方案已保存。");
            }
            catch (Exception ex)
            {
                Log("保存 AI 配置方案失败：" + ex.Message);
                ShowError("保存失败", ex.Message);
            }
        }

        private void SaveAiProfileFromPage()
        {
            try
            {
                AiProfile profile = CreateAiProfileFromPage();
                bool editing = editingAiProfileIndex >= 0 && settings.Ai.Profiles != null && editingAiProfileIndex < settings.Ai.Profiles.Count;
                int targetIndex = editing ? editingAiProfileIndex : -1;
                InsertOrReplaceAiProfile(profile, targetIndex);
                settingsStore.Save(settings);
                int selectedIndex = editing ? targetIndex : 0;
                editingAiProfileIndex = -1;
                SetActivePage(PageSettings);
                PopulateAiProfiles(selectedIndex);
                ResetAiProfileListScroll();
                RefreshAiProfileListLayout();
                string verb = editing ? "已更新" : "已新增";
                Log("AI 配置方案" + verb + "：" + profile.Name + "。");
                ShowInfo("完成", "AI 配置方案" + verb + "。");
            }
            catch (Exception ex)
            {
                Log("保存 AI 配置方案失败：" + ex.Message);
                ShowError("保存失败", ex.Message);
            }
        }

        private AiProfile CreateAiProfileFromPage()
        {
            DateTime savedAt = DateTime.Now;
            string name = aiProfileNameInput == null ? null : NormalizeValue(aiProfileNameInput.Text);
            string model = aiProfileModelInput == null ? null : NormalizeValue(aiProfileModelInput.Text);

            AiProfile profile = new AiProfile
            {
                Name = name,
                SavedAt = savedAt,
                AccessMode = ResolveSelectedAiProfileAccessMode(),
                Endpoint = aiProfileEndpointInput == null ? string.Empty : NormalizeValue(aiProfileEndpointInput.Text),
                ApiKey = aiProfileApiKeyInput == null ? string.Empty : NormalizeValue(aiProfileApiKeyInput.Text),
                Model = model,
                MaxSuggestions = ParsePositiveInt(aiProfileMaxSuggestionsInput == null ? null : aiProfileMaxSuggestionsInput.Text, 30),
                SystemPrompt = ResolveAiProfilePageSystemPrompt(),
                ModelCookieMappings = new List<AiModelCookieMapping>()
            };

            if (string.IsNullOrWhiteSpace(profile.Endpoint)) throw new InvalidOperationException("请填写接口地址。");
            if (string.IsNullOrWhiteSpace(profile.Model)) throw new InvalidOperationException("请填写模型。");

            IList<AiModelCookieMapping> mappings = ParseModelCookieMappings(aiProfileCookieMappingsInput == null ? null : aiProfileCookieMappingsInput.Text, profile.Model);
            for (int index = 0; index < mappings.Count; index++)
            {
                profile.ModelCookieMappings.Add(new AiModelCookieMapping
                {
                    Model = mappings[index].Model,
                    Cookie = mappings[index].Cookie
                });
            }

            if (string.IsNullOrWhiteSpace(profile.Name)) profile.Name = AiSettings.BuildProfileAutoName(profile.Model, profile.SavedAt);
            return profile;
        }

        private string ResolveAiProfilePageSystemPrompt()
        {
            if (editingAiProfileIndex >= 0 && settings != null && settings.Ai != null && settings.Ai.Profiles != null && editingAiProfileIndex < settings.Ai.Profiles.Count)
            {
                string profilePrompt = NormalizeValue(settings.Ai.Profiles[editingAiProfileIndex].SystemPrompt);
                return string.IsNullOrWhiteSpace(profilePrompt) ? GetCurrentSystemPromptText() : profilePrompt;
            }

            return GetCurrentSystemPromptText();
        }

        private void CancelAiProfileCreatePage()
        {
            editingAiProfileIndex = -1;
            SetActivePage(PageSettings);
        }

        private void ApplySelectedAiProfile()
        {
            AiProfile profile = ResolveSelectedAiProfile();
            if (profile == null)
            {
                ShowInfo("提示", "暂无可应用的 AI 历史配置。");
                return;
            }

            ApplyAiProfileToUi(profile);
            Log("已应用 AI 配置方案到界面：" + profile.Name + "。点击保存配置后生效。");
        }

        private AiProfile ResolveSelectedAiProfile()
        {
            if (settings == null || settings.Ai == null || settings.Ai.Profiles == null) return null;
            int index = ClampAiProfileIndex(selectedAiProfileIndex);
            if (index < 0 || index >= settings.Ai.Profiles.Count) return null;
            return settings.Ai.Profiles[index];
        }

        private void RefreshAiProfileCards()
        {
            if (aiProfileListPanel == null) return;

            aiProfileListPanel.SuspendLayout();
            try
            {
                aiProfileListPanel.Controls.Clear();
                if (settings == null || settings.Ai == null || settings.Ai.Profiles == null || settings.Ai.Profiles.Count == 0)
                {
                    selectedAiProfileIndex = -1;
                    aiProfileListPanel.Controls.Add(AiProfileCardFactory.CreateEmptyCard(aiProfileListPanel));
                    return;
                }

                int selectedIndex = ClampAiProfileIndex(selectedAiProfileIndex);
                selectedAiProfileIndex = selectedIndex;
                for (int index = 0; index < settings.Ai.Profiles.Count; index++)
                {
                    aiProfileListPanel.Controls.Add(AiProfileCardFactory.CreateProfileCard(aiProfileListPanel, settings.Ai.Profiles[index], index, index == selectedIndex, SelectAiProfile, ApplyAiProfileFromCard, EditAiProfile, DeleteAiProfile));
                }
            }
            finally
            {
                aiProfileListPanel.ResumeLayout();
                RefreshAiProfileListLayout();
            }
        }

        private void ResetAiProfileListScroll()
        {
            if (aiProfileListPanel == null || aiProfileListPanel.ScrollBar == null) return;
            aiProfileListPanel.ScrollBar.ValueY = 0;
        }

        private void RefreshAiProfileListLayout()
        {
            if (aiProfileListPanel == null) return;
            ResizeAiProfileCards();
            aiProfileListPanel.PerformLayout();
            aiProfileListPanel.Invalidate(true);
        }

        private void SelectAiProfile(int index)
        {
            int nextIndex = ClampAiProfileIndex(index);
            if (nextIndex < 0) return;
            bool selectionChanged = selectedAiProfileIndex != nextIndex;
            selectedAiProfileIndex = nextIndex;
            if (selectionChanged) RefreshAiProfileCards();
        }

        private void ApplyAiProfileFromCard(int index)
        {
            SelectAiProfile(index);
            ApplySelectedAiProfile();
        }

        private int ClampAiProfileIndex(int index)
        {
            if (settings == null || settings.Ai == null || settings.Ai.Profiles == null || settings.Ai.Profiles.Count == 0) return -1;
            if (index < 0) return 0;
            if (index >= settings.Ai.Profiles.Count) return settings.Ai.Profiles.Count - 1;
            return index;
        }

        private void ResizeAiProfileCards()
        {
            AiProfileCardFactory.ResizeCards(aiProfileListPanel);
        }

        private AiProfile CreateCurrentAiProfile(string name)
        {
            AiProfile profile = new AiProfile
            {
                Name = NormalizeValue(name),
                SavedAt = DateTime.Now,
                AccessMode = settings.Ai.AccessMode,
                Endpoint = settings.Ai.Endpoint,
                ApiKey = settings.Ai.ApiKey,
                Model = settings.Ai.Model,
                MaxSuggestions = settings.Ai.MaxSuggestions,
                SystemPrompt = settings.Ai.SystemPrompt,
                ModelCookieMappings = new List<AiModelCookieMapping>()
            };

            IList<AiModelCookieMapping> mappings = AiSettings.NormalizeModelCookieMappings(settings.Ai.ModelCookieMappings);
            for (int i = 0; i < mappings.Count; i++)
            {
                profile.ModelCookieMappings.Add(new AiModelCookieMapping
                {
                    Model = mappings[i].Model,
                    Cookie = mappings[i].Cookie
                });
            }

            if (string.IsNullOrWhiteSpace(profile.Name)) profile.Name = AiSettings.BuildProfileAutoName(profile.Model, profile.SavedAt);
            return profile;
        }

        private void UpsertAiProfile(AiProfile profile, bool matchByName)
        {
            InsertOrReplaceAiProfile(profile, -1);
        }

        private void InsertOrReplaceAiProfile(AiProfile profile, int replaceIndex)
        {
            if (profile == null) return;
            if (settings.Ai.Profiles == null) settings.Ai.Profiles = new List<AiProfile>();

            List<AiProfile> profiles = new List<AiProfile>(AiSettings.NormalizeProfiles(settings.Ai.Profiles));

            if (replaceIndex >= 0 && replaceIndex < profiles.Count)
            {
                profiles[replaceIndex] = profile.Clone();
            }
            else
            {
                profiles.Insert(0, profile.Clone());
            }

            while (profiles.Count > 10) profiles.RemoveAt(profiles.Count - 1);
            settings.Ai.Profiles = profiles;
        }

        private void ApplyAiProfileToUi(AiProfile profile)
        {
            if (profile == null) return;
            aiAccessModeSelect.SelectedValue = AiSettings.NormalizeAccessMode(profile.AccessMode);
            endpointInput.Text = NormalizeValue(profile.Endpoint);
            apiKeyInput.Text = NormalizeValue(profile.ApiKey);
            modelInput.Text = NormalizeValue(profile.Model);
            maxSuggestionsInput.Text = (profile.MaxSuggestions <= 0 ? 30 : profile.MaxSuggestions).ToString();
            pendingSystemPrompt = NormalizeValue(profile.SystemPrompt);
            modelCookieMappingsInput.Text = FormatModelCookieMappings(profile.ModelCookieMappings, profile.Model);
            UpdateAiAccessModeUi();
            SelectAiProviderPresetForSettings(endpointInput.Text, modelInput.Text);
        }

        private string PromptForAiProfileName(string defaultName)
        {
            AntdUI.Panel content = CreateFlatPanel();
            content.Width = 420;
            content.Height = 72;
            content.Padding = new Padding(0, 4, 0, 0);
            content.BackColor = Color.Transparent;

            AntdUI.GridPanel layout = CreateGridPanel("78 fill");
            layout.Dock = DockStyle.Fill;
            layout.BackColor = Color.Transparent;

            AntdUI.Label label = CreateCaption("配置名称");
            AntdUI.Input input = CreateInput("例如：开发环境");
            input.Text = defaultName ?? string.Empty;
            AddGridControl(layout, label, 0);
            AddGridControl(layout, input, 1);
            content.Controls.Add(layout);

            AntdUI.Modal.Config config = AntdUI.Modal.config(this, "保存 AI 配置方案", content, AntdUI.TType.Info);
            config.OkText = "保存";
            config.CancelText = "取消";
            config.OkType = AntdUI.TTypeMini.Primary;
            config.Width = 480;
            config.MaskClosable = false;
            return AntdUI.Modal.open(config) == DialogResult.OK ? NormalizeValue(input.Text) : null;
        }

        private void EditAiProfile(int index)
        {
            if (settings == null || settings.Ai == null || settings.Ai.Profiles == null) return;
            if (index < 0 || index >= settings.Ai.Profiles.Count) return;

            AiProfile profile = settings.Ai.Profiles[index];
            editingAiProfileIndex = index;
            LoadAiProfilePageValues(profile);
            UpdateAiProfilePageHeader(true);
            SetActivePage(PageAiProfileCreate);
        }

        private void LoadAiProfilePageValues(AiProfile profile)
        {
            if (profile == null) return;
            aiProfileNameInput.Text = NormalizeValue(profile.Name);
            aiProfileAccessModeSelect.SelectedValue = AiSettings.NormalizeAccessMode(profile.AccessMode);
            aiProfileEndpointInput.Text = NormalizeValue(profile.Endpoint);
            aiProfileApiKeyInput.Text = NormalizeValue(profile.ApiKey);
            aiProfileModelInput.Text = NormalizeValue(profile.Model);
            aiProfileMaxSuggestionsInput.Text = (profile.MaxSuggestions <= 0 ? 30 : profile.MaxSuggestions).ToString();
            aiProfileCookieMappingsInput.Text = FormatModelCookieMappings(profile.ModelCookieMappings, profile.Model);
            UpdateAiProfileAccessModeUi();
            SelectAiProfileProviderPresetForValues(aiProfileEndpointInput.Text, aiProfileModelInput.Text);
        }

        private void DeleteAiProfile(int index)
        {
            if (settings == null || settings.Ai == null || settings.Ai.Profiles == null) return;
            if (index < 0 || index >= settings.Ai.Profiles.Count) return;

            AiProfile profile = settings.Ai.Profiles[index];
            string name = string.IsNullOrWhiteSpace(profile.Name) ? "未命名配置" : profile.Name;

            AntdUI.Modal.Config config = AntdUI.Modal.config(this, "确认删除", "确定要删除配置「" + name + "」吗？此操作不可撤销。", AntdUI.TType.Warn);
            config.OkText = "删除";
            config.CancelText = "取消";
            config.OkType = AntdUI.TTypeMini.Error;
            config.MaskClosable = false;
            if (AntdUI.Modal.open(config) != DialogResult.OK) return;

            List<AiProfile> profiles = new List<AiProfile>(settings.Ai.Profiles);
            profiles.RemoveAt(index);
            settings.Ai.Profiles = profiles;
            settingsStore.Save(settings);
            int preferredIndex = selectedAiProfileIndex;
            if (index < selectedAiProfileIndex)
            {
                preferredIndex = selectedAiProfileIndex - 1;
            }
            else if (index == selectedAiProfileIndex)
            {
                preferredIndex = Math.Min(index, profiles.Count - 1);
            }
            PopulateAiProfiles(preferredIndex);
            Log("已删除 AI 配置方案：" + name + "。");
        }
    }
}
