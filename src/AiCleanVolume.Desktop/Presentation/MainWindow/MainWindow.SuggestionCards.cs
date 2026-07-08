using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using AiCleanVolume.Core.Domain.Cleanup;
using AiCleanVolume.Core.Application.Scanning;
using AiCleanVolume.Desktop.Controls;
using AiCleanVolume.Desktop.Presentation.Shared;
using AiCleanVolume.Desktop.Presentation.Shared.Antd;
using AiCleanVolume.Desktop.ViewModels;

namespace AiCleanVolume.Desktop
{
    // 右栏智能推荐：可勾选卡片列表 + 底部合计执行栏。
    // suggestionTable 保留为隐藏数据载体，删除流程沿用其行状态更新。
    public sealed partial class MainWindow : AntdUI.Window
    {
        private AntdUI.StackPanel suggestionCardsPanel;

        private AntdUI.Panel suggestionEmptyHint;

        private AntdUI.Label suggestionSelectionLabel;

        private AntdUI.Label suggestionSelectionSizeLabel;

        private AntdUI.Button suggestionRecycleButton;

        private AntdUI.Button suggestionPermDeleteButton;

        private void BuildSuggestionCardsColumn()
        {
            AntdUI.Panel headerRow = CreateFlatPanel();
            headerRow.Dock = DockStyle.Top;
            headerRow.Height = 46;
            headerRow.Padding = new Padding(16, 12, 12, 4);

            AntdUI.Label titleLabel = new AntdUI.Label();
            titleLabel.Dock = DockStyle.Left;
            titleLabel.AutoSizeMode = AntdUI.TAutoSize.Width;
            titleLabel.Text = "智能推荐";
            titleLabel.Font = new Font(Font.FontFamily, 10F, FontStyle.Bold);
            titleLabel.ForeColor = Palette.TextPrimary;

            analyzeButton = new AntdUI.Button();
            analyzeButton.Dock = DockStyle.Right;
            analyzeButton.AutoSizeMode = AntdUI.TAutoSize.Width;
            analyzeButton.Text = "重新分析";
            analyzeButton.IconSvg = "SyncOutlined";
            analyzeButton.IconRatio = 0.7F;
            analyzeButton.Ghost = true;
            analyzeButton.BorderWidth = 0F;
            analyzeButton.ForeColor = Palette.Accent;
            analyzeButton.Font = new Font(Font.FontFamily, 9F);
            analyzeButton.Click += delegate { AnalyzeSuggestions(); };

            headerRow.Controls.Add(analyzeButton);
            headerRow.Controls.Add(titleLabel);
            titleLabel.BringToFront();

            AntdUI.Panel hintPanel = new AntdUI.Panel();
            hintPanel.Dock = DockStyle.Top;
            hintPanel.Height = 46;
            hintPanel.Radius = 8;
            hintPanel.Back = Palette.AccentSoft;
            hintPanel.BorderWidth = 1F;
            hintPanel.BorderColor = Palette.AccentSoftBorder;
            hintPanel.Shadow = 0;
            hintPanel.Margin = new Padding(16, 0, 16, 6);
            hintPanel.Padding = new Padding(10, 4, 10, 4);

            AntdUI.Label hintLabel = new AntdUI.Label();
            hintLabel.Dock = DockStyle.Fill;
            hintLabel.Text = "AI 发现的可清理项，勾选后在底部一键执行。删除前会再次做沙盒安全评估。";
            hintLabel.Font = new Font(Font.FontFamily, 8.5F);
            hintLabel.ForeColor = Palette.AccentText;
            hintLabel.BackColor = Color.Transparent;
            hintPanel.Controls.Add(hintLabel);

            AntdUI.Panel hintHost = CreateFlatPanel();
            hintHost.Dock = DockStyle.Top;
            hintHost.Height = 54;
            hintHost.Padding = new Padding(16, 0, 16, 8);
            hintPanel.Dock = DockStyle.Fill;
            hintPanel.Margin = Padding.Empty;
            hintHost.Controls.Add(hintPanel);

            suggestionCardsPanel = AntdControlFactory.CreateVerticalScrollPanel();
            suggestionCardsPanel.Dock = DockStyle.Fill;
            suggestionCardsPanel.BackColor = Palette.Surface;
            suggestionCardsPanel.Padding = new Padding(12, 2, 12, 8);
            suggestionCardsPanel.Gap = 8;

            suggestionEmptyHint = CreateFlatPanel();
            suggestionEmptyHint.Dock = DockStyle.Fill;
            AntdUI.Label emptyLabel = new AntdUI.Label();
            emptyLabel.Dock = DockStyle.Fill;
            emptyLabel.Text = "扫描完成后自动生成推荐；\r\n也可点击右上角「重新分析」。";
            emptyLabel.Font = new Font(Font.FontFamily, 9F);
            emptyLabel.ForeColor = Palette.TextFaint;
            emptyLabel.TextAlign = ContentAlignment.MiddleCenter;
            suggestionEmptyHint.Controls.Add(emptyLabel);

            // 底部合计执行栏
            AntdUI.Panel footer = CreateFlatPanel();
            footer.Dock = DockStyle.Bottom;
            footer.Height = 108;
            footer.BackColor = Palette.SurfaceFaint;
            footer.Padding = new Padding(16, 8, 16, 8);

            AntdUI.Panel footerInfoRow = CreateFlatPanel();
            footerInfoRow.Dock = DockStyle.Top;
            footerInfoRow.Height = 28;

            suggestionSelectionLabel = new AntdUI.Label();
            suggestionSelectionLabel.Dock = DockStyle.Left;
            suggestionSelectionLabel.AutoSizeMode = AntdUI.TAutoSize.Width;
            suggestionSelectionLabel.Font = new Font(Font.FontFamily, 9F);
            suggestionSelectionLabel.ForeColor = Palette.TextSecondary;
            suggestionSelectionLabel.Text = "已选 0 项";

            suggestionSelectionSizeLabel = new AntdUI.Label();
            suggestionSelectionSizeLabel.Dock = DockStyle.Right;
            suggestionSelectionSizeLabel.AutoSizeMode = AntdUI.TAutoSize.Width;
            suggestionSelectionSizeLabel.Font = AntdControlFactory.MonoFontBold(11F);
            suggestionSelectionSizeLabel.ForeColor = Palette.Accent;
            suggestionSelectionSizeLabel.Text = string.Empty;

            footerInfoRow.Controls.Add(suggestionSelectionLabel);
            footerInfoRow.Controls.Add(suggestionSelectionSizeLabel);

            AntdUI.Panel footerButtonRow = CreateFlatPanel();
            footerButtonRow.Dock = DockStyle.Top;
            footerButtonRow.Height = 42;
            footerButtonRow.Padding = new Padding(0, 4, 0, 4);

            suggestionPermDeleteButton = new AntdUI.Button();
            suggestionPermDeleteButton.Dock = DockStyle.Right;
            suggestionPermDeleteButton.Width = 96;
            suggestionPermDeleteButton.AutoSizeMode = AntdUI.TAutoSize.None;
            suggestionPermDeleteButton.Text = "永久删除";
            suggestionPermDeleteButton.Radius = 8;
            suggestionPermDeleteButton.BorderWidth = 1F;
            suggestionPermDeleteButton.DefaultBorderColor = Palette.DangerBorder;
            suggestionPermDeleteButton.ForeColor = Palette.Danger;
            suggestionPermDeleteButton.Margin = new Padding(8, 0, 0, 0);
            suggestionPermDeleteButton.Click += delegate { DeleteSelectedSuggestionsWithMode(false); };

            suggestionRecycleButton = new AntdUI.Button();
            suggestionRecycleButton.Dock = DockStyle.Fill;
            suggestionRecycleButton.Text = "移到回收站";
            suggestionRecycleButton.Type = AntdUI.TTypeMini.Primary;
            suggestionRecycleButton.Radius = 8;
            suggestionRecycleButton.Click += delegate { DeleteSelectedSuggestionsWithMode(true); };

            footerButtonRow.Controls.Add(suggestionRecycleButton);
            footerButtonRow.Controls.Add(suggestionPermDeleteButton);
            suggestionRecycleButton.BringToFront();

            AntdUI.Label footerHint = new AntdUI.Label();
            footerHint.Dock = DockStyle.Bottom;
            footerHint.Height = 20;
            footerHint.Text = "执行前将逐项进行安全评估";
            footerHint.Font = new Font(Font.FontFamily, 8F);
            footerHint.ForeColor = Palette.TextFaint;
            footerHint.TextAlign = ContentAlignment.MiddleCenter;

            footer.Controls.Add(footerButtonRow);
            footer.Controls.Add(footerInfoRow);
            footer.Controls.Add(footerHint);
            footerButtonRow.BringToFront();

            rightColumnPanel.Controls.Add(suggestionCardsPanel);
            rightColumnPanel.Controls.Add(suggestionEmptyHint);
            rightColumnPanel.Controls.Add(hintHost);
            rightColumnPanel.Controls.Add(headerRow);
            rightColumnPanel.Controls.Add(footer);
            suggestionCardsPanel.BringToFront();
            suggestionEmptyHint.BringToFront();
            UpdateSuggestionCardsVisibility();
        }

