using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using AiCleanVolume.Core.Domain.Ai;
using AiCleanVolume.Core.Domain.Storage;
using AiCleanVolume.Core.Application.Scanning;
using AiCleanVolume.Desktop.Presentation.Shared;
using AiCleanVolume.Desktop.Presentation.Shared.Antd;
using AiCleanVolume.Desktop.ViewModels;
using Newtonsoft.Json;

namespace AiCleanVolume.Desktop
{
    // 中栏 AI 解析：扫描完成自动生成整盘报告 + 多轮对话 + 文件夹附件提问。
    public sealed partial class MainWindow : AntdUI.Window
    {
        private const int MaxChatAttachments = 4;

        private AntdUI.Panel chatEmptyPanel;

        private AntdUI.Panel chatWaitingPanel;

        private AntdUI.Panel chatMainPanel;

        private AntdUI.Panel reportCardPanel;

        private AntdUI.Label reportSafeValueLabel;

        private AntdUI.Label reportConfirmValueLabel;

        private AntdUI.Label reportSystemValueLabel;

        private AntdUI.Label reportSummaryLabel;

        private AntdUI.Label chatTokensLabel;

        private AntdUI.Chat.ChatList chatList;

        private AntdUI.Input chatInput;

        private AntdUI.Button chatSendButton;

        private FlowLayoutPanel attachChipRow;

        private readonly List<AiChatMessage> chatHistory = new List<AiChatMessage>();

        private readonly List<string> chatAttachments = new List<string>();

        private readonly Dictionary<string, long> chatAttachmentBytes = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);

        private int chatTokensTotal;

        private bool aiChatBusy;

        private void BuildAiChatColumn()
        {
            AntdUI.Panel headerRow = CreateFlatPanel();
            headerRow.Dock = DockStyle.Top;
            headerRow.Height = 52;
            headerRow.Padding = new Padding(20, 12, 20, 4);

            AntdUI.Label titleLabel = new AntdUI.Label();
            titleLabel.Dock = DockStyle.Left;
            titleLabel.AutoSizeMode = AntdUI.TAutoSize.Width;
            titleLabel.Text = "AI 解析";
            titleLabel.Font = new Font(Font.FontFamily, 10.5F, FontStyle.Bold);
            titleLabel.ForeColor = Palette.TextPrimary;

            AntdUI.Label subtitleLabel = new AntdUI.Label();
            subtitleLabel.Dock = DockStyle.Left;
            subtitleLabel.AutoSizeMode = AntdUI.TAutoSize.Width;
            subtitleLabel.Text = "  基于扫描结果的智能清理分析 · 支持附加文件夹提问";
            subtitleLabel.Font = new Font(Font.FontFamily, 8.5F);
            subtitleLabel.ForeColor = Palette.TextMuted;

            chatTokensLabel = new AntdUI.Label();
            chatTokensLabel.Dock = DockStyle.Right;
            chatTokensLabel.AutoSizeMode = AntdUI.TAutoSize.Width;
            chatTokensLabel.Font = AntdControlFactory.MonoFont(8.5F);
            chatTokensLabel.ForeColor = Palette.TextMuted;
            chatTokensLabel.Text = string.Empty;

            headerRow.Controls.Add(chatTokensLabel);
            headerRow.Controls.Add(subtitleLabel);
            headerRow.Controls.Add(titleLabel);

            chatEmptyPanel = BuildChatGuidePanel("AI 解析尚未开始", "选择磁盘 → 开始扫描 → AI 自动生成整盘解析报告。\r\n之后可把左侧文件夹「询问 AI」，或在下方输入框直接提问。");
            chatWaitingPanel = BuildChatGuidePanel("等待扫描结果", "扫描完成后将自动把存储结构摘要发送给 AI，\r\n生成整盘解析报告。仅发送目录统计，不上传文件内容。");
            chatWaitingPanel.Visible = false;

            chatMainPanel = CreateFlatPanel();
            chatMainPanel.Dock = DockStyle.Fill;
            chatMainPanel.Visible = false;
            chatMainPanel.Padding = new Padding(20, 0, 20, 0);

            reportCardPanel = BuildReportCard();
            chatList = new AntdUI.Chat.ChatList();
            chatList.Dock = DockStyle.Fill;
            chatList.BackColor = Palette.Page;

            chatMainPanel.Controls.Add(chatList);
            chatMainPanel.Controls.Add(reportCardPanel);
            chatList.BringToFront();

            AntdUI.Panel inputHost = BuildChatInputArea();

            centerColumnPanel.Controls.Add(chatMainPanel);
            centerColumnPanel.Controls.Add(chatWaitingPanel);
            centerColumnPanel.Controls.Add(chatEmptyPanel);
            centerColumnPanel.Controls.Add(inputHost);
            centerColumnPanel.Controls.Add(headerRow);
            chatMainPanel.BringToFront();
            chatWaitingPanel.BringToFront();
            chatEmptyPanel.BringToFront();
        }

