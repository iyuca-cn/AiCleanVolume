using System;
using System.Drawing;
using System.Windows.Forms;
using AiCleanVolume.Core.Domain.Settings;
using AiCleanVolume.Desktop.Presentation.Shared;
using AiCleanVolume.Desktop.Presentation.Shared.Antd;

namespace AiCleanVolume.Desktop.Presentation.Features.Settings
{
    public sealed class SettingsPageView : IFeaturePage
    {
        private static Color PageBackground { get { return AntdUI.Style.Db.BgLayout; } }

        private static Color SurfaceColor { get { return AntdUI.Style.Db.BgContainer; } }

        public SettingsPageView(Font font)
        {
            if (font == null) throw new ArgumentNullException("font");

            ViewPanel = CreatePageContainer();
            ViewPanel.Controls.Add(CreateSettingsPanel(font));
        }

        public string PageId { get { return "settings"; } }

        public Control View { get { return ViewPanel; } }

        public AntdUI.Panel ViewPanel { get; private set; }

        public AntdUI.StackPanel SettingsScrollHost { get; private set; }

        public AntdUI.GridPanel SettingsContentLayout { get; private set; }

        public AntdUI.Select AiAccessModeSelect { get; private set; }

        public AntdUI.Input EndpointInput { get; private set; }

        public AntdUI.Input ApiKeyInput { get; private set; }

        public AntdUI.Input ModelInput { get; private set; }

        public AntdUI.Input MaxSuggestionsInput { get; private set; }

        public AntdUI.Select AiProviderPresetSelect { get; private set; }

        public AntdUI.Input ModelCookieMappingsInput { get; private set; }

        public AntdUI.Input AllowRootsInput { get; private set; }

        public AntdUI.StackPanel AiProfileListPanel { get; private set; }

        public AntdUI.Button TestAiSettingsButton { get; private set; }

        public AntdUI.Button ApplyAiProfileButton { get; private set; }

        public AntdUI.Button AddAiProfileButton { get; private set; }

        public AntdUI.Button SaveSettingsButton { get; private set; }

        public AntdUI.Switch AiEnabledSwitch { get; private set; }

        public AntdUI.Switch RecycleSwitch { get; private set; }

        public AntdUI.Checkbox PrivilegedCheckbox { get; private set; }

        public void OnActivated()
        {
        }

        private static AntdUI.Panel CreatePageContainer()
        {
            AntdUI.Panel panel = AntdControlFactory.CreateFlatPanel();
            panel.Dock = DockStyle.Fill;
            panel.BackColor = PageBackground;
            panel.Padding = new Padding(0);
            return panel;
        }

        private Control CreateSettingsPanel(Font font)
        {
            AntdUI.Panel panel = AntdControlFactory.CreateFlatPanel();
            panel.Dock = DockStyle.Fill;
            panel.BackColor = PageBackground;
            panel.Padding = new Padding(0);

            AiEnabledSwitch = AntdControlFactory.CreateSettingsSwitch();
            RecycleSwitch = AntdControlFactory.CreateSettingsSwitch();
            PrivilegedCheckbox = AntdControlFactory.CreateCheckbox("启用完全权限（管理员）");
            TestAiSettingsButton = AntdControlFactory.CreateSettingsActionButton("测试 AI", AntdUI.TTypeMini.Default);
            TestAiSettingsButton.IconSvg = "SearchOutlined";
            ApplyAiProfileButton = AntdControlFactory.CreateSettingsActionButton("应用选中", AntdUI.TTypeMini.Primary);
            ApplyAiProfileButton.IconSvg = "CheckOutlined";
            AddAiProfileButton = AntdControlFactory.CreateAddAiProfileButton();
            SaveSettingsButton = AntdControlFactory.CreateSettingsActionButton("保存配置", AntdUI.TTypeMini.Primary);
            AiAccessModeSelect = AntdControlFactory.CreateSettingsSelect(font);
            EndpointInput = AntdControlFactory.CreateInput("https://api.openai.com");
            ApiKeyInput = AntdControlFactory.CreateInput("sk-...");
            ModelInput = AntdControlFactory.CreateInput(AiSettings.DefaultModel);
            MaxSuggestionsInput = AntdControlFactory.CreateInput("30");
            AiProviderPresetSelect = AntdControlFactory.CreateSettingsSelect(font);
            ModelCookieMappingsInput = AntdControlFactory.CreateInput("直接粘贴当前模型的一整行 Cookie；也兼容 model=Cookie");
            AllowRootsInput = AntdControlFactory.CreateInput("每行一个允许位置");

            ModelCookieMappingsInput.Multiline = false;
            ModelCookieMappingsInput.AutoScroll = false;
            AllowRootsInput.Multiline = true;
            AllowRootsInput.AutoScroll = true;

            SettingsScrollHost = AntdControlFactory.CreateVerticalScrollPanel();
            AntdUI.StackPanel scrollHost = SettingsScrollHost;
            scrollHost.Dock = DockStyle.Fill;
            scrollHost.AutoScroll = true;
            scrollHost.BackColor = PageBackground;
            scrollHost.Padding = new Padding(0, 0, 4, 8);

            SettingsContentLayout = AntdControlFactory.CreateGridPanel("62:fill;96:fill;520:fill;266:fill");
            AntdUI.GridPanel layout = SettingsContentLayout;
            layout.Dock = DockStyle.Top;
            layout.Height = 944;
            layout.BackColor = PageBackground;
            layout.Width = Math.Max(720, scrollHost.ClientSize.Width - 8);
            scrollHost.Resize += delegate
            {
                layout.Width = Math.Max(720, scrollHost.ClientSize.Width - 8);
            };

            Control overviewSection = CreateSettingsOverviewSection(AiEnabledSwitch, RecycleSwitch, MaxSuggestionsInput);
            Control profilesSection = CreateAiProfileSection(TestAiSettingsButton, ApplyAiProfileButton, AddAiProfileButton, PrivilegedCheckbox);
            Control sandboxSection = CreateSandboxSection();
            Control actionBar = CreateSettingsActionBar(SaveSettingsButton);
            actionBar.Margin = new Padding(0, 6, 0, 8);
            overviewSection.Margin = new Padding(0, 6, 0, 12);
            profilesSection.Margin = new Padding(0, 6, 0, 12);
            sandboxSection.Margin = new Padding(0);

            AntdControlFactory.AddGridControl(layout, actionBar, 0);
            AntdControlFactory.AddGridControl(layout, overviewSection, 1);
            AntdControlFactory.AddGridControl(layout, profilesSection, 2);
            AntdControlFactory.AddGridControl(layout, sandboxSection, 3);

            scrollHost.Controls.Add(layout);
            panel.Controls.Add(scrollHost);
            return panel;
        }

