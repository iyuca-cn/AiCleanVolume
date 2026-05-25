using System;
using System.Drawing;
using System.Windows.Forms;
using AiCleanVolume.Core.Domain.Settings;
using AiCleanVolume.Desktop.Presentation.Shared;
using AiCleanVolume.Desktop.Presentation.Shared.Antd;

namespace AiCleanVolume.Desktop.Presentation.Features.Settings
{
    public sealed class AiProfilePageView : IFeaturePage
    {
        private static Color PageBackground { get { return AntdUI.Style.Db.BgLayout; } }

        public AiProfilePageView(Font font)
        {
            if (font == null) throw new ArgumentNullException("font");

            ViewPanel = CreatePageContainer();
            ViewPanel.Controls.Add(CreateAiProfileCreatePage(font));
        }

        public string PageId { get { return "ai_profile_create"; } }

        public Control View { get { return ViewPanel; } }

        public AntdUI.Panel ViewPanel { get; private set; }

        public AntdUI.Button BackButton { get; private set; }

        public AntdUI.Button SaveButton { get; private set; }

        public AntdUI.Button CancelButton { get; private set; }

        public AntdUI.Label TitleLabel { get; private set; }

        public AntdUI.Label DescLabel { get; private set; }

        public AntdUI.Input NameInput { get; private set; }

        public AntdUI.Select AccessModeSelect { get; private set; }

        public AntdUI.Input MaxSuggestionsInput { get; private set; }

        public AntdUI.Select ProviderPresetSelect { get; private set; }

        public AntdUI.Input EndpointInput { get; private set; }

        public AntdUI.Input ApiKeyInput { get; private set; }

        public AntdUI.Input ModelInput { get; private set; }

        public AntdUI.Input CookieMappingsInput { get; private set; }

        public void OnActivated()
        {
        }

        private static AntdUI.Panel CreatePageContainer()
        {
            AntdUI.Panel panel = AntdControlFactory.CreateFlatPanel();
            panel.Dock = DockStyle.Fill;
            panel.BackColor = PageBackground;
            panel.Padding = new Padding(20);
            return panel;
        }

        private Control CreateAiProfileCreatePage(Font font)
        {
            AntdUI.Panel panel = AntdControlFactory.CreateFlatPanel();
            panel.Dock = DockStyle.Fill;
            panel.BackColor = PageBackground;
            panel.Padding = new Padding(20);

            AntdUI.GridPanel pageLayout = AntdControlFactory.CreateGridPanel("fill;56:fill");
            pageLayout.Dock = DockStyle.Fill;
            pageLayout.BackColor = Color.Transparent;

            AntdUI.StackPanel scrollHost = AntdControlFactory.CreateVerticalScrollPanel();
            scrollHost.Dock = DockStyle.Fill;
            scrollHost.AutoScroll = true;
            scrollHost.BackColor = PageBackground;
            scrollHost.Padding = new Padding(0, 0, 4, 12);

            AntdUI.GridPanel content = AntdControlFactory.CreateGridPanel("88:fill;196:fill;240:fill");
            content.Dock = DockStyle.Top;
            content.BackColor = PageBackground;
            content.Height = 524;
            content.Width = Math.Max(720, scrollHost.ClientSize.Width - 8);
            scrollHost.Resize += delegate { content.Width = Math.Max(720, scrollHost.ClientSize.Width - 8); };

            AntdUI.Panel header = CreateAiProfileCreateHeader();
            Control basicSection = CreateAiProfileBasicSection(font);
            Control endpointSection = CreateAiProfileEndpointSection(font);
            header.Margin = new Padding(0, 0, 0, 12);
            basicSection.Margin = new Padding(0, 0, 0, 12);
            endpointSection.Margin = new Padding(0);

            AntdControlFactory.AddGridControl(content, header, 0);
            AntdControlFactory.AddGridControl(content, basicSection, 1);
            AntdControlFactory.AddGridControl(content, endpointSection, 2);
            scrollHost.Controls.Add(content);

            AntdUI.Panel footer = CreateAiProfileCreateFooter();
            AntdControlFactory.AddGridControl(pageLayout, scrollHost, 0);
            AntdControlFactory.AddGridControl(pageLayout, footer, 1);
            panel.Controls.Add(pageLayout);
            return panel;
        }