        private AntdUI.Panel BuildChatGuidePanel(string title, string description)
        {
            AntdUI.Panel host = CreateFlatPanel();
            host.Dock = DockStyle.Fill;
            host.BackColor = Palette.Page;

            AntdUI.Panel card = new AntdUI.Panel();
            card.Size = new Size(380, 190);
            card.Radius = 12;
            card.Back = Palette.SurfaceFaint;
            card.BorderWidth = 1F;
            card.BorderColor = Palette.Border;
            card.Shadow = 0;
            card.Padding = new Padding(24, 24, 24, 16);

            AntdUI.Label titleLabel = new AntdUI.Label();
            titleLabel.Dock = DockStyle.Top;
            titleLabel.Height = 34;
            titleLabel.Text = title;
            titleLabel.Font = new Font(Font.FontFamily, 11F, FontStyle.Bold);
            titleLabel.ForeColor = Palette.TextPrimary;
            titleLabel.BackColor = Color.Transparent;
            titleLabel.TextAlign = ContentAlignment.MiddleCenter;

            AntdUI.Label descLabel = new AntdUI.Label();
            descLabel.Dock = DockStyle.Fill;
            descLabel.Text = description;
            descLabel.Font = new Font(Font.FontFamily, 9F);
            descLabel.ForeColor = Palette.TextMuted;
            descLabel.BackColor = Color.Transparent;
            descLabel.TextAlign = ContentAlignment.TopCenter;

            card.Controls.Add(descLabel);
            card.Controls.Add(titleLabel);
            descLabel.BringToFront();

            host.Controls.Add(card);
            host.Resize += delegate
            {
                card.Left = (host.ClientSize.Width - card.Width) / 2;
                card.Top = (host.ClientSize.Height - card.Height) / 2 - 40;
            };
            return host;
        }

        private AntdUI.Panel BuildReportCard()
        {
            AntdUI.Panel card = new AntdUI.Panel();
            card.Dock = DockStyle.Top;
            card.Height = 170;
            card.Radius = 12;
            card.Back = Palette.Surface;
            card.BorderWidth = 1F;
            card.BorderColor = Palette.Border;
            card.Shadow = 0;
            card.Padding = new Padding(18, 12, 18, 10);
            card.Margin = new Padding(0, 4, 0, 8);

            AntdUI.Label captionLabel = new AntdUI.Label();
            captionLabel.Dock = DockStyle.Top;
            captionLabel.Height = 22;
            captionLabel.Text = "扫描解析报告";
            captionLabel.Font = new Font(Font.FontFamily, 8.5F, FontStyle.Bold);
            captionLabel.ForeColor = Palette.Accent;

            AntdUI.GridPanel statsGrid = AntdControlFactory.CreateGridPanel("66:33% 33% 34%");
            statsGrid.Dock = DockStyle.Top;
            statsGrid.Height = 70;
            statsGrid.BackColor = Color.Transparent;

            reportSafeValueLabel = null;
            reportConfirmValueLabel = null;
            reportSystemValueLabel = null;
            AntdControlFactory.AddGridControl(statsGrid, BuildReportStatTile("可安全释放", Palette.AccentSoft, Palette.AccentSoftBorder, Palette.Accent, out reportSafeValueLabel), 0);
            AntdControlFactory.AddGridControl(statsGrid, BuildReportStatTile("需人工确认", Palette.WarningSoft, Palette.WarningBorder, Palette.Warning, out reportConfirmValueLabel), 1);
            AntdControlFactory.AddGridControl(statsGrid, BuildReportStatTile("系统保留 · 勿动", Palette.CardFill, Palette.Border, Palette.Ink, out reportSystemValueLabel), 2);

            reportSummaryLabel = new AntdUI.Label();
            reportSummaryLabel.Dock = DockStyle.Fill;
            reportSummaryLabel.Font = new Font(Font.FontFamily, 9F);
            reportSummaryLabel.ForeColor = Palette.TextSecondary;
            reportSummaryLabel.Text = "扫描完成后自动生成报告…";
            reportSummaryLabel.Padding = new Padding(0, 6, 0, 0);

            card.Controls.Add(reportSummaryLabel);
            card.Controls.Add(statsGrid);
            card.Controls.Add(captionLabel);
            reportSummaryLabel.BringToFront();
            return card;
        }