        private Control CreateSettingsActionBar(AntdUI.Button saveSettingsButton)
        {
            AntdUI.Panel panel = CreateCompactSurfacePanel(6);
            panel.Dock = DockStyle.Fill;

            AntdUI.GridPanel layout = AntdControlFactory.CreateGridPanel("fill 112");
            layout.Dock = DockStyle.Fill;
            layout.BackColor = Color.Transparent;

            AntdUI.Label hint = CreateSmallMutedLabel("AI 接入、清理策略和沙盒范围");
            saveSettingsButton.Margin = new Padding(0, 0, 0, 0);
            saveSettingsButton.Dock = DockStyle.Fill;

            AntdControlFactory.AddGridControl(layout, hint, 0);
            AntdControlFactory.AddGridControl(layout, saveSettingsButton, 1);
            panel.Controls.Add(layout);
            return panel;
        }

        private static AntdUI.Panel CreateCompactSurfacePanel(int padding)
        {
            AntdUI.Panel panel = new AntdUI.Panel();
            panel.Padding = new Padding(padding);
            panel.Radius = 8;
            panel.Back = SurfaceColor;
            panel.BorderWidth = 1F;
            panel.BorderColor = Color.FromArgb(230, 230, 230);
            panel.Shadow = 0;
            return panel;
        }

        private Control CreateSettingsOverviewSection(AntdUI.Switch aiEnabledSwitch, AntdUI.Switch recycleSwitch, AntdUI.Input maxSuggestionsInput)
        {
            AntdUI.Panel section = CreateSettingsSurfacePanel(12);

            AntdUI.GridPanel layout = AntdControlFactory.CreateGridPanel("48 78 84 78 86 104 fill");
            layout.Dock = DockStyle.Fill;
            layout.BackColor = Color.Transparent;

            maxSuggestionsInput.Margin = new Padding(0, 8, 0, 8);

            AntdControlFactory.AddGridControl(layout, CreateCaption("AI"), 0);
            AntdControlFactory.AddGridControl(layout, aiEnabledSwitch, 1);
            AntdControlFactory.AddGridControl(layout, CreateCaption("回收站"), 2);
            AntdControlFactory.AddGridControl(layout, recycleSwitch, 3);
            AntdControlFactory.AddGridControl(layout, CreateCaption("建议条数"), 4);
            AntdControlFactory.AddGridControl(layout, maxSuggestionsInput, 5);
            AntdControlFactory.AddGridControl(layout, AntdControlFactory.CreateGridSpacer(), 6);

            section.Controls.Add(layout);
            return section;
        }

