using System;
using System.Drawing;
using System.Windows.Forms;
using AiCleanVolume.Desktop.Controls;
using AiCleanVolume.Desktop.Presentation.Shared;

namespace AiCleanVolume.Desktop.Presentation.Shared.Antd
{
    public static class AntdControlFactory
    {
        public static AntdUI.Panel CreateFlatPanel()
        {
            AntdUI.Panel panel = new AntdUI.Panel();
            panel.Radius = 0;
            panel.Shadow = 0;
            panel.BorderWidth = 0F;
            panel.Back = Color.Transparent;
            return panel;
        }

        public static AntdUI.StackPanel CreateVerticalScrollPanel()
        {
            AntdUI.StackPanel panel = new SmoothScrollStackPanel();
            panel.Vertical = true;
            panel.Radius = 0;
            panel.Gap = 0;
            return panel;
        }

        public static AntdUI.GridPanel CreateGridPanel(string span)
        {
            AntdUI.GridPanel panel = new AntdUI.GridPanel();
            panel.Span = span;
            panel.Radius = 0;
            panel.BorderWidth = 0F;
            panel.Back = Color.Transparent;
            panel.BackColor = Color.Transparent;
            return panel;
        }

        public static void AddGridControl(AntdUI.GridPanel panel, Control control, int index)
        {
            panel.Controls.Add(control);
            panel.SetIndex(control, index);
        }

        public static Control CreateGridSpacer()
        {
            return CreateFlatPanel();
        }

        public static void ConfigureTableSurface(AntdUI.Table table)
        {
            table.Bordered = false;
            table.Radius = 10;
            table.BorderWidth = 0F;
            table.BorderCellWidth = 1F;
            table.BorderColor = Palette.Divider;
            table.ColumnBack = Palette.TableHeader;
            table.ColumnFore = Palette.TableHeaderText;
            table.RowHoverBg = Palette.RowHover;
            table.RowSelectedBg = Palette.RowSelected;
        }

        public static AntdUI.Button CreateSuggestionActionButton(string text, AntdUI.TTypeMini type)
        {
            AntdUI.Button button = new AntdUI.Button();
            button.AutoSizeMode = AntdUI.TAutoSize.None;
            button.Text = text;
            button.Type = type;
            button.Width = 76;
            button.Height = 30;
            button.Radius = 8;
            button.BorderWidth = 1F;
            button.WaveSize = 2;
            button.Ghost = type == AntdUI.TTypeMini.Default;
            button.Margin = new Padding(8, 0, 0, 0);
            return button;
        }

        public static AntdUI.Button CreateToolbarActionButton(string text, AntdUI.TTypeMini type)
        {
            AntdUI.Button button = new AntdUI.Button();
            button.AutoSizeMode = AntdUI.TAutoSize.None;
            button.Text = text;
            button.Type = type;
            button.Width = text == "删除勾选" || text == "保存配置" ? 110 : 100;
            button.Height = 36;
            button.Radius = 6;
            button.BorderWidth = 0F;
            button.WaveSize = 3;
            button.IconSvg = GetToolbarButtonIconSvg(text);
            button.Margin = Padding.Empty;
            return button;
        }

        public static AntdUI.Button CreateSettingsActionButton(string text, AntdUI.TTypeMini type)
        {
            AntdUI.Button button = new AntdUI.Button();
            button.Dock = DockStyle.Fill;
            button.AutoSizeMode = AntdUI.TAutoSize.None;
            button.Text = text;
            button.Type = type;
            button.Height = 34;
            button.Radius = 6;
            button.BorderWidth = 1F;
            button.WaveSize = 2;
            button.Ghost = type == AntdUI.TTypeMini.Default;
            button.Margin = new Padding(0, 4, 8, 4);
            return button;
        }

        public static AntdUI.Button CreateAddAiProfileButton()
        {
            AntdUI.Button button = new AntdUI.Button();
            button.Dock = DockStyle.Fill;
            button.AutoSizeMode = AntdUI.TAutoSize.None;
            button.DisplayStyle = AntdUI.TButtonDisplayStyle.Image;
            button.Shape = AntdUI.TShape.Circle;
            button.IconSvg = "PlusOutlined";
            button.Type = AntdUI.TTypeMini.Primary;
            button.Width = 32;
            button.Height = 32;
            button.BorderWidth = 0F;
            button.Margin = new Padding(8, 4, 0, 4);
            button.WaveSize = 2;
            return button;
        }

        public static AntdUI.Select CreateSelect(Font font)
        {
            AntdUI.Select select = new AntdUI.Select();
            select.Dock = DockStyle.Fill;
            select.DropDownArrow = true;
            select.ListAutoWidth = true;
            select.DropDownRadius = 8;
            select.Radius = 8;
            select.BorderWidth = 1F;
            select.BorderColor = Palette.Border;
            select.BorderHover = Palette.Accent;
            select.BorderActive = Palette.Accent;
            select.BackColor = Palette.Surface;
            select.Font = font;
            return select;
        }

