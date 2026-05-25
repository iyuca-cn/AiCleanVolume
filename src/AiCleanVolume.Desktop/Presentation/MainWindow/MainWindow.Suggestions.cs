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
using AiCleanVolume.Desktop.Presentation.Features.Scan;
using AiCleanVolume.Desktop.Presentation.Shared;
using AiCleanVolume.Desktop.ViewModels;


namespace AiCleanVolume.Desktop
{
    public sealed partial class MainWindow : AntdUI.Window
    {
        private void AnalyzeSuggestions()
        {
            AnalyzeSuggestionsCore(true);
        }

        private void AnalyzeRegularSuggestions()
        {
            AnalyzeConfiguredPathSuggestions();
        }

        private void AnalyzeSuperSuggestions()
        {
            AnalyzeSuggestionsCore(false);
        }

        private void AnalyzeConfiguredPathSuggestions()
        {
            SaveSettingsFromUi();
            IList<CleanupSuggestion> suggestions = null;
            DateTime analyzeStartedAt = DateTime.UtcNow;
            string caption = "正在汇总常规清理路径…";
            Log("常规清理开始：仅使用内置和已配置允许位置进行汇总清理。");

            RunBackground(caption, delegate
            {
                int maxCount = settings != null && settings.Ai != null ? settings.Ai.MaxSuggestions : 30;
                suggestions = configuredPathCleanupPlanner.BuildSuggestions(settings, maxCount);
                LogBackground("常规路径汇总完成：count=" + (suggestions == null ? 0 : suggestions.Count) + "。");
                EvaluateSandbox(suggestions);
            }, delegate
            {
                BindSuggestions(suggestions);
                TimeSpan elapsed = DateTime.UtcNow - analyzeStartedAt;
                Log("常规清理生成完成，共 " + suggestionRows.Count + " 项，耗时 " + elapsed.TotalSeconds.ToString("0.00") + " 秒。");
            });
        }

        private void AnalyzeSuggestionsCore(bool preferAi)
        {
            string location = ResolveSuggestionLocation();
            if (NeedAutoScanBeforeAnalyze(location))
            {
                string actionName = preferAi ? "AI 识别" : "超级清理";
                Log("未发现当前所选位置的扫描结果，先自动扫描：" + location);
                ScanSuggestionLocation(location, delegate
                {
                    Log("自动扫描完成，继续执行" + actionName + "。");
                    AnalyzeSuggestionsCore(preferAi);
                }, "未发现当前所选位置的扫描结果，正在自动扫描...");
                return;
            }

            SaveSettingsFromUi();
            if (preferAi && !settings.Ai.Enabled && IsAiConfigured(settings.Ai))
            {
                settings.Ai.Enabled = true;
                aiEnabledSwitch.Checked = true;
                Log("AI 配置已填写，自动启用 AI 识别。");
            }
            IList<CleanupSuggestion> suggestions = null;
            StorageItem analysisRoot = null;
            ScanRequest request = BuildSuggestionScanRequest(location, -1);
            string caption = preferAi ? "正在生成 AI 清理建议…" : "正在生成超级清理列表…";
            DateTime analyzeStartedAt = DateTime.UtcNow;
            Log((preferAi ? "AI 识别" : "超级清理") + "开始：" + ScanPageText.DescribeRequest(request) + "，AIEnabled=" + settings.Ai.Enabled + "，AccessMode=" + settings.Ai.AccessMode + "，Endpoint=" + settings.Ai.Endpoint + "，Model=" + settings.Ai.Model + "，CookieMappings=" + (settings.Ai.ModelCookieMappings == null ? 0 : settings.Ai.ModelCookieMappings.Count) + "。");

            RunBackground(caption, delegate
            {
                analysisRoot = scanProvider.Scan(request);
                LogBackground("候选构建开始：root=" + (analysisRoot == null ? string.Empty : analysisRoot.Path) + "，rootSize=" + (analysisRoot == null ? string.Empty : StorageFormatting.FormatBytes(analysisRoot.Bytes)) + "。");
                IList<CleanupCandidate> candidates = candidatePlanner.BuildCandidates(
                    analysisRoot,
                    ResolveCandidateMinBytes(preferAi),
                    settings.Ai.MaxSuggestions * (preferAi ? 4 : 6));
                LogBackground("候选构建完成：count=" + candidates.Count + "，minBytes=" + StorageFormatting.FormatBytes(ResolveCandidateMinBytes(preferAi)) + "。");
                suggestions = preferAi ? aiAdvisor.Analyze(analysisRoot, candidates, settings) : localAdvisor.Analyze(analysisRoot, candidates, settings);
                LogBackground((preferAi ? "AI/回退" : "超级") + "建议原始结果：count=" + (suggestions == null ? 0 : suggestions.Count) + "。");
                EvaluateSandbox(suggestions);
            }, delegate
            {
                BindSuggestions(suggestions);
                string sourceName;
                if (preferAi) sourceName = settings.Ai.Enabled ? "AI 建议" : "本地规则回退";
                else sourceName = "超级清理";
                TimeSpan elapsed = DateTime.UtcNow - analyzeStartedAt;
                Log(sourceName + "生成完成，共 " + suggestionRows.Count + " 项，耗时 " + elapsed.TotalSeconds.ToString("0.00") + " 秒。");
            });
        }

