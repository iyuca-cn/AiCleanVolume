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
using AiCleanVolume.Desktop.Presentation.Shared;
using AiCleanVolume.Desktop.Presentation.Shared.Antd;
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
            panel.Radius = 10;
            panel.Back = Palette.CardFill;
            panel.BorderWidth = 0F;
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
            label.Font = new Font("Segoe UI", 11F, FontStyle.Bold);
            label.ForeColor = Palette.TextPrimary;
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
            label.ForeColor = Palette.TextMuted;
            label.BackColor = Color.Transparent;
            return label;
        }

        private static int MeasureTextWidth(string text, Font font)
        {
            return AntdControlFactory.MeasureTextWidth(text, font);
        }

        private static int MeasureSelectWidth(Font font, params string[] options)
        {
            return AntdControlFactory.MeasureSelectWidth(font, options);
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
            panel.Radius = 8;
            panel.Back = SurfaceColor;
            panel.BorderWidth = 1F;
            panel.BorderColor = Color.FromArgb(230, 230, 230);
            panel.Shadow = 16;
            panel.ShadowOpacity = 0.08F;
            panel.ShadowOpacityHover = animateShadowOnHover ? 0.12F : 0.08F;
            panel.ShadowOpacityAnimation = animateShadowOnHover;
            panel.ShadowOffsetY = 3;
            return panel;
        }

        private static AntdUI.Panel CreateCompactSurfacePanel(int padding)
        {
            AntdUI.Panel panel = new AntdUI.Panel();
            panel.Padding = new Padding(padding);
            panel.Radius = 10;
            panel.Back = Palette.CardFill;
            panel.BorderWidth = 0F;
            panel.Shadow = 0;
            return panel;
        }

        private static AntdUI.Panel CreateFlatPanel()
        {
            return AntdControlFactory.CreateFlatPanel();
        }

        private static AntdUI.StackPanel CreateVerticalScrollPanel()
        {
            return AntdControlFactory.CreateVerticalScrollPanel();
        }

        private static AntdUI.GridPanel CreateGridPanel(string span)
        {
            return AntdControlFactory.CreateGridPanel(span);
        }

        private static void AddGridControl(AntdUI.GridPanel panel, Control control, int index)
        {
            AntdControlFactory.AddGridControl(panel, control, index);
        }

        private static Control CreateGridSpacer()
        {
            return AntdControlFactory.CreateGridSpacer();
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
            AntdControlFactory.ConfigureTableSurface(table);
        }

        private static AntdUI.Button CreateToolbarActionButton(string text, AntdUI.TTypeMini type)
        {
            return AntdControlFactory.CreateToolbarActionButton(text, type);
        }

        private static AntdUI.Button CreateSuggestionActionButton(string text, AntdUI.TTypeMini type)
        {
            return AntdControlFactory.CreateSuggestionActionButton(text, type);
        }

        private static AntdUI.Button CreateSettingsActionButton(string text, AntdUI.TTypeMini type)
        {
            return AntdControlFactory.CreateSettingsActionButton(text, type);
        }

        private static AntdUI.Button CreateAddAiProfileButton()
        {
            return AntdControlFactory.CreateAddAiProfileButton();
        }

        private static AntdUI.Switch CreateSettingsSwitch()
        {
            return AntdControlFactory.CreateSettingsSwitch();
        }

        private static AntdUI.Input CreateInput(string placeholder)
        {
            return AntdControlFactory.CreateInput(placeholder);
        }

        private static AntdUI.Checkbox CreateCheckbox(string text)
        {
            return AntdControlFactory.CreateCheckbox(text);
        }

        private AntdUI.Select CreateSelect()
        {
            return AntdControlFactory.CreateSelect(Font);
        }

        private AntdUI.Select CreateSettingsSelect()
        {
            return AntdControlFactory.CreateSettingsSelect(Font);
        }

        private static AntdUI.Label CreateCaption(string text)
        {
            return AntdControlFactory.CreateCaption(text);
        }

        private static void ConfigureCleanupListSurface(AntdUI.Table table)
        {
            AntdControlFactory.ConfigureCleanupListSurface(table);
        }

        private static Control CreateInfoCard(string title, out AntdUI.Label valueLabel)
        {
            return AntdControlFactory.CreateInfoCard(title, out valueLabel);
        }
    }
}