        private void RebuildSuggestionCards()
        {
            if (suggestionCardsPanel == null) return;

            suggestionCardsPanel.SuspendLayout();
            try
            {
                suggestionCardsPanel.Controls.Clear();
                if (suggestionRows != null)
                {
                    for (int i = suggestionRows.Count - 1; i >= 0; i--)
                    {
                        suggestionCardsPanel.Controls.Add(BuildSuggestionCard(suggestionRows[i]));
                    }
                }
            }
            finally
            {
                suggestionCardsPanel.ResumeLayout(true);
            }

            UpdateSuggestionCardsVisibility();
            RefreshSuggestionSelectionFooter();
        }

        private void UpdateSuggestionCardsVisibility()
        {
            bool hasCards = suggestionRows != null && suggestionRows.Count > 0;
            if (suggestionCardsPanel != null) suggestionCardsPanel.Visible = hasCards;
            if (suggestionEmptyHint != null) suggestionEmptyHint.Visible = !hasCards;
        }

        private Control BuildSuggestionCard(CleanupSuggestionRow row)
        {
            CleanupSuggestion suggestion = row.Suggestion;
            bool deleted = suggestion.Status == CleanupStatus.Deleted;

            AntdUI.Panel card = new AntdUI.Panel();
            card.Radius = 10;
            card.Back = deleted ? Palette.CardFill : Palette.Surface;
            card.BorderWidth = 1F;
            card.BorderColor = row.selected ? Palette.AccentSoftBorder : Palette.Border;
            card.Shadow = 0;
            card.Height = 108;
            card.Padding = new Padding(10, 8, 10, 6);

            AntdUI.Panel titleRow = CreateFlatPanel();
            titleRow.Dock = DockStyle.Top;
            titleRow.Height = 26;

            AntdUI.Checkbox checkbox = new AntdUI.Checkbox();
            checkbox.Dock = DockStyle.Left;
            checkbox.AutoSizeMode = AntdUI.TAutoSize.Width;
            checkbox.Text = suggestion.Name;
            checkbox.Font = new Font(Font.FontFamily, 9.5F, FontStyle.Bold);
            checkbox.ForeColor = deleted ? Palette.TextFaint : Palette.TextPrimary;
            checkbox.Checked = row.selected && !deleted;
            checkbox.Enabled = !deleted;
            checkbox.CheckedChanged += delegate (object sender, AntdUI.BoolEventArgs e)
            {
                row.selected = e.Value;
                card.BorderColor = e.Value ? Palette.AccentSoftBorder : Palette.Border;
                RefreshSuggestionSelectionFooter();
            };

            AntdUI.Tag riskTag = new AntdUI.Tag();
            riskTag.Dock = DockStyle.Right;
            riskTag.AutoSizeMode = AntdUI.TAutoSize.Width;
            riskTag.Font = new Font(Font.FontFamily, 7.5F);
            riskTag.Radius = 8;
            riskTag.BorderWidth = 0F;
            if (deleted)
            {
                riskTag.Text = "已删除";
                riskTag.BackColor = Palette.CardFill;
                riskTag.ForeColor = Palette.TextFaint;
            }
            else if (suggestion.Risk == CleanupRisk.Low)
            {
                riskTag.Text = "可安全清理";
                riskTag.BackColor = Palette.SuccessSoft;
                riskTag.ForeColor = Palette.Success;
            }
            else
            {
                riskTag.Text = "需确认";
                riskTag.BackColor = Palette.WarningSoft;
                riskTag.ForeColor = Palette.Warning;
            }

            AntdUI.Tag sourceTag = new AntdUI.Tag();
            sourceTag.Dock = DockStyle.Right;
            sourceTag.AutoSizeMode = AntdUI.TAutoSize.Width;
            sourceTag.Text = suggestion.Source;
            sourceTag.Font = new Font(Font.FontFamily, 7.5F);
            sourceTag.Radius = 8;
            sourceTag.BorderWidth = 0F;
            sourceTag.BackColor = Palette.CardFill;
            sourceTag.ForeColor = Palette.TableHeaderText;
            sourceTag.Margin = new Padding(0, 0, 4, 0);

            titleRow.Controls.Add(checkbox);
            titleRow.Controls.Add(sourceTag);
            titleRow.Controls.Add(riskTag);
            checkbox.BringToFront();

            AntdUI.Label descLabel = new AntdUI.Label();
            descLabel.Dock = DockStyle.Top;
            descLabel.Height = 34;
            descLabel.Text = string.IsNullOrWhiteSpace(suggestion.Reason) ? suggestion.Path : suggestion.Reason;
            descLabel.Font = new Font(Font.FontFamily, 8F);
            descLabel.ForeColor = Palette.TextMuted;
            descLabel.AutoEllipsis = true;

            AntdUI.Panel bottomRow = CreateFlatPanel();
            bottomRow.Dock = DockStyle.Top;
            bottomRow.Height = 24;

            AntdUI.Label sizeLabel = new AntdUI.Label();
            sizeLabel.Dock = DockStyle.Left;
            sizeLabel.AutoSizeMode = AntdUI.TAutoSize.Width;
            sizeLabel.Text = StorageFormatting.FormatBytes(suggestion.Bytes);
            sizeLabel.Font = AntdControlFactory.MonoFontBold(9.5F);
            sizeLabel.ForeColor = deleted ? Palette.TextFaint : Palette.TextPrimary;

            AntdUI.Button openButton = new AntdUI.Button();
            openButton.Dock = DockStyle.Right;
            openButton.AutoSizeMode = AntdUI.TAutoSize.Width;
            openButton.Text = "打开位置";
            openButton.Font = new Font(Font.FontFamily, 8F);
            openButton.Ghost = true;
            openButton.BorderWidth = 0F;
            openButton.ForeColor = Palette.Accent;
            openButton.Click += delegate { explorerService.OpenPath(suggestion.Path, !suggestion.IsDirectory); };

            bottomRow.Controls.Add(sizeLabel);
            bottomRow.Controls.Add(openButton);

            card.Controls.Add(bottomRow);
            card.Controls.Add(descLabel);
            card.Controls.Add(titleRow);
            bottomRow.BringToFront();
            AntdUI.TooltipComponent tooltip = new AntdUI.TooltipComponent();
            tooltip.SetTip(checkbox, suggestion.Path);
            return card;
        }