        private void EvaluateSandbox(IList<CleanupSuggestion> suggestions)
        {
            if (suggestions == null) return;
            for (int i = 0; i < suggestions.Count; i++)
            {
                suggestions[i].Sandbox = deletionWorkflow.Evaluate(suggestions[i].Path, settings.Sandbox);
            }
        }

        private void BindSuggestions(IList<CleanupSuggestion> suggestions)
        {
            suggestionRows = new List<CleanupSuggestionRow>();
            if (suggestions != null)
            {
                for (int i = 0; i < suggestions.Count; i++) suggestionRows.Add(new CleanupSuggestionRow(suggestions[i]));
            }
            suggestionTable.DataSource = suggestionRows;
        }

        private void RefreshSuggestionSandboxFromCurrentSettings()
        {
            if (suggestionRows == null || suggestionRows.Count == 0 || settings == null || settings.Sandbox == null) return;

            for (int i = 0; i < suggestionRows.Count; i++)
            {
                CleanupSuggestionRow row = suggestionRows[i];
                row.Suggestion.Sandbox = deletionWorkflow.Evaluate(row.Suggestion.Path, settings.Sandbox);
                row.RefreshSandbox();
            }

            if (suggestionTable != null) suggestionTable.Refresh();
        }

        private bool NeedAutoScanBeforeAnalyze(string location)
        {
            if (currentRoot == null) return true;
            return !IsSamePath(currentRoot.Path, location);
        }

        private void SetSuggestionSelection(bool selected)
        {
            if (suggestionRows == null || suggestionRows.Count == 0) return;

            for (int i = 0; i < suggestionRows.Count; i++)
            {
                CleanupSuggestionRow row = suggestionRows[i];
                if (row == null || row.Suggestion == null || row.Suggestion.Status == CleanupStatus.Deleted) continue;
                row.selected = selected;
            }

            if (suggestionTable != null) suggestionTable.Refresh();
        }

        private void InvertSuggestionSelection()
        {
            if (suggestionRows == null || suggestionRows.Count == 0) return;

            for (int i = 0; i < suggestionRows.Count; i++)
            {
                CleanupSuggestionRow row = suggestionRows[i];
                if (row == null || row.Suggestion == null || row.Suggestion.Status == CleanupStatus.Deleted) continue;
                row.selected = !row.selected;
            }

            if (suggestionTable != null) suggestionTable.Refresh();
        }

        private void SuggestionTable_CellDoubleClick(object sender, AntdUI.TableClickEventArgs e)
        {
            CleanupSuggestionRow row = e.Record as CleanupSuggestionRow;
            if (row == null) return;
            explorerService.OpenPath(row.path, !row.Suggestion.IsDirectory);
        }

        private void SuggestionTable_CellButtonClick(object sender, AntdUI.TableButtonEventArgs e)
        {
            CleanupSuggestionRow row = e.Record as CleanupSuggestionRow;
            if (row == null) return;
            string key = e.Btn == null ? null : e.Btn.Id;
            if (string.Equals(key, "delete", StringComparison.OrdinalIgnoreCase)) DeleteSingleSuggestion(row);
            else explorerService.OpenPath(row.path, !row.Suggestion.IsDirectory);
        }

        private long ResolveCandidateMinBytes(bool preferAi)
        {
            long configured = settings != null && settings.Scan != null && settings.Scan.MinSizeMb > 0
                ? settings.Scan.MinSizeMb * 1024L * 1024L / 2L
                : -1L;
            long baseline = preferAi ? 67108864L : 16777216L;
            return Math.Max(baseline, configured);
        }
    }
}
