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
        private static AntdUI.Panel CreateSettingsSurfacePanel(int padding)
        {
            AntdUI.Panel panel = new AntdUI.Panel();
            panel.Dock = DockStyle.Fill;
            panel.Padding = new Padding(padding);
            panel.Radius = 14;
            panel.Back = SurfaceColor;
            panel.BorderWidth = 1F;
            panel.BorderColor = BorderLightColor;
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

            body = CreateFlatPanel();
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
            label.Font = new Font("Microsoft YaHei UI", 11.5F, FontStyle.Bold);
            label.ForeColor = TextPrimaryColor;
            label.BackColor = Color.Transparent;
            return label;
        }

        private static AntdUI.Label CreateSmallMutedLabel(string text)
        {
            AntdUI.Label label = new AntdUI.Label();
            label.Dock = DockStyle.Fill;
            label.Text = text;
            label.TextAlign = ContentAlignment.MiddleLeft;
            label.Font = new Font("Microsoft YaHei UI", 9F);
            label.ForeColor = TextTertiaryColor;
            label.BackColor = Color.Transparent;
            return label;
        }

        private static int MeasureTextWidth(string text, Font font)
        {
            return TextRenderer.MeasureText(text ?? string.Empty, font, new Size(int.MaxValue, int.MaxValue), TextFormatFlags.NoPadding).Width;
        }

        private static int MeasureSelectWidth(Font font, params string[] options)
        {
            int maxTextWidth = 0;
            for (int i = 0; i < options.Length; i++)
            {
                maxTextWidth = Math.Max(maxTextWidth, MeasureTextWidth(options[i], font));
            }

            return maxTextWidth + 40;
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

        private static int ParseInt(string text, int fallback)
        {
            int parsed;
            return int.TryParse(text, out parsed) ? parsed : fallback;
        }

        private static AntdUI.Panel CreateCardPanel(int padding)
        {
            return CreateCardPanel(padding, false);
        }

        private static AntdUI.Panel CreateCardPanel(int padding, bool animateShadowOnHover)
        {
            AntdUI.Panel panel = new AntdUI.Panel();
            panel.Padding = new Padding(padding);
            panel.Radius = 14;
            panel.Back = SurfaceColor;
            panel.BorderWidth = 1F;
            panel.BorderColor = BorderLightColor;
            panel.Shadow = 18;
            panel.ShadowOpacity = 0.08F;
            panel.ShadowOpacityHover = animateShadowOnHover ? 0.14F : 0.08F;
            panel.ShadowOpacityAnimation = animateShadowOnHover;
            panel.ShadowOffsetY = 5;
            return panel;
        }

        private static AntdUI.Panel CreateCompactSurfacePanel(int padding)
        {
            AntdUI.Panel panel = new AntdUI.Panel();
            panel.Padding = new Padding(padding);
            panel.Radius = 4;
            panel.Back = SurfaceColor;
            panel.BorderWidth = 1F;
            panel.BorderColor = BorderLightColor;
            panel.Shadow = 0;
            return panel;
        }

        private static AntdUI.Panel CreateFlatPanel()
        {
            AntdUI.Panel panel = new AntdUI.Panel();
            panel.Radius = 0;
            panel.Shadow = 0;
            panel.BorderWidth = 0F;
            panel.Back = Color.Transparent;
            return panel;
        }

        private static AntdUI.StackPanel CreateVerticalScrollPanel()
        {
            AntdUI.StackPanel panel = new SmoothScrollStackPanel();
            panel.Vertical = true;
            panel.Radius = 0;
            panel.Gap = 0;
            return panel;
        }

        private static AntdUI.GridPanel CreateGridPanel(string span)
        {
            AntdUI.GridPanel panel = new AntdUI.GridPanel();
            panel.Span = span;
            panel.Radius = 0;
            panel.BorderWidth = 0F;
            panel.Back = Color.Transparent;
            panel.BackColor = Color.Transparent;
            return panel;
        }

        private static void AddGridControl(AntdUI.GridPanel panel, Control control, int index)
        {
            panel.Controls.Add(control);
            panel.SetIndex(control, index);
        }

        private static Control CreateGridSpacer()
        {
            return CreateFlatPanel();
        }

        private static AntdUI.Label CreateSectionTitle(string text)
        {
            AntdUI.Label heading = new AntdUI.Label();
            heading.Dock = DockStyle.Top;
            heading.Height = 34;
            heading.Text = text;
            heading.Font = new Font("Microsoft YaHei UI", 13.5F, FontStyle.Bold);
            heading.ForeColor = TextPrimaryColor;
            heading.BackColor = Color.Transparent;
            return heading;
        }

        private static AntdUI.Label CreateSectionDescription(string text)
        {
            AntdUI.Label desc = new AntdUI.Label();
            desc.Dock = DockStyle.Top;
            desc.Height = 26;
            desc.Text = text;
            desc.ForeColor = TextSecondaryColor;
            desc.Font = new Font("Microsoft YaHei UI", 9.5F);
            desc.BackColor = Color.Transparent;
            return desc;
        }

        private static void ConfigureTableSurface(AntdUI.Table table)
        {
            table.Bordered = true;
            table.Radius = 10;
            table.BorderWidth = 1F;
            table.BorderCellWidth = 1F;
            table.BorderColor = BorderLightColor;
            table.ColumnBack = FillSecondary;
            table.ColumnFore = TextSecondaryColor;
            table.RowHoverBg = Color.FromArgb(245, 248, 255);
            table.RowSelectedBg = PrimarySoftColor;
        }

        private static AntdUI.Button CreateToolbarActionButton(string text, AntdUI.TTypeMini type)
        {
            AntdUI.Button button = new AntdUI.Button();
            button.AutoSizeMode = AntdUI.TAutoSize.None;
            button.Text = text;
            button.Type = type;
            button.Width = text == "删除勾选" || text == "保存配置" ? 104 : 92;
            button.Height = 40;
            button.Radius = 9;
            button.BorderWidth = 0F;
            button.IconSvg = GetToolbarButtonIconSvg(text);
            button.Margin = Padding.Empty;
            return button;
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

        private static AntdUI.Button CreateSuggestionActionButton(string text, AntdUI.TTypeMini type)
        {
            AntdUI.Button button = new AntdUI.Button();
            button.AutoSizeMode = AntdUI.TAutoSize.None;
            button.Text = text;
            button.Type = type;
            button.Width = 78;
            button.Height = 28;
            button.Radius = 8;
            button.BorderWidth = 1F;
            button.Ghost = type == AntdUI.TTypeMini.Default;
            button.Margin = new Padding(8, 0, 0, 0);
            return button;
        }

        private static AntdUI.Button CreateSettingsActionButton(string text, AntdUI.TTypeMini type)
        {
            AntdUI.Button button = new AntdUI.Button();
            button.Dock = DockStyle.Fill;
            button.AutoSizeMode = AntdUI.TAutoSize.None;
            button.Text = text;
            button.Type = type;
            button.Height = 34;
            button.Radius = 8;
            button.BorderWidth = 1F;
            button.Ghost = type == AntdUI.TTypeMini.Default;
            button.Margin = new Padding(0, 4, 8, 4);
            return button;
        }

        private static AntdUI.Button CreateAddAiProfileButton()
        {
            AntdUI.Button button = new AntdUI.Button();
            button.Dock = DockStyle.Fill;
            button.AutoSizeMode = AntdUI.TAutoSize.None;
            button.DisplayStyle = AntdUI.TButtonDisplayStyle.Image;
            button.Shape = AntdUI.TShape.Circle;
            button.IconSvg = "PlusOutlined";
            button.Type = AntdUI.TTypeMini.Warn;
            button.Width = 34;
            button.Height = 34;
            button.BorderWidth = 0F;
            button.Margin = new Padding(8, 4, 0, 4);
            button.WaveSize = 2;
            return button;
        }

        private static AntdUI.Switch CreateSettingsSwitch()
        {
            AntdUI.Switch control = new AntdUI.Switch();
            control.Width = 60;
            control.Height = 34;
            control.Anchor = AnchorStyles.Left;
            control.Margin = new Padding(0, 8, 16, 8);
            control.WaveSize = 2;
            return control;
        }

        private static AntdUI.Input CreateInput(string placeholder)
        {
            AntdUI.Input input = new AntdUI.Input();
            input.Dock = DockStyle.Fill;
            input.PlaceholderText = placeholder;
            input.Font = new Font("Microsoft YaHei UI", 10.5F);
            input.Radius = 8;
            input.BorderWidth = 1F;
            input.BorderColor = BorderDefaultColor;
            input.BorderHover = PrimaryColor;
            input.BorderActive = PrimaryColor;
            input.BackColor = SurfaceColor;
            return input;
        }

        private static AntdUI.Checkbox CreateCheckbox(string text)
        {
            AntdUI.Checkbox checkbox = new AntdUI.Checkbox();
            checkbox.Dock = DockStyle.Fill;
            checkbox.Text = text;
            checkbox.Font = new Font("Microsoft YaHei UI", 9.5F);
            checkbox.ForeColor = TextPrimaryColor;
            checkbox.BackColor = Color.Transparent;
            return checkbox;
        }

        private AntdUI.Select CreateSelect()
        {
            AntdUI.Select select = new AntdUI.Select();
            select.Dock = DockStyle.Fill;
            select.DropDownArrow = true;
            select.ListAutoWidth = true;
            select.DropDownRadius = 8;
            select.Radius = 8;
            select.BorderWidth = 1F;
            select.BorderColor = BorderDefaultColor;
            select.BorderHover = PrimaryColor;
            select.BorderActive = PrimaryColor;
            select.BackColor = SurfaceColor;
            select.Font = Font;
            return select;
        }

        private AntdUI.Select CreateSettingsSelect()
        {
            AntdUI.Select select = CreateSelect();
            select.WheelModifyEnabled = false;
            return select;
        }

        private static AntdUI.Label CreateCaption(string text)
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

        private static void ConfigureCleanupListSurface(AntdUI.Table table)
        {
            ConfigureTableSurface(table);
            table.RowHeight = 54;
            table.RowHeightHeader = 42;
            table.GapCell = 8;
        }

        private static Control CreateInfoCard(string title, out AntdUI.Label valueLabel)
        {
            AntdUI.Panel panel = new AntdUI.Panel();
            panel.Dock = DockStyle.Fill;
            panel.Radius = 8;
            panel.Padding = new Padding(12, 8, 12, 8);
            panel.Back = FillSecondary;
            panel.BorderWidth = 1F;
            panel.BorderColor = BorderLightColor;

            AntdUI.Label titleLabel = new AntdUI.Label();
            titleLabel.Dock = DockStyle.Top;
            titleLabel.Height = 18;
            titleLabel.Text = title;
            titleLabel.Font = new Font("Microsoft YaHei UI", 9F);
            titleLabel.ForeColor = TextTertiaryColor;
            titleLabel.BackColor = Color.Transparent;

            valueLabel = new AntdUI.Label();
            valueLabel.Dock = DockStyle.Fill;
            valueLabel.Font = new Font("Microsoft YaHei UI", 11F, FontStyle.Bold);
            valueLabel.ForeColor = TextPrimaryColor;
            valueLabel.BackColor = Color.Transparent;
            valueLabel.TextAlign = ContentAlignment.MiddleLeft;
            valueLabel.Text = "-";

            panel.Controls.Add(valueLabel);
            panel.Controls.Add(titleLabel);
            return panel;
        }
    }
}