        private void RefreshSuggestionSelectionFooter()
        {
            if (suggestionSelectionLabel == null) return;

            int count = 0;
            long bytes = 0;
            if (suggestionRows != null)
            {
                for (int i = 0; i < suggestionRows.Count; i++)
                {
                    CleanupSuggestionRow row = suggestionRows[i];
                    if (row == null || row.Suggestion == null || !row.selected || row.Suggestion.Status == CleanupStatus.Deleted) continue;
                    count++;
                    bytes += row.Suggestion.Bytes;
                }
            }

            suggestionSelectionLabel.Text = "已选 " + count + " 项";
            suggestionSelectionSizeLabel.Text = count > 0 ? StorageFormatting.FormatBytes(bytes) : string.Empty;
            if (suggestionRecycleButton != null) suggestionRecycleButton.Enabled = count > 0;
            if (suggestionPermDeleteButton != null) suggestionPermDeleteButton.Enabled = count > 0;
        }

        private void DeleteSelectedSuggestionsWithMode(bool useRecycleBin)
        {
            if (busy) return;

            // 按钮是显式选择删除方式：直接作为当前删除策略，确认与执行链路按其运行
            settings.Sandbox.UseRecycleBin = useRecycleBin;
            DeleteSelectedSuggestions();
        }

        // 旧建议页控件的兼容桩：删除/分析流程仍读写这些字段，不再进布局
        private void EnsureSuggestionCompatControls()
        {
            suggestionTable = new AntdUI.Table();
            suggestionDriveSelect = new AntdUI.Select();
            suggestionMinSizeInput = new AntdUI.Input();
            suggestionMinSizeInput.Text = "128";
            suggestionLimitInput = new AntdUI.Input();
            suggestionLimitInput.Text = "-1";
            privilegedQuickCheckbox = new AntdUI.Checkbox();
            privilegedQuickCheckbox.CheckedChanged += PrivilegedCheckbox_CheckedChanged;
            regularCleanButton = new AntdUI.Button();
            superCleanButton = new AntdUI.Button();
            deleteButton = new AntdUI.Button();
            suggestionPromptButton = new AntdUI.Button();
            selectAllSuggestionsButton = new AntdUI.Button();
            clearAllSuggestionsButton = new AntdUI.Button();
            invertSuggestionsButton = new AntdUI.Button();
        }
    }
}