        private Control CreateAiProfileSection(AntdUI.Button testAiSettingsButton, AntdUI.Button applyAiProfileButton, AntdUI.Button addAiProfileButton, AntdUI.Checkbox privilegedCheckbox)
        {
            AntdUI.Panel body;
            AntdUI.Panel section = CreateSettingsGroupPanel("AI 配置", "最近保存的接口、模型和访问方式会显示在这里。", out body);

            AntdUI.GridPanel layout = AntdControlFactory.CreateGridPanel("fill 230 108 116 46;fill-44 fill");
            layout.Dock = DockStyle.Fill;
            layout.BackColor = Color.Transparent;

            AntdUI.Label hint = CreateSmallMutedLabel("点击卡片选择配置，右侧按钮可直接应用到当前设置。");
            privilegedCheckbox.Margin = new Padding(0, 4, 8, 4);
            AntdControlFactory.AddGridControl(layout, hint, 0);
            AntdControlFactory.AddGridControl(layout, privilegedCheckbox, 1);
            AntdControlFactory.AddGridControl(layout, testAiSettingsButton, 2);
            AntdControlFactory.AddGridControl(layout, applyAiProfileButton, 3);
            AntdControlFactory.AddGridControl(layout, addAiProfileButton, 4);

            AiProfileListPanel = AntdControlFactory.CreateVerticalScrollPanel();
            AiProfileListPanel.Dock = DockStyle.Fill;
            AiProfileListPanel.BackColor = SurfaceColor;
            AiProfileListPanel.AutoScroll = true;
            AiProfileListPanel.Padding = new Padding(0);
            AiProfileListPanel.Margin = new Padding(0);

            AntdControlFactory.AddGridControl(layout, AiProfileListPanel, 5);
            body.Controls.Add(layout);
            return section;
        }

        private Control CreateSandboxSection()
        {
            AntdUI.Panel body;
            AntdUI.Panel section = CreateSettingsGroupPanel("沙盒范围", "允许位置内的路径可直接执行删除，其他位置会继续确认。", out body);

            AntdUI.GridPanel layout = AntdControlFactory.CreateGridPanel("fill;fill-26 fill");
            layout.Dock = DockStyle.Fill;
            layout.BackColor = Color.Transparent;

            AntdControlFactory.AddGridControl(layout, CreateSmallMutedLabel("允许位置"), 0);
            AntdControlFactory.AddGridControl(layout, AllowRootsInput, 1);

            body.Controls.Add(layout);
            return section;
        }

        private static AntdUI.Panel CreateSettingsSurfacePanel(int padding)
        {
            AntdUI.Panel panel = new AntdUI.Panel();
            panel.Dock = DockStyle.Fill;
            panel.Padding = new Padding(padding);
            panel.Radius = 8;
            panel.Back = SurfaceColor;
            panel.BorderWidth = 1F;
            panel.BorderColor = Color.FromArgb(230, 230, 230);
            panel.Shadow = 0;
            return panel;
        }

        private static AntdUI.Panel CreateSettingsGroupPanel(string title, string description, out AntdUI.Panel body)
        {
            AntdUI.Panel panel = CreateSettingsSurfacePanel(16);

            AntdUI.Label titleLabel = CreateSettingsGroupTitle(title);
            AntdUI.Label descLabel = CreateSmallMutedLabel(description);
            descLabel.Dock = DockStyle.Top;
            descLabel.Height = 22;

            body = AntdControlFactory.CreateFlatPanel();
            body.Dock = DockStyle.Fill;
            body.BackColor = Color.Transparent;
            body.Padding = new Padding(0, 8, 0, 0);

            panel.Controls.Add(body);
            panel.Controls.Add(descLabel);
            panel.Controls.Add(titleLabel);
            return panel;
        }

        private static AntdUI.Label CreateSettingsGroupTitle(string text)
        {
            AntdUI.Label label = new AntdUI.Label();
            label.Dock = DockStyle.Top;
            label.Height = 26;
            label.Text = text;
            label.TextAlign = ContentAlignment.MiddleLeft;
            label.Font = new Font("Segoe UI", 11F, FontStyle.Bold);
            label.ForeColor = Color.FromArgb(30, 30, 30);
            label.BackColor = Color.Transparent;
            return label;
        }

        private static AntdUI.Label CreateSmallMutedLabel(string text)
        {
            AntdUI.Label label = new AntdUI.Label();
            label.Dock = DockStyle.Fill;
            label.Text = text;
            label.TextAlign = ContentAlignment.MiddleLeft;
            label.Font = new Font("Segoe UI", 9F);
            label.ForeColor = Color.FromArgb(120, 120, 120);
            label.BackColor = Color.Transparent;
            return label;
        }

        private static AntdUI.Label CreateCaption(string text)
        {
            return AntdControlFactory.CreateCaption(text);
        }
    }
}
