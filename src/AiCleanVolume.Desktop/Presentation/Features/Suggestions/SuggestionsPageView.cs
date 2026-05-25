using System;
using System.Drawing;
using System.Windows.Forms;
using AiCleanVolume.Desktop.Presentation.Shared;
using AiCleanVolume.Desktop.Presentation.Shared.Antd;

namespace AiCleanVolume.Desktop.Presentation.Features.Suggestions
{
    public sealed class SuggestionsPageView : IFeaturePage
    {
        private static Color SurfaceColor { get { return AntdUI.Style.Db.BgContainer; } }

        public SuggestionsPageView(Font font)
        {
            if (font == null) throw new ArgumentNullException("font");

            Panel = CreatePageContainer();
            Panel.Controls.Add(CreateSuggestionPanel(font));
        }

        public string PageId
        {
            get { return "suggestions"; }
        }

        public Control View
        {
            get { return Panel; }
        }

        public AntdUI.Panel Panel { get; private set; }

        public AntdUI.Select DriveSelect { get; private set; }

        public AntdUI.Input MinSizeInput { get; private set; }

        public AntdUI.Input LimitInput { get; private set; }

        public AntdUI.Checkbox PrivilegedQuickCheckbox { get; private set; }

        public AntdUI.Button PromptButton { get; private set; }

        public AntdUI.Button SelectAllButton { get; private set; }

        public AntdUI.Button ClearAllButton { get; private set; }

        public AntdUI.Button InvertButton { get; private set; }

        public AntdUI.Button RegularCleanButton { get; private set; }

        public AntdUI.Button SuperCleanButton { get; private set; }

        public AntdUI.Button AnalyzeButton { get; private set; }

        public AntdUI.Button DeleteButton { get; private set; }

        public AntdUI.Table SuggestionTable { get; private set; }

        public void OnActivated()
        {
        }

        private static AntdUI.Panel CreatePageContainer()
        {
            AntdUI.Panel page = AntdControlFactory.CreateFlatPanel();
            page.Dock = DockStyle.Fill;
            page.BackColor = AntdUI.Style.Db.BgLayout;
            return page;
        }