        private Control BuildReportStatTile(string caption, Color back, Color border, Color valueColor, out AntdUI.Label valueLabel)
        {
            AntdUI.Panel tile = new AntdUI.Panel();
            tile.Radius = 9;
            tile.Back = back;
            tile.BorderWidth = 1F;
            tile.BorderColor = border;
            tile.Shadow = 0;
            tile.Padding = new Padding(12, 6, 12, 4);
            tile.Margin = new Padding(0, 0, 8, 0);

            valueLabel = new AntdUI.Label();
            valueLabel.Dock = DockStyle.Top;
            valueLabel.Height = 30;
            valueLabel.Text = "—";
            valueLabel.Font = AntdControlFactory.MonoFontBold(13F);
            valueLabel.ForeColor = valueColor;

            AntdUI.Label captionLabel = new AntdUI.Label();
            captionLabel.Dock = DockStyle.Top;
            captionLabel.Height = 20;
            captionLabel.Text = caption;
            captionLabel.Font = new Font(Font.FontFamily, 8.5F);
            captionLabel.ForeColor = Palette.TextSecondary;

            tile.Controls.Add(captionLabel);
            tile.Controls.Add(valueLabel);
            captionLabel.BringToFront();
            return tile;
        }

        private AntdUI.Panel BuildChatInputArea()
        {
            AntdUI.Panel host = CreateFlatPanel();
            host.Dock = DockStyle.Bottom;
            host.Height = 96;
            host.BackColor = Palette.Page;
            host.Padding = new Padding(20, 4, 20, 14);

            attachChipRow = new FlowLayoutPanel();
            attachChipRow.Dock = DockStyle.Top;
            attachChipRow.Height = 30;
            attachChipRow.BackColor = Color.Transparent;
            attachChipRow.WrapContents = false;
            attachChipRow.AutoScroll = false;
            attachChipRow.Visible = false;

            AntdUI.Panel inputRow = CreateFlatPanel();
            inputRow.Dock = DockStyle.Bottom;
            inputRow.Height = 44;

            chatInput = new AntdUI.Input();
            chatInput.Dock = DockStyle.Fill;
            chatInput.PlaceholderText = "问问 AI：哪些可以清理？也可附加文件夹后提问";
            chatInput.Radius = 10;
            chatInput.BorderWidth = 1F;
            chatInput.BorderColor = Palette.Border;
            chatInput.BorderHover = Palette.Accent;
            chatInput.BorderActive = Palette.Accent;
            chatInput.BackColor = Palette.Surface;
            chatInput.KeyDown += ChatInput_KeyDown;

            AntdUI.Button pickFolderButton = new AntdUI.Button();
            pickFolderButton.Dock = DockStyle.Right;
            pickFolderButton.AutoSizeMode = AntdUI.TAutoSize.None;
            pickFolderButton.Width = 40;
            pickFolderButton.IconSvg = "FolderAddOutlined";
            pickFolderButton.IconRatio = 0.72F;
            pickFolderButton.Radius = 10;
            pickFolderButton.BorderWidth = 1F;
            pickFolderButton.DefaultBorderColor = Palette.Border;
            pickFolderButton.Margin = new Padding(8, 0, 0, 0);
            pickFolderButton.Click += PickChatFolder_Click;

            chatSendButton = new AntdUI.Button();
            chatSendButton.Dock = DockStyle.Right;
            chatSendButton.AutoSizeMode = AntdUI.TAutoSize.Width;
            chatSendButton.Text = "发送";
            chatSendButton.IconSvg = "SendOutlined";
            chatSendButton.IconRatio = 0.7F;
            chatSendButton.Type = AntdUI.TTypeMini.Primary;
            chatSendButton.Radius = 10;
            chatSendButton.Margin = new Padding(8, 0, 0, 0);
            chatSendButton.Click += delegate { SendChatMessage(); };

            inputRow.Controls.Add(chatInput);
            inputRow.Controls.Add(pickFolderButton);
            inputRow.Controls.Add(chatSendButton);
            chatInput.BringToFront();

            host.Controls.Add(inputRow);
            host.Controls.Add(attachChipRow);
            return host;
        }

