using System;
using System.Drawing;
using System.Windows.Forms;
using AiCleanVolume.Core.Domain.Storage;
using AiCleanVolume.Desktop.Presentation.Shared;
using AiCleanVolume.Desktop.Presentation.Shared.Antd;

namespace AiCleanVolume.Desktop.Presentation.Features.Scan
{
    public sealed class ScanPageView : IFeaturePage
    {
        private static Color PageBackground { get { return AntdUI.Style.Db.BgLayout; } }

        private static Color SurfaceColor { get { return AntdUI.Style.Db.BgContainer; } }

        private static Color BorderLightColor { get { return AntdUI.Style.Db.BorderSecondary; } }

        private static Color TextPrimaryColor { get { return AntdUI.Style.Db.Text; } }

        private static Color TextSecondaryColor { get { return AntdUI.Style.Db.TextSecondary; } }

        private readonly AntdUI.Panel view;

        public ScanPageView(Font font)
        {
            if (font == null) throw new ArgumentNullException("font");

            ScanButton = AntdControlFactory.CreateToolbarActionButton("扫描", AntdUI.TTypeMini.Primary);
            view = CreatePageContainer();
            view.Controls.Add(CreateStoragePanel());
            view.Controls.Add(CreateToolbarPanel(font));
        }

        public string PageId
        {
            get { return "scan"; }
        }

        public Control View
        {
            get { return view; }
        }

        public AntdUI.Panel Panel
        {
            get { return view; }
        }

        public AntdUI.Select DriveSelect { get; private set; }

        public AntdUI.Select SortSelect { get; private set; }

        public AntdUI.Input PathInput { get; private set; }

        public AntdUI.Input MinSizeInput { get; private set; }

        public AntdUI.Input LimitInput { get; private set; }

        public AntdUI.Table StorageTable { get; private set; }

        public AntdUI.Button ScanButton { get; private set; }

        public AntdUI.Label SelectedDriveValueLabel { get; private set; }

        public AntdUI.Label TotalSpaceValueLabel { get; private set; }

        public AntdUI.Label UsedSpaceValueLabel { get; private set; }

        public AntdUI.Label AvailableSpaceValueLabel { get; private set; }

        public AntdUI.Label ReservedSpaceValueLabel { get; private set; }

        public AntdUI.Label ScanStatusLabel { get; private set; }

        public AntdUI.Label ScanElapsedLabel { get; private set; }

        public AntdUI.Progress ScanProgress { get; private set; }

        public void OnActivated()
        {
        }

        private static AntdUI.Panel CreatePageContainer()
        {
            AntdUI.Panel page = AntdControlFactory.CreateFlatPanel();
            page.Dock = DockStyle.Fill;
            page.BackColor = PageBackground;
            return page;
        }

        private Control CreateToolbarPanel(Font font)
        {
            AntdUI.Panel toolbarHost = AntdControlFactory.CreateFlatPanel();
            toolbarHost.Dock = DockStyle.Top;
            toolbarHost.BackColor = PageBackground;
            toolbarHost.Height = 148;
            toolbarHost.Padding = new Padding(0, 0, 0, 8);

            AntdUI.Panel toolbarCard = CreateCompactSurfacePanel(12);
            toolbarCard.Dock = DockStyle.Fill;

            AntdUI.GridPanel toolbarLayout = AntdControlFactory.CreateGridPanel("fill 1 420");
            toolbarLayout.Dock = DockStyle.Fill;
            toolbarLayout.BackColor = Color.Transparent;

            Control filtersPanel = CreateFiltersPanel(font);
            AntdUI.Divider divider = new AntdUI.Divider();
            divider.Dock = DockStyle.Fill;
            divider.Vertical = true;
            divider.ColorSplit = BorderLightColor;
            divider.Margin = new Padding(16, 4, 16, 8);

            Control statusPanel = CreateStatusPanel();
            Control summaryPanel = CreateDriveSummaryPanel();
            AntdUI.GridPanel leftLayout = AntdControlFactory.CreateGridPanel("84:fill;32:fill");
            leftLayout.Dock = DockStyle.Fill;
            AntdControlFactory.AddGridControl(leftLayout, filtersPanel, 0);
            AntdControlFactory.AddGridControl(leftLayout, statusPanel, 1);

            AntdControlFactory.AddGridControl(toolbarLayout, leftLayout, 0);
            AntdControlFactory.AddGridControl(toolbarLayout, divider, 1);
            AntdControlFactory.AddGridControl(toolbarLayout, summaryPanel, 2);

            toolbarCard.Controls.Add(toolbarLayout);
            toolbarHost.Controls.Add(toolbarCard);
            return toolbarHost;
        }