        private AntdUI.Panel CreateAiProfileCreateHeader()
        {
            AntdUI.Panel header = AntdControlFactory.CreateFlatPanel();
            header.Dock = DockStyle.Fill;
            header.BackColor = PageBackground;

            AntdUI.GridPanel layout = AntdControlFactory.CreateGridPanel("48 fill");
            layout.Dock = DockStyle.Fill;
            layout.BackColor = Color.Transparent;

            BackButton = new AntdUI.Button();
            BackButton.Dock = DockStyle.Fill;
            BackButton.AutoSizeMode = AntdUI.TAutoSize.None;
            BackButton.DisplayStyle = AntdUI.TButtonDisplayStyle.Image;
            BackButton.Shape = AntdUI.TShape.Circle;
            BackButton.IconSvg = "ArrowLeftOutlined";
            BackButton.Type = AntdUI.TTypeMini.Default;
            BackButton.Ghost = true;
            BackButton.BorderWidth = 1F;
            BackButton.DefaultBorderColor = AntdUI.Style.Db.BorderSecondary;
            BackButton.Margin = new Padding(0, 0, 10, 0);
            BackButton.WaveSize = 2;

            TitleLabel = CreateSectionTitle("新增 AI 配置");
            TitleLabel.Dock = DockStyle.Fill;
            DescLabel = CreateSectionDescription("填写接入参数并保存为配置卡片。");
            DescLabel.Dock = DockStyle.Fill;

            AntdUI.GridPanel textLayout = AntdControlFactory.CreateGridPanel("36:fill;30:fill");
            textLayout.Dock = DockStyle.Fill;
            AntdControlFactory.AddGridControl(textLayout, TitleLabel, 0);
            AntdControlFactory.AddGridControl(textLayout, DescLabel, 1);

            AntdControlFactory.AddGridControl(layout, BackButton, 0);
            AntdControlFactory.AddGridControl(layout, textLayout, 1);
            header.Controls.Add(layout);
            return header;
        }

        private Control CreateAiProfileBasicSection(Font font)
        {
            AntdUI.Panel body;
            AntdUI.Panel section = CreateSettingsGroupPanel("基础信息", "命名这套配置，并选择访问方式。", out body);

            AntdUI.GridPanel form = CreateTwoColumnProfileForm(2);
            NameInput = AntdControlFactory.CreateInput("例如：开发环境");
            AccessModeSelect = AntdControlFactory.CreateSettingsSelect(font);
            MaxSuggestionsInput = AntdControlFactory.CreateInput("30");

            AntdControlFactory.AddGridControl(form, CreateCaption("配置名称"), 0);
            AntdControlFactory.AddGridControl(form, NameInput, 1);
            AntdControlFactory.AddGridControl(form, CreateCaption("接入类型"), 2);
            AntdControlFactory.AddGridControl(form, AccessModeSelect, 3);
            AntdControlFactory.AddGridControl(form, CreateCaption("建议条数"), 4);
            AntdControlFactory.AddGridControl(form, MaxSuggestionsInput, 5);

            body.Controls.Add(form);
            return section;
        }

        private Control CreateAiProfileEndpointSection(Font font)
        {
            AntdUI.Panel body;
            AntdUI.Panel section = CreateSettingsGroupPanel("接口参数", "配置 OpenAI 兼容接口地址、密钥、模型和 Cookie。", out body);

            AntdUI.GridPanel form = AntdControlFactory.CreateGridPanel("44:92 fill 92 fill;44:92 fill 92 fill;44:92 fill");
            form.Dock = DockStyle.Fill;
            form.BackColor = Color.Transparent;
            ProviderPresetSelect = AntdControlFactory.CreateSettingsSelect(font);
            EndpointInput = AntdControlFactory.CreateInput("https://api.openai.com");
            ApiKeyInput = AntdControlFactory.CreateInput("sk-...");
            ModelInput = AntdControlFactory.CreateInput(AiSettings.DefaultModel);
            CookieMappingsInput = AntdControlFactory.CreateInput("直接粘贴当前模型的一整行 Cookie；也兼容 model=Cookie");
            CookieMappingsInput.Multiline = false;
            CookieMappingsInput.AutoScroll = false;

            AntdControlFactory.AddGridControl(form, CreateCaption("接口预设"), 0);
            AntdControlFactory.AddGridControl(form, ProviderPresetSelect, 1);
            AntdControlFactory.AddGridControl(form, CreateCaption("接口地址"), 2);
            AntdControlFactory.AddGridControl(form, EndpointInput, 3);
            AntdControlFactory.AddGridControl(form, CreateCaption("SK / API Key"), 4);
            AntdControlFactory.AddGridControl(form, ApiKeyInput, 5);
            AntdControlFactory.AddGridControl(form, CreateCaption("模型"), 6);
            AntdControlFactory.AddGridControl(form, ModelInput, 7);
            AddWideProfileFieldAtIndex(form, "模型 Cookie", CookieMappingsInput, 8);

            body.Controls.Add(form);
            return section;
        }