        private void SetAiChatState(StorageTreeState state)
        {
            if (chatEmptyPanel == null) return;
            chatEmptyPanel.Visible = state == StorageTreeState.Empty;
            chatWaitingPanel.Visible = state == StorageTreeState.Scanning;
            chatMainPanel.Visible = state == StorageTreeState.Done;
        }

        private void ChatInput_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode != Keys.Enter || e.Shift) return;
            e.Handled = true;
            e.SuppressKeyPress = true;
            SendChatMessage();
        }

        private void PickChatFolder_Click(object sender, EventArgs e)
        {
            using (FolderBrowserDialog dialog = new FolderBrowserDialog())
            {
                dialog.Description = "选择要交给 AI 分析的文件夹";
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                AddChatAttachment(dialog.SelectedPath, -1);
            }
        }

        /// <summary>左树勾选/右键入口：把路径附加到提问。父目录吞并子目录，最多 4 个。</summary>
        internal void AddChatAttachment(string path, long bytes)
        {
            if (string.IsNullOrWhiteSpace(path)) return;
            path = path.TrimEnd('\\');

            for (int i = 0; i < chatAttachments.Count; i++)
            {
                if (string.Equals(chatAttachments[i], path, StringComparison.OrdinalIgnoreCase)) return;
                if (path.StartsWith(chatAttachments[i] + "\\", StringComparison.OrdinalIgnoreCase))
                {
                    AntdUI.Message.warn(this, "其上级文件夹「" + chatAttachments[i] + "」已附加，无需重复添加", autoClose: 3);
                    return;
                }
            }

            int swallowed = chatAttachments.RemoveAll(delegate (string existing)
            {
                return existing.StartsWith(path + "\\", StringComparison.OrdinalIgnoreCase);
            });

            if (chatAttachments.Count >= MaxChatAttachments)
            {
                AntdUI.Message.warn(this, "最多同时附加 " + MaxChatAttachments + " 个文件夹", autoClose: 3);
                return;
            }

            chatAttachments.Add(path);
            chatAttachmentBytes[path] = bytes;
            if (swallowed > 0) AntdUI.Message.info(this, "已附加上级文件夹，自动合并了 " + swallowed + " 个子文件夹", autoClose: 3);
            RebuildAttachChips();
        }

        private void RebuildAttachChips()
        {
            attachChipRow.Controls.Clear();
            attachChipRow.Visible = chatAttachments.Count > 0;

            for (int i = 0; i < chatAttachments.Count; i++)
            {
                string path = chatAttachments[i];
                AntdUI.Tag chip = new AntdUI.Tag();
                chip.Text = CompactDeletionPath(path, 42) + RenderAttachmentSize(path);
                chip.Font = AntdControlFactory.MonoFont(8F);
                chip.BackColor = Palette.AccentSoft;
                chip.ForeColor = Palette.AccentText;
                chip.BorderWidth = 1F;
                chip.Radius = 7;
                chip.CloseIcon = true;
                string captured = path;
                chip.CloseChanged += delegate
                {
                    chatAttachments.Remove(captured);
                    chatAttachmentBytes.Remove(captured);
                    RebuildAttachChips();
                    return true;
                };
                attachChipRow.Controls.Add(chip);
            }
        }

        private string RenderAttachmentSize(string path)
        {
            long bytes;
            if (chatAttachmentBytes.TryGetValue(path, out bytes) && bytes >= 0)
            {
                return "  " + StorageFormatting.FormatBytes(bytes);
            }

            return string.Empty;
        }

        private void SendChatMessage()
        {
            if (aiChatBusy)
            {
                AntdUI.Message.warn(this, "AI 正在思考，请稍候", autoClose: 3);
                return;
            }

            string question = chatInput.Text == null ? string.Empty : chatInput.Text.Trim();
            if (question.Length == 0 && chatAttachments.Count == 0) return;
            if (question.Length == 0) question = "请分析这些文件夹里哪些内容可以安全清理。";

            SetAiChatState(StorageTreeState.Done);

            string userContent = BuildUserMessageContent(question);
            chatInput.Text = string.Empty;

            AntdUI.Chat.TextChatItem userBubble = new AntdUI.Chat.TextChatItem(BuildUserBubbleText(question));
            userBubble.Me = true;
            chatList.AddToBottom(userBubble, true);

            chatAttachments.Clear();
            chatAttachmentBytes.Clear();
            RebuildAttachChips();

            EnsureChatSystemPrompt();
            chatHistory.Add(new AiChatMessage(AiChatMessage.UserRole, userContent));
            RequestChatCompletion(null);
        }

        private string BuildUserBubbleText(string question)
        {
            if (chatAttachments.Count == 0) return question;

            StringBuilder text = new StringBuilder(question);
            text.Append("\r\n");
            for (int i = 0; i < chatAttachments.Count; i++)
            {
                text.Append("\r\n📁 ").Append(chatAttachments[i]).Append(RenderAttachmentSize(chatAttachments[i]));
            }

            return text.ToString();
        }

        private string BuildUserMessageContent(string question)
        {
            if (chatAttachments.Count == 0) return question;

            StringBuilder content = new StringBuilder(question);
            content.Append("\r\n\r\n附加文件夹（含两层子目录统计）：");
            for (int i = 0; i < chatAttachments.Count; i++)
            {
                content.Append("\r\n").Append(BuildDirectorySummary(chatAttachments[i], 2, 12));
            }

            return content.ToString();
        }

        private void EnsureChatSystemPrompt()
        {
            if (chatHistory.Count > 0 && chatHistory[0].Role == AiChatMessage.SystemRole) return;

            string driveRoot = GetPromptDriveRoot();
            string prompt = "你是 Windows 磁盘清理顾问，帮助用户理解磁盘占用并判断哪些内容可以安全清理。" +
                "回答用简体中文，简明扼要，重点给出可执行的清理建议与风险提示。" +
                "只讨论 " + driveRoot + " 范围内的内容。不要建议删除系统核心文件、应用主体和用户文档。";
            chatHistory.Insert(0, new AiChatMessage(AiChatMessage.SystemRole, prompt));
        }

        // 通用对话请求：占位气泡 → 后台补全 → 回填/降级
        private void RequestChatCompletion(Action<string> onSuccess)
        {
            AntdUI.Chat.TextChatItem aiBubble = new AntdUI.Chat.TextChatItem("…");
            aiBubble.Loading = true;
            chatList.AddToBottom(aiBubble, true);

            aiChatBusy = true;
            AiChatResult result = null;
            List<AiChatMessage> snapshot = new List<AiChatMessage>(chatHistory);
            backgroundWorker.Enqueue(delegate
            {
                try
                {
                    result = aiAdvisor.Complete(snapshot, settings);
                }
                catch (Exception ex)
                {
                    result = AiChatResult.Fail(ex.Message);
                }

                BeginInvoke((MethodInvoker)delegate
                {
                    if (IsDisposed) return;
                    aiChatBusy = false;
                    aiBubble.Loading = false;
                    if (result != null && result.Success)
                    {
                        aiBubble.Text = result.Content;
                        chatHistory.Add(new AiChatMessage(AiChatMessage.AssistantRole, result.Content));
                        chatTokensTotal += result.TotalTokens;
                        if (chatTokensLabel != null && chatTokensTotal > 0) chatTokensLabel.Text = "tokens " + chatTokensTotal.ToString("N0");
                        if (onSuccess != null) onSuccess(result.Content);
                    }
                    else
                    {
                        aiBubble.Text = "请求失败：" + (result == null ? "未知错误" : result.Error);
                    }
                });
            });
        }

        /// <summary>扫描完成后自动生成整盘解析报告（AI 未启用则静默跳过）。</summary>
        private void StartAiScanReport()
        {
            if (settings == null || settings.Ai == null || !settings.Ai.Enabled || currentRoot == null) return;
            if (aiChatBusy) return;

            chatHistory.Clear();
            chatTokensTotal = 0;
            if (chatList != null) chatList.Items.Clear();
            EnsureChatSystemPrompt();

            string structureSummary = BuildDirectorySummary(currentRoot.Path, 2, 40);
            string reportPrompt = "以下是刚完成的磁盘扫描目录统计。请输出严格 JSON（不要任何解释文字、不要代码块标记）：" +
                "{\"safe_bytes\":整数,\"confirm_bytes\":整数,\"system_bytes\":整数,\"summary\":\"120字以内的中文总结\"," +
                "\"classified\":[{\"path\":\"目录完整路径\",\"tag\":\"safe|confirm|system\"}]}。" +
                "safe=可安全清理（缓存/临时/日志等可再生成内容），confirm=需人工确认，system=系统保留勿动。" +
                "classified 只包含你能明确判断的顶层目录，path 必须取自下方统计中出现的路径。\r\n\r\n" + structureSummary;

            chatHistory.Add(new AiChatMessage(AiChatMessage.UserRole, reportPrompt));
            RequestChatCompletion(ApplyAiScanReport);
        }

        private void ApplyAiScanReport(string content)
        {
            AiScanReportDto report = null;
            try
            {
                string json = content.Trim();
                int start = json.IndexOf('{');
                int end = json.LastIndexOf('}');
                if (start >= 0 && end > start) json = json.Substring(start, end - start + 1);
                report = JsonConvert.DeserializeObject<AiScanReportDto>(json);
            }
            catch
            {
                report = null;
            }

            if (report == null)
            {
                // JSON 不守约：正文已作为气泡展示，报告卡片保持占位
                if (reportSummaryLabel != null) reportSummaryLabel.Text = "AI 未返回结构化报告，请查看对话内容。";
                return;
            }

            if (reportSafeValueLabel != null) reportSafeValueLabel.Text = StorageFormatting.FormatBytes(Math.Max(0L, report.safe_bytes));
            if (reportConfirmValueLabel != null) reportConfirmValueLabel.Text = StorageFormatting.FormatBytes(Math.Max(0L, report.confirm_bytes));
            if (reportSystemValueLabel != null) reportSystemValueLabel.Text = StorageFormatting.FormatBytes(Math.Max(0L, report.system_bytes));
            if (reportSummaryLabel != null && !string.IsNullOrWhiteSpace(report.summary)) reportSummaryLabel.Text = report.summary;

            ApplyAiClassificationTags(report.classified);
            UpdateReclaimEstimate(Math.Max(0L, report.safe_bytes));
        }

        private void ApplyAiClassificationTags(List<AiScanReportEntryDto> classified)
        {
            if (classified == null || classified.Count == 0 || storageTable == null) return;
            List<StorageEntryRow> rootRows = storageTable.DataSource as List<StorageEntryRow>;
            if (rootRows == null) return;

            Dictionary<string, string> tagByPath = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < classified.Count; i++)
            {
                if (!string.IsNullOrWhiteSpace(classified[i].path) && !string.IsNullOrWhiteSpace(classified[i].tag))
                {
                    tagByPath[classified[i].path.TrimEnd('\\')] = classified[i].tag.Trim().ToLowerInvariant();
                }
            }

            for (int i = 0; i < rootRows.Count; i++) ApplyAiClassificationTag(rootRows[i], tagByPath);
            storageTable.Refresh();
        }

        private static void ApplyAiClassificationTag(StorageEntryRow row, Dictionary<string, string> tagByPath)
        {
            if (row == null || row.Item == null) return;

            string tag;
            if (!string.IsNullOrWhiteSpace(row.Item.Path) && tagByPath.TryGetValue(row.Item.Path.TrimEnd('\\'), out tag))
            {
                if (tag == "safe") row.SetCleanupTag("可安全清理", AntdUI.TTypeMini.Success);
                else if (tag == "confirm") row.SetCleanupTag("需确认", AntdUI.TTypeMini.Warn);
                else if (tag == "system") row.SetCleanupTag("系统保留", AntdUI.TTypeMini.Default);
            }

            for (int i = 0; i < row.Children.Count; i++)
            {
                StorageEntryRow child = row.Children[i] as StorageEntryRow;
                if (child != null) ApplyAiClassificationTag(child, tagByPath);
            }
        }

        // 目录统计摘要：只发名称/大小/文件数，不发文件内容
        private string BuildDirectorySummary(string rootPath, int depth, int perLevelLimit)
        {
            StringBuilder summary = new StringBuilder();
            try
            {
                ScanRequest request = currentTreeRequest != null
                    ? CreateScanRequest(rootPath, depth, currentTreeRequest)
                    : new ScanRequest { Location = rootPath, LoadDepth = depth, SortMode = ResolveSelectedSizeMode(), MinSizeBytes = -1, PerLevelLimit = perLevelLimit };
                request.LoadDepth = depth;
                request.PerLevelLimit = perLevelLimit;
                if (currentTreeRequest != null && !IsSameRootPath(rootPath))
                {
                    request.SessionIdentity = null;
                    request.SessionNodeId = -1;
                }

                StorageItem item = scanProvider.Scan(request);
                AppendDirectorySummary(summary, item, 0, perLevelLimit);
            }
            catch (Exception ex)
            {
                summary.Append(rootPath).Append("（统计失败：").Append(ex.Message).Append("）");
            }

            if (summary.Length > 8000) summary.Length = 8000;
            return summary.ToString();
        }

        private bool IsSameRootPath(string path)
        {
            return currentRoot != null && string.Equals(
                currentRoot.Path == null ? null : currentRoot.Path.TrimEnd('\\'),
                path == null ? null : path.TrimEnd('\\'),
                StringComparison.OrdinalIgnoreCase);
        }

        private static void AppendDirectorySummary(StringBuilder summary, StorageItem item, int indent, int perLevelLimit)
        {
            if (item == null) return;
            if (summary.Length > 7600) return;

            summary.Append('\r').Append('\n');
            for (int i = 0; i < indent; i++) summary.Append("  ");
            summary.Append(indent == 0 ? item.Path : item.Name)
                .Append("  ").Append(StorageFormatting.FormatBytes(item.Bytes));
            if (item.IsDirectory && item.TotalFileCount > 0) summary.Append("  ").Append(item.TotalFileCount).Append("文件");

            if (!item.IsDirectory || item.Children == null) return;
            int emitted = 0;
            for (int i = 0; i < item.Children.Count && emitted < perLevelLimit; i++)
            {
                AppendDirectorySummary(summary, item.Children[i], indent + 1, perLevelLimit);
                emitted++;
            }
        }

        private sealed class AiScanReportDto
        {
            public long safe_bytes { get; set; }
            public long confirm_bytes { get; set; }
            public long system_bytes { get; set; }
            public string summary { get; set; }
            public List<AiScanReportEntryDto> classified { get; set; }
        }

        private sealed class AiScanReportEntryDto
        {
            public string path { get; set; }
            public string tag { get; set; }
        }
    }
}