        private Control CreateFiltersPanel(Font font)
        {
            AntdUI.GridPanel host = AntdControlFactory.CreateGridPanel("42:42 192 86 42 112 44 88 44 88 fill;42:42 fill");
            host.Dock = DockStyle.Fill;
            host.BackColor = Color.Transparent;

            DriveSelect = CreateSelect(font);

            ScanButton.Dock = DockStyle.Fill;
            ScanButton.Margin = new Padding(10, 0, 0, 0);

            PathInput = AntdControlFactory.CreateInput("C:\\ 或目录路径");
            PathInput.PrefixSvg = "FolderOpenOutlined";

            SortSelect = CreateSelect(font);
            string[] sortOptionTexts = { "占用大小", "实际大小" };
            SortSelect.Items.Add(new AntdUI.SelectItem(sortOptionTexts[0], ScanSortMode.Allocated));
            SortSelect.Items.Add(new AntdUI.SelectItem(sortOptionTexts[1], ScanSortMode.Logical));
            SortSelect.Width = AntdControlFactory.MeasureSelectWidth(SortSelect.Font, sortOptionTexts);

            MinSizeInput = AntdControlFactory.CreateInput("-1 表示不限");
            LimitInput = AntdControlFactory.CreateInput("-1 表示不限");

            AntdControlFactory.AddGridControl(host, CreateToolbarCaption("选择:"), 0);
            AntdControlFactory.AddGridControl(host, DriveSelect, 1);
            AntdControlFactory.AddGridControl(host, ScanButton, 2);
            AntdControlFactory.AddGridControl(host, CreateToolbarCaption("模式:"), 3);
            AntdControlFactory.AddGridControl(host, SortSelect, 4);
            AntdControlFactory.AddGridControl(host, CreateToolbarCaption("最小:"), 5);
            AntdControlFactory.AddGridControl(host, MinSizeInput, 6);
            AntdControlFactory.AddGridControl(host, CreateToolbarCaption("限制:"), 7);
            AntdControlFactory.AddGridControl(host, LimitInput, 8);
            AntdControlFactory.AddGridControl(host, AntdControlFactory.CreateGridSpacer(), 9);
            AntdControlFactory.AddGridControl(host, CreateToolbarCaption("位置:"), 10);
            AntdControlFactory.AddGridControl(host, PathInput, 11);
            return host;
        }

        private Control CreateDriveSummaryPanel()
        {
            AntdUI.GridPanel layout = AntdControlFactory.CreateGridPanel("fill 190");
            layout.Dock = DockStyle.Fill;
            layout.BackColor = Color.Transparent;

            AntdUI.GridPanel leftLayout = AntdControlFactory.CreateGridPanel("24:48 fill;24:48 fill;24:48 fill");
            leftLayout.Dock = DockStyle.Fill;
            leftLayout.BackColor = Color.Transparent;

            AntdUI.GridPanel rightLayout = AntdControlFactory.CreateGridPanel("24:64 fill;24:64 fill;fill");
            rightLayout.Dock = DockStyle.Fill;
            rightLayout.BackColor = Color.Transparent;

            SelectedDriveValueLabel = CreateSummaryValueLabel(true);
            SelectedDriveValueLabel.AutoEllipsis = true;
            TotalSpaceValueLabel = CreateSummaryValueLabel(true);
            UsedSpaceValueLabel = CreateSummaryValueLabel(true);
            AvailableSpaceValueLabel = CreateSummaryValueLabel(true);
            ReservedSpaceValueLabel = CreateSummaryValueLabel(true);
            TotalSpaceValueLabel.AutoEllipsis = true;
            UsedSpaceValueLabel.AutoEllipsis = true;
            AvailableSpaceValueLabel.AutoEllipsis = true;
            ReservedSpaceValueLabel.AutoEllipsis = true;

            AntdControlFactory.AddGridControl(leftLayout, CreateSummaryCaption("选择:"), 0);
            AntdControlFactory.AddGridControl(leftLayout, SelectedDriveValueLabel, 1);
            AntdControlFactory.AddGridControl(leftLayout, CreateSummaryCaption("已用:"), 2);
            AntdControlFactory.AddGridControl(leftLayout, UsedSpaceValueLabel, 3);
            AntdControlFactory.AddGridControl(leftLayout, CreateSummaryCaption("可用:"), 4);
            AntdControlFactory.AddGridControl(leftLayout, AvailableSpaceValueLabel, 5);

            AntdControlFactory.AddGridControl(rightLayout, CreateSummaryCaption("总空间:"), 0);
            AntdControlFactory.AddGridControl(rightLayout, TotalSpaceValueLabel, 1);
            AntdControlFactory.AddGridControl(rightLayout, CreateSummaryCaption("预留:"), 2);
            AntdControlFactory.AddGridControl(rightLayout, ReservedSpaceValueLabel, 3);

            AntdControlFactory.AddGridControl(layout, leftLayout, 0);
            AntdControlFactory.AddGridControl(layout, rightLayout, 1);
            return layout;
        }

