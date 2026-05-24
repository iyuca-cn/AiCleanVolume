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
        private void SelectAiProfileProviderPresetForValues(string endpoint, string model)
        {
            if (aiProfileProviderPresetSelect == null) return;

            AiProviderPreset preset = FindAiProviderPreset(endpoint, model);
            syncingAiProfileProviderPreset = true;
            try
            {
                aiProfileProviderPresetSelect.SelectedValue = preset == null ? CustomAiProviderPresetKey : preset.Key;
            }
            finally
            {
                syncingAiProfileProviderPreset = false;
            }
        }

        private static string BuildAiProfileDisplayName(AiProfile profile)
        {
            if (profile == null) return string.Empty;
            string name = NormalizeValue(profile.Name);
            string endpoint = NormalizeEndpoint(profile.Endpoint);
            if (string.IsNullOrWhiteSpace(endpoint)) return name;

            Uri uri;
            string host = Uri.TryCreate(endpoint, UriKind.Absolute, out uri) ? uri.Host : endpoint;
            return string.IsNullOrWhiteSpace(host) ? name : name + " · " + host;
        }

        private void AiProfileProviderPresetSelect_SelectedValueChanged(object sender, AntdUI.ObjectNEventArgs e)
        {
            if (loadingStartupUi) return;
            if (syncingAiProfileProviderPreset || e.Value == null) return;

            string key = e.Value.ToString();
            if (string.Equals(key, CustomAiProviderPresetKey, StringComparison.OrdinalIgnoreCase)) return;

            AiProviderPreset preset = FindAiProviderPresetByKey(key);
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
                    aiProfileListPanel.Controls.Add(CreateEmptyAiProfileCard());
                    return;
                }

                int selectedIndex = ClampAiProfileIndex(selectedAiProfileIndex);
                selectedAiProfileIndex = selectedIndex;
                for (int index = 0; index < settings.Ai.Profiles.Count; index++)
                {
                    aiProfileListPanel.Controls.Add(CreateAiProfileCard(settings.Ai.Profiles[index], index, index == selectedIndex));
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

        private Control CreateEmptyAiProfileCard()
        {
            AntdUI.Panel card = CreateAiProfileCardSurface(false);
            card.Height = 88;

            AntdUI.Label label = CreateSmallMutedLabel("还没有保存过 AI 配置。填写接入参数后点击“保存为配置”，这里会生成类似接口列表的配置卡片。");
            label.Dock = DockStyle.Fill;
            label.TextAlign = ContentAlignment.MiddleCenter;
            card.Controls.Add(label);
            return card;
        }

        private Control CreateAiProfileCard(AiProfile profile, int index, bool selected)
        {
            AntdUI.Panel card = CreateAiProfileCardSurface(selected);

            AntdUI.GridPanel layout = CreateGridPanel("54 fill 122");
            layout.Dock = DockStyle.Fill;
            layout.BackColor = Color.Transparent;

            AntdUI.Panel avatarCell = CreateFlatPanel();
            avatarCell.Dock = DockStyle.Fill;
            avatarCell.BackColor = Color.Transparent;
            AntdUI.Avatar avatar = new AntdUI.Avatar();
            avatar.Width = 40;
            avatar.Height = 40;
            avatar.Left = 3;
            avatar.Top = 20;
            avatar.Text = BuildAiProfileAvatarText(profile);
            avatar.Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Bold);
            avatar.ForeColor = PrimaryColor;
            avatar.BackColor = Color.FromArgb(235, 245, 255);
            avatar.BorderWidth = 1F;
            avatar.BorderColor = Color.FromArgb(200, 225, 255);
            avatar.Radius = 18;
            avatarCell.Controls.Add(avatar);

            AntdUI.GridPanel content = CreateGridPanel("fill;fill;fill-30 24 fill");
            content.Dock = DockStyle.Fill;
            content.BackColor = Color.Transparent;

            AntdUI.GridPanel titleRow = CreateGridPanel("fill 182");
            titleRow.Dock = DockStyle.Fill;
            titleRow.BackColor = Color.Transparent;
            titleRow.Margin = Padding.Empty;
            titleRow.Padding = Padding.Empty;

            AntdUI.Label title = new AntdUI.Label();
            title.Dock = DockStyle.Fill;
            title.AutoEllipsis = true;
            title.Text = BuildAiProfileDisplayName(profile);
            title.Font = new Font("Microsoft YaHei UI", 10.5F, FontStyle.Bold);
            title.ForeColor = TextPrimaryColor;
            title.BackColor = Color.Transparent;
            title.TextAlign = ContentAlignment.MiddleLeft;
            title.Margin = new Padding(0, 3, 8, 0);

            AntdUI.FlowPanel tagRow = new AntdUI.FlowPanel();
            tagRow.Dock = DockStyle.Fill;
            tagRow.BackColor = Color.Transparent;
            tagRow.Align = AntdUI.TAlignFlow.LeftCenter;
            tagRow.Margin = Padding.Empty;
            tagRow.Padding = Padding.Empty;

            AddGridControl(titleRow, title, 0);
            tagRow.Controls.Add(CreateAiProfileTag(IsAiProfileConfigured(profile) ? "正常" : "待补全", IsAiProfileConfigured(profile) ? AntdUI.TTypeMini.Success : AntdUI.TTypeMini.Warn));
            tagRow.Controls.Add(CreateAiProfileTag(FormatAiAccessModeLabel(profile.AccessMode), AntdUI.TTypeMini.Info));
            AddGridControl(titleRow, tagRow, 1);

            AntdUI.Label endpoint = new AntdUI.Label();
            endpoint.Dock = DockStyle.Fill;
            endpoint.Text = NormalizeEndpoint(profile.Endpoint);
            endpoint.Font = new Font("Microsoft YaHei UI", 10F);
            endpoint.ForeColor = PrimaryColor;
            endpoint.BackColor = Color.Transparent;
            endpoint.TextAlign = ContentAlignment.MiddleLeft;
            endpoint.AutoEllipsis = true;

            AntdUI.Label meta = CreateSmallMutedLabel(BuildAiProfileMeta(profile));

            AddGridControl(content, titleRow, 0);
            AddGridControl(content, endpoint, 1);
            AddGridControl(content, meta, 2);

            AntdUI.GridPanel actions = CreateGridPanel("fill;fill;fill;fill;fill-fill 32 4 28 fill");
            actions.Dock = DockStyle.Fill;
            actions.BackColor = Color.Transparent;

            AntdUI.Button applyButton = CreateAiProfileCardActionButton(selected ? "已选中" : "应用", "CheckOutlined", selected);
            applyButton.Click += delegate
            {
                SelectAiProfile(index);
                ApplySelectedAiProfile();
            };

            AntdUI.GridPanel iconRow = CreateGridPanel("fill 28 6 28 fill");
            iconRow.Dock = DockStyle.Fill;
            iconRow.BackColor = Color.Transparent;
            iconRow.Margin = new Padding(8, 0, 0, 0);

            AntdUI.Button editButton = CreateAiProfileCardIconButton("EditOutlined", AntdUI.TTypeMini.Primary);
            editButton.Click += delegate { EditAiProfile(index); };

            AntdUI.Button deleteButton = CreateAiProfileCardIconButton("DeleteOutlined", AntdUI.TTypeMini.Error);
            deleteButton.Click += delegate { DeleteAiProfile(index); };

            AddGridControl(iconRow, CreateGridSpacer(), 0);
            AddGridControl(iconRow, editButton, 1);
            AddGridControl(iconRow, CreateGridSpacer(), 2);
            AddGridControl(iconRow, deleteButton, 3);
            AddGridControl(iconRow, CreateGridSpacer(), 4);

            AddGridControl(actions, CreateGridSpacer(), 0);
            AddGridControl(actions, applyButton, 1);
            AddGridControl(actions, CreateGridSpacer(), 2);
            AddGridControl(actions, iconRow, 3);
            AddGridControl(actions, CreateGridSpacer(), 4);

            AddGridControl(layout, avatarCell, 0);
            AddGridControl(layout, content, 1);
            AddGridControl(layout, actions, 2);
            card.Controls.Add(layout);

            BindAiProfileCardSelection(card, index);
            return card;
        }

        private AntdUI.Panel CreateAiProfileCardSurface(bool selected)
        {
            AntdUI.Panel card = new AntdUI.Panel();
            card.Width = ResolveAiProfileCardWidth();
            card.Height = 102;
            card.Margin = new Padding(0, 0, 0, 10);
            card.Padding = new Padding(14, 10, 14, 10);
            card.Radius = 12;
            card.Back = selected ? Color.FromArgb(240, 248, 255) : SurfaceColor;
            card.BorderWidth = 1F;
            card.BorderColor = selected ? Color.FromArgb(120, 180, 255) : BorderDefaultColor;
            card.Shadow = 6;
            card.ShadowOpacity = 0.04F;
            card.ShadowOpacityHover = 0.12F;
            card.ShadowOpacityAnimation = true;
            card.ShadowOffsetY = 2;
            return card;
        }

        private AntdUI.Button CreateAiProfileCardIconButton(string iconSvg, AntdUI.TTypeMini type)
        {
            AntdUI.Button button = new AntdUI.Button();
            button.Dock = DockStyle.Fill;
            button.AutoSizeMode = AntdUI.TAutoSize.None;
            button.DisplayStyle = AntdUI.TButtonDisplayStyle.Image;
            button.IconSvg = iconSvg;
            button.Type = type;
            button.Ghost = true;
            button.Height = 30;
            button.Radius = 8;
            button.BorderWidth = 1F;
            button.WaveSize = 2;
            button.Margin = new Padding(0, 1, 0, 1);
            return button;
        }

        private AntdUI.Button CreateAiProfileCardActionButton(string text, string iconSvg, bool selected)
        {
            AntdUI.Button button = new AntdUI.Button();
            button.Dock = DockStyle.Fill;
            button.AutoSizeMode = AntdUI.TAutoSize.None;
            button.Text = text;
            button.IconSvg = iconSvg;
            button.Type = selected ? AntdUI.TTypeMini.Default : AntdUI.TTypeMini.Primary;
            button.Ghost = selected;
            button.Height = 32;
            button.Radius = 8;
            button.BorderWidth = 1F;
            button.WaveSize = 2;
            button.Margin = new Padding(8, 0, 0, 0);
            if (selected)
            {
                button.DefaultBorderColor = Color.FromArgb(160, 200, 255);
                button.ForeColor = Color.FromArgb(22, 100, 220);
                button.BackColor = Color.FromArgb(235, 245, 255);
            }
            return button;
        }

        private static AntdUI.Tag CreateAiProfileTag(string text, AntdUI.TTypeMini type)
        {
            AntdUI.Tag tag = new AntdUI.Tag();
            tag.AutoSizeMode = AntdUI.TAutoSize.Auto;
            tag.Text = text;
            tag.Type = type;
            tag.BorderWidth = 0F;
            tag.Radius = 8;
            tag.Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Bold);
            tag.Margin = new Padding(0, 3, 6, 0);
            return tag;
        }

        private void BindAiProfileCardSelection(Control control, int index)
        {
            if (!(control is AntdUI.Button))
            {
                control.Cursor = Cursors.Hand;
                control.Click += delegate { SelectAiProfile(index); };
            }

            foreach (Control child in control.Controls)
            {
                BindAiProfileCardSelection(child, index);
            }
        }

        private void SelectAiProfile(int index)
        {
            int nextIndex = ClampAiProfileIndex(index);
            if (nextIndex < 0) return;
            bool selectionChanged = selectedAiProfileIndex != nextIndex;
            selectedAiProfileIndex = nextIndex;
            if (selectionChanged) RefreshAiProfileCards();
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
            if (aiProfileListPanel == null) return;
            int width = ResolveAiProfileCardWidth();
            foreach (Control control in aiProfileListPanel.Controls)
            {
                control.Width = width;
            }
        }

        private int ResolveAiProfileCardWidth()
        {
            if (aiProfileListPanel == null) return 640;
            int scrollBarOffset = aiProfileListPanel.ScrollBar != null && aiProfileListPanel.ScrollBar.ShowY ? aiProfileListPanel.ScrollBar.SIZE : 0;
            return Math.Max(420, aiProfileListPanel.ClientSize.Width - scrollBarOffset - 8);
        }

        private static bool IsAiProfileConfigured(AiProfile profile)
        {
            return profile != null && !string.IsNullOrWhiteSpace(profile.Endpoint) && !string.IsNullOrWhiteSpace(profile.Model);
        }

        private static string FormatAiAccessModeLabel(string accessMode)
        {
            return string.Equals(AiSettings.NormalizeAccessMode(accessMode), AiSettings.TwoApiAccessMode, StringComparison.OrdinalIgnoreCase) ? "2API" : "标准 API";
        }

        private static string BuildAiProfileMeta(AiProfile profile)
        {
            if (profile == null) return string.Empty;
            string model = string.IsNullOrWhiteSpace(profile.Model) ? "未填写模型" : profile.Model.Trim();
            int maxSuggestions = profile.MaxSuggestions <= 0 ? 30 : profile.MaxSuggestions;
            return "模型：" + model + "    建议：" + maxSuggestions.ToString() + " 条    保存：" + profile.SavedAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm");
        }

        private static string BuildAiProfileAvatarText(AiProfile profile)
        {
            string source = profile == null ? null : (string.IsNullOrWhiteSpace(profile.Name) ? profile.Model : profile.Name);
            source = NormalizeValue(source);
            if (string.IsNullOrWhiteSpace(source)) return "AI";

            string[] parts = Regex.Split(source, "[^A-Za-z0-9]+");
            string result = string.Empty;
            for (int i = 0; i < parts.Length && result.Length < 2; i++)
            {
                if (string.IsNullOrWhiteSpace(parts[i])) continue;
                result += char.ToUpperInvariant(parts[i][0]).ToString();
            }

            if (result.Length == 0)
            {
                result = source.Substring(0, Math.Min(2, source.Length)).ToUpperInvariant();
            }
            else if (result.Length == 1 && source.Length > 1)
            {
                result += char.ToUpperInvariant(source[1]).ToString();
            }

            return result.Length > 2 ? result.Substring(0, 2) : result;
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