        private Control CreateSuggestionPanel(Font font)
        {
            AntdUI.Panel panel = CreateCompactSurfacePanel(0);
            panel.Dock = DockStyle.Fill;

            AntdUI.Panel toolbarHost = AntdControlFactory.CreateFlatPanel();
            toolbarHost.Dock = DockStyle.Top;
            toolbarHost.Height = 94;
            toolbarHost.Padding = new Padding(8, 8, 8, 6);
            toolbarHost.BackColor = Color.Transparent;

            InvertButton = AntdControlFactory.CreateSuggestionActionButton("反选", AntdUI.TTypeMini.Default);
            ClearAllButton = AntdControlFactory.CreateSuggestionActionButton("全不选", AntdUI.TTypeMini.Default);
            SelectAllButton = AntdControlFactory.CreateSuggestionActionButton("全选", AntdUI.TTypeMini.Primary);

            PromptButton = AntdControlFactory.CreateToolbarActionButton("提示词", AntdUI.TTypeMini.Default);
            PromptButton.IconSvg = "EditOutlined";
            RegularCleanButton = AntdControlFactory.CreateToolbarActionButton("常规清理", AntdUI.TTypeMini.Primary);
            SuperCleanButton = AntdControlFactory.CreateToolbarActionButton("超级清理", AntdUI.TTypeMini.Warn);
            AnalyzeButton = AntdControlFactory.CreateToolbarActionButton("AI 识别", AntdUI.TTypeMini.Success);
            DeleteButton = AntdControlFactory.CreateToolbarActionButton("删除勾选", AntdUI.TTypeMini.Error);

            PrivilegedQuickCheckbox = AntdControlFactory.CreateCheckbox("完全权限模式（仅管理员运行时生效）");

            AntdUI.GridPanel topRow = AntdControlFactory.CreateGridPanel("42 160 120 78 76 78 fill");
            topRow.Dock = DockStyle.Fill;
            topRow.BackColor = Color.Transparent;

            DriveSelect = AntdControlFactory.CreateSelect(font);
            DriveSelect.ListAutoWidth = true;
            MinSizeInput = AntdControlFactory.CreateInput("最小值（单位MB）");
            MinSizeInput.Text = "128";
            LimitInput = AntdControlFactory.CreateInput("数量限制，-1 不限");
            LimitInput.Text = "-1";

            AntdControlFactory.AddGridControl(topRow, CreateToolbarCaption("盘符:"), 0);
            AntdControlFactory.AddGridControl(topRow, DriveSelect, 1);
            AntdControlFactory.AddGridControl(topRow, CreateToolbarCaption("最小 MB:"), 2);
            AntdControlFactory.AddGridControl(topRow, MinSizeInput, 3);
            AntdControlFactory.AddGridControl(topRow, CreateToolbarCaption("数量:"), 4);
            AntdControlFactory.AddGridControl(topRow, LimitInput, 5);
            AntdControlFactory.AddGridControl(topRow, PrivilegedQuickCheckbox, 6);

            AntdUI.GridPanel actionRow = AntdControlFactory.CreateGridPanel("104 104 104 104 112 fill 74 74 74");
            actionRow.Dock = DockStyle.Fill;
            actionRow.BackColor = Color.Transparent;
            RegularCleanButton.Margin = new Padding(0, 2, 8, 2);
            SuperCleanButton.Margin = new Padding(0, 2, 8, 2);
            AnalyzeButton.Margin = new Padding(0, 2, 8, 2);
            PromptButton.Margin = new Padding(0, 2, 8, 2);
            DeleteButton.Margin = new Padding(0, 2, 8, 2);
            SelectAllButton.Margin = new Padding(0, 4, 8, 4);
            ClearAllButton.Margin = new Padding(0, 4, 8, 4);
            InvertButton.Margin = new Padding(0, 4, 0, 4);
            AntdControlFactory.AddGridControl(actionRow, RegularCleanButton, 0);
            AntdControlFactory.AddGridControl(actionRow, SuperCleanButton, 1);
            AntdControlFactory.AddGridControl(actionRow, AnalyzeButton, 2);
            AntdControlFactory.AddGridControl(actionRow, PromptButton, 3);
            AntdControlFactory.AddGridControl(actionRow, DeleteButton, 4);
            AntdControlFactory.AddGridControl(actionRow, AntdControlFactory.CreateGridSpacer(), 5);
            AntdControlFactory.AddGridControl(actionRow, SelectAllButton, 6);
            AntdControlFactory.AddGridControl(actionRow, ClearAllButton, 7);
            AntdControlFactory.AddGridControl(actionRow, InvertButton, 8);

            AntdUI.GridPanel toolbarLayout = AntdControlFactory.CreateGridPanel("40:fill;40:fill");
            toolbarLayout.Dock = DockStyle.Fill;
            toolbarLayout.BackColor = Color.Transparent;
            AntdControlFactory.AddGridControl(toolbarLayout, topRow, 0);
            AntdControlFactory.AddGridControl(toolbarLayout, actionRow, 1);
            toolbarHost.Controls.Add(toolbarLayout);

            SuggestionTable = new AntdUI.Table();
            SuggestionTable.Dock = DockStyle.Fill;
            AntdControlFactory.ConfigureCleanupListSurface(SuggestionTable);
            SuggestionTable.FixedHeader = true;
            SuggestionTable.ScrollBarAvoidHeader = true;

            panel.Controls.Add(SuggestionTable);
            panel.Controls.Add(toolbarHost);
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

        private static AntdUI.Label CreateToolbarCaption(string text)
        {
            AntdUI.Label label = new AntdUI.Label();
            label.Dock = DockStyle.Fill;
            label.TextAlign = ContentAlignment.MiddleLeft;
            label.Text = text;
            label.Font = new Font("Microsoft YaHei UI", 9.5F);
            label.ForeColor = AntdUI.Style.Db.TextSecondary;
            label.BackColor = Color.Transparent;
            return label;
        }
    }
}