        private Control CreateStatusPanel()
        {
            AntdUI.GridPanel panel = AntdControlFactory.CreateGridPanel("126 112 fill");
            panel.Dock = DockStyle.Fill;
            panel.BackColor = Color.Transparent;
            panel.Padding = new Padding(0, 8, 0, 0);

            ScanStatusLabel = new AntdUI.Label();
            ScanStatusLabel.Dock = DockStyle.Fill;
            ScanStatusLabel.Font = new Font("Microsoft YaHei UI", 9F);
            ScanStatusLabel.ForeColor = TextSecondaryColor;
            ScanStatusLabel.BackColor = Color.Transparent;
            ScanStatusLabel.TextAlign = ContentAlignment.MiddleLeft;
            ScanStatusLabel.Text = "等待开始扫描";

            ScanElapsedLabel = new AntdUI.Label();
            ScanElapsedLabel.Dock = DockStyle.Fill;
            ScanElapsedLabel.Font = new Font("Microsoft YaHei UI", 9F);
            ScanElapsedLabel.ForeColor = TextSecondaryColor;
            ScanElapsedLabel.BackColor = Color.Transparent;
            ScanElapsedLabel.TextAlign = ContentAlignment.MiddleLeft;
            ScanElapsedLabel.Text = "用时 0.0 秒";
            ScanElapsedLabel.Margin = new Padding(8, 0, 0, 0);

            ScanProgress = new AntdUI.Progress();
            ScanProgress.Dock = DockStyle.Fill;
            ScanProgress.Margin = new Padding(0, 7, 4, 9);
            ScanProgress.Shape = AntdUI.TShapeProgress.Round;
            ScanProgress.Radius = 8;
            ScanProgress.Value = 0F;
            ScanProgress.State = AntdUI.TType.Success;
            ScanProgress.UseSystemText = false;

            AntdControlFactory.AddGridControl(panel, ScanStatusLabel, 0);
            AntdControlFactory.AddGridControl(panel, ScanElapsedLabel, 1);
            AntdControlFactory.AddGridControl(panel, ScanProgress, 2);
            return panel;
        }

        private Control CreateStoragePanel()
        {
            AntdUI.Panel panel = CreateCompactSurfacePanel(0);
            panel.Dock = DockStyle.Fill;

            StorageTable = new AntdUI.Table();
            StorageTable.Dock = DockStyle.Fill;
            StorageTable.TabStop = true;
            AntdControlFactory.ConfigureTableSurface(StorageTable);
            StorageTable.FixedHeader = true;
            StorageTable.ScrollBarAvoidHeader = true;

            panel.Controls.Add(StorageTable);
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

        private static AntdUI.Select CreateSelect(Font font)
        {
            AntdUI.Select select = new AntdUI.Select();
            select.Dock = DockStyle.Fill;
            select.DropDownArrow = true;
            select.ListAutoWidth = true;
            select.DropDownRadius = 6;
            select.Radius = 6;
            select.BorderWidth = 1F;
            select.BorderColor = Color.FromArgb(210, 210, 210);
            select.BorderHover = Color.FromArgb(22, 119, 255);
            select.BorderActive = Color.FromArgb(22, 119, 255);
            select.BackColor = Color.White;
            select.Font = font;
            return select;
        }

        private static AntdUI.Label CreateToolbarCaption(string text)
        {
            AntdUI.Label label = new AntdUI.Label();
            label.Dock = DockStyle.Fill;
            label.TextAlign = ContentAlignment.MiddleLeft;
            label.Text = text;
            label.Font = new Font("Microsoft YaHei UI", 9.5F);
            label.ForeColor = TextSecondaryColor;
            label.BackColor = Color.Transparent;
            return label;
        }

        private static AntdUI.Label CreateSummaryCaption(string text)
        {
            AntdUI.Label label = new AntdUI.Label();
            label.Dock = DockStyle.Fill;
            label.TextAlign = ContentAlignment.MiddleLeft;
            label.Text = text;
            label.Font = new Font("Microsoft YaHei UI", 9F);
            label.ForeColor = TextSecondaryColor;
            label.BackColor = Color.Transparent;
            return label;
        }

        private static AntdUI.Label CreateSummaryValueLabel(bool bold)
        {
            AntdUI.Label label = new AntdUI.Label();
            label.Dock = DockStyle.Fill;
            label.TextAlign = ContentAlignment.MiddleLeft;
            label.Font = new Font("Microsoft YaHei UI", bold ? 10F : 9.5F, bold ? FontStyle.Bold : FontStyle.Regular);
            label.ForeColor = TextPrimaryColor;
            label.BackColor = Color.Transparent;
            label.Text = "-";
            return label;
        }
    }
}
