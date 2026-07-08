using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using AiCleanVolume.Core.Domain.Settings;
using AiCleanVolume.Desktop.Presentation.Features.Settings;
using AiCleanVolume.Desktop.Presentation.Shared;


namespace AiCleanVolume.Desktop
{
    // 设置弹窗中的 AI 配置档案列表：选择 / 应用到输入框 / 删除 / 另存当前为档案。
    public sealed partial class MainWindow : AntdUI.Window
    {
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
            }
            catch (Exception ex)
            {
                Log("保存 AI 配置方案失败：" + ex.Message);
                ShowError("保存失败", ex.Message);
            }
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
            Log("已应用 AI 配置方案到界面：" + profile.Name + "。点击保存后生效。");
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
                    aiProfileListPanel.Controls.Add(AiProfileCardFactory.CreateProfileCard(aiProfileListPanel, settings.Ai.Profiles[index], index, index == selectedIndex, SelectAiProfile, ApplyAiProfileFromCard, ApplyAiProfileFromCard, DeleteAiProfile));
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
            pendingSystemPrompt = NormalizeValue(profile.SystemPrompt);
            if (aiAccessModeSelect != null) aiAccessModeSelect.SelectedValue = AiSettings.NormalizeAccessMode(profile.AccessMode);
            if (endpointInput != null) endpointInput.Text = NormalizeValue(profile.Endpoint);
            if (apiKeyInput != null) apiKeyInput.Text = NormalizeValue(profile.ApiKey);
            if (modelInput != null) modelInput.Text = NormalizeValue(profile.Model);
            if (maxSuggestionsInput != null) maxSuggestionsInput.Text = (profile.MaxSuggestions <= 0 ? 30 : profile.MaxSuggestions).ToString();
            if (modelCookieMappingsInput != null) modelCookieMappingsInput.Text = FormatModelCookieMappings(profile.ModelCookieMappings, profile.Model);
            if (aiAccessModeSelect != null) UpdateAiAccessModeUi();
            if (aiProviderPresetSelect != null && endpointInput != null && modelInput != null) SelectAiProviderPresetForSettings(endpointInput.Text, modelInput.Text);
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