        private AntdUI.Panel CreateAiProfileCreateFooter()
        {
            AntdUI.Panel footer = AntdControlFactory.CreateFlatPanel();
            footer.Dock = DockStyle.Fill;
            footer.BackColor = PageBackground;
            footer.Padding = new Padding(0, 10, 0, 0);

            AntdUI.GridPanel layout = AntdControlFactory.CreateGridPanel("108 116");
            layout.Dock = DockStyle.Right;
            layout.BackColor = Color.Transparent;
            layout.Width = 236;

            CancelButton = AntdControlFactory.CreateSettingsActionButton("取消", AntdUI.TTypeMini.Default);
            CancelButton.Dock = DockStyle.Fill;
            CancelButton.Margin = new Padding(0, 4, 8, 4);

            SaveButton = AntdControlFactory.CreateSettingsActionButton("保存", AntdUI.TTypeMini.Primary);
            SaveButton.Dock = DockStyle.Fill;
            SaveButton.Margin = new Padding(0, 4, 0, 4);

            AntdControlFactory.AddGridControl(layout, CancelButton, 0);
            AntdControlFactory.AddGridControl(layout, SaveButton, 1);
            footer.Controls.Add(layout);
            return footer;
        }

        private static AntdUI.Panel CreateSettingsSurfacePanel(int padding)
        {
            AntdUI.Panel panel = new AntdUI.Panel();
            panel.Dock = DockStyle.Fill;
            panel.Padding = new Padding(padding);
            panel.Radius = 8;
            panel.Back = AntdUI.Style.Db.BgContainer;
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

        private static AntdUI.Label CreateSectionTitle(string text)
        {
            AntdUI.Label label = new AntdUI.Label();
            label.Dock = DockStyle.Top;
            label.Height = 34;
            label.Text = text;
            label.Font = new Font("Microsoft YaHei UI", 13.5F, FontStyle.Bold);
            label.ForeColor = AntdUI.Style.Db.Text;
            label.BackColor = Color.Transparent;
            return label;
        }

        private static AntdUI.Label CreateSectionDescription(string text)
        {
            AntdUI.Label desc = new AntdUI.Label();
            desc.Dock = DockStyle.Top;
            desc.Height = 26;
            desc.Text = text;
            desc.ForeColor = AntdUI.Style.Db.TextSecondary;
            desc.Font = new Font("Microsoft YaHei UI", 9.5F);
            desc.BackColor = Color.Transparent;
            return desc;
        }

        private static AntdUI.Label CreateCaption(string text)
        {
            return AntdControlFactory.CreateCaption(text);
        }

        private static AntdUI.GridPanel CreateTwoColumnProfileForm(int rows)
        {
            string[] rowDefinitions = new string[rows];
            for (int row = 0; row < rows; row++) rowDefinitions[row] = "44:92 fill 92 fill";
            AntdUI.GridPanel form = AntdControlFactory.CreateGridPanel(string.Join(";", rowDefinitions));
            form.Dock = DockStyle.Fill;
            form.BackColor = Color.Transparent;
            return form;
        }

        private static void AddWideProfileFieldAtIndex(AntdUI.GridPanel form, string caption, Control control, int index)
        {
            AntdUI.Label label = CreateCaption(caption);
            label.Margin = new Padding(0, 0, 8, 8);
            control.Margin = new Padding(0, 0, 0, 8);
            AntdControlFactory.AddGridControl(form, label, index);
            AntdControlFactory.AddGridControl(form, control, index + 1);
        }
    }
}