        public static AntdUI.Select CreateSettingsSelect(Font font)
        {
            AntdUI.Select select = CreateSelect(font);
            select.WheelModifyEnabled = false;
            return select;
        }

        public static AntdUI.Switch CreateSettingsSwitch()
        {
            AntdUI.Switch control = new AntdUI.Switch();
            control.Width = 56;
            control.Height = 28;
            control.Anchor = AnchorStyles.Left;
            control.Margin = new Padding(0, 8, 16, 8);
            control.WaveSize = 2;
            return control;
        }

        public static AntdUI.Input CreateInput(string placeholder)
        {
            AntdUI.Input input = new AntdUI.Input();
            input.Dock = DockStyle.Fill;
            input.PlaceholderText = placeholder;
            input.Font = new Font("Segoe UI", 10F);
            input.Radius = 8;
            input.BorderWidth = 1F;
            input.BorderColor = Palette.Border;
            input.BorderHover = Palette.Accent;
            input.BorderActive = Palette.Accent;
            input.BackColor = Palette.Surface;
            return input;
        }

        public static AntdUI.Checkbox CreateCheckbox(string text)
        {
            AntdUI.Checkbox checkbox = new AntdUI.Checkbox();
            checkbox.Dock = DockStyle.Fill;
            checkbox.Text = text;
            checkbox.Font = new Font("Segoe UI", 9.5F);
            checkbox.ForeColor = Palette.TextSecondary;
            checkbox.BackColor = Color.Transparent;
            return checkbox;
        }

        public static AntdUI.Label CreateCaption(string text)
        {
            AntdUI.Label label = new AntdUI.Label();
            label.Dock = DockStyle.Fill;
            label.TextAlign = ContentAlignment.MiddleLeft;
            label.Text = text;
            label.Font = new Font("Segoe UI", 9.5F);
            label.ForeColor = Palette.TextSecondary;
            label.BackColor = Color.Transparent;
            return label;
        }

        public static void ConfigureCleanupListSurface(AntdUI.Table table)
        {
            ConfigureTableSurface(table);
            table.RowHeight = 52;
            table.RowHeightHeader = 40;
            table.GapCell = 8;
        }

        public static Control CreateInfoCard(string title, out AntdUI.Label valueLabel)
        {
            AntdUI.Panel panel = new AntdUI.Panel();
            panel.Dock = DockStyle.Fill;
            panel.Radius = 8;
            panel.Padding = new Padding(12, 8, 12, 8);
            panel.Back = Palette.CardFill;
            panel.BorderWidth = 0F;
            panel.Shadow = 0;

            AntdUI.Label titleLabel = new AntdUI.Label();
            titleLabel.Dock = DockStyle.Top;
            titleLabel.Height = 18;
            titleLabel.Text = title;
            titleLabel.Font = new Font("Segoe UI", 9F);
            titleLabel.ForeColor = Palette.TextMuted;
            titleLabel.BackColor = Color.Transparent;

            valueLabel = new AntdUI.Label();
            valueLabel.Dock = DockStyle.Fill;
            valueLabel.Font = new Font("Segoe UI", 11F, FontStyle.Bold);
            valueLabel.ForeColor = Palette.TextPrimary;
            valueLabel.BackColor = Color.Transparent;
            valueLabel.TextAlign = ContentAlignment.MiddleLeft;
            valueLabel.Text = "-";

            panel.Controls.Add(valueLabel);
            panel.Controls.Add(titleLabel);
            return panel;
        }

        public static int MeasureTextWidth(string text, Font font)
        {
            return TextRenderer.MeasureText(text ?? string.Empty, font, new Size(int.MaxValue, int.MaxValue), TextFormatFlags.NoPadding).Width;
        }

        public static int MeasureSelectWidth(Font font, params string[] options)
        {
            int maxTextWidth = 0;
            for (int i = 0; i < options.Length; i++)
            {
                maxTextWidth = Math.Max(maxTextWidth, MeasureTextWidth(options[i], font));
            }

            return maxTextWidth + 40;
        }

        private static string GetToolbarButtonIconSvg(string text)
        {
            switch (text)
            {
                case "扫描":
                case "常规清理":
                    return "SearchOutlined";
                case "保存配置":
                    return "SaveFilled";
                case "删除勾选":
                    return "DeleteFilled";
                case "AI 识别":
                    return "RobotFilled";
                case "超级清理":
                    return "RocketFilled";
                default:
                    return null;
            }
        }
    }
}
