using AiCleanVolume.Core.Models;
using AiCleanVolume.Core.Services;

namespace AiCleanVolume.Desktop.ViewModels
{
    public sealed class CleanupSuggestionRow : AntdUI.NotifyProperty
    {
        public CleanupSuggestionRow(CleanupSuggestion suggestion)
        {
            Suggestion = suggestion;
            selected = suggestion.Selected;
            name = suggestion.Name;
            path = suggestion.Path;
            size = StorageFormatting.FormatBytes(suggestion.Bytes);
            source = suggestion.Source;
            details = BuildDetailsText(suggestion);
            category = CreateCategoryBadge(suggestion);
            risk = CreateRiskBadge(suggestion.Risk);
            sandbox = CreateSandboxBadge(suggestion.Sandbox);
            status = CreateStatusBadge(suggestion.Status);
            actions = new AntdUI.CellLink[]
            {
                new AntdUI.CellButton("open", "查看", AntdUI.TTypeMini.Primary).SetBorder().SetGhost(),
                new AntdUI.CellButton("delete", "删除", AntdUI.TTypeMini.Error).SetBorder().SetGhost()
            };
        }

        public CleanupSuggestion Suggestion { get; private set; }

        private bool selectedValue;
        public bool selected
        {
            get { return selectedValue; }
            set
            {
                if (selectedValue == value) return;
                selectedValue = value;
                Suggestion.Selected = value;
                OnPropertyChanged("selected");
            }
        }

        public string name { get; set; }
        public string path { get; set; }
        public string size { get; set; }
        public string source { get; set; }
        public string details { get; set; }
        public AntdUI.CellBadge category { get; set; }
        public AntdUI.CellBadge risk { get; set; }
        public AntdUI.CellBadge sandbox { get; set; }
        public AntdUI.CellBadge status { get; set; }
        public AntdUI.CellLink[] actions { get; set; }

        public void RefreshSandbox()
        {
            sandbox = CreateSandboxBadge(Suggestion.Sandbox);
            OnPropertyChanged("sandbox");
        }

        public void SetStatus(CleanupStatus newStatus, string message)
        {
            Suggestion.Status = newStatus;
            Suggestion.ErrorMessage = message;
            status = CreateStatusBadge(newStatus);
            if (!string.IsNullOrWhiteSpace(message))
            {
                details = BuildDetailsText(Suggestion);
                OnPropertyChanged("details");
            }
            OnPropertyChanged("status");
        }

        private static string BuildDetailsText(CleanupSuggestion suggestion)
        {
            if (suggestion == null) return string.Empty;

            string message = suggestion.Reason;
            if (!string.IsNullOrWhiteSpace(suggestion.ErrorMessage)) message = string.IsNullOrWhiteSpace(message) ? suggestion.ErrorMessage : message + "；" + suggestion.ErrorMessage;
            if (string.IsNullOrWhiteSpace(message)) return suggestion.Path;
            return suggestion.Path + "\r\n" + message;
        }

        private static AntdUI.CellBadge CreateCategoryBadge(CleanupSuggestion suggestion)
        {
            string lower = ((suggestion == null ? null : suggestion.Path) ?? string.Empty).ToLowerInvariant();
            if (lower.Contains("\\cache") || lower.Contains("\\caches") || lower.Contains("\\webcache") || lower.Contains("\\inetcache"))
            {
                return new AntdUI.CellBadge(AntdUI.TState.Processing, "缓存");
            }

            if (lower.Contains("\\temp") || lower.Contains("\\tmp"))
            {
                return new AntdUI.CellBadge(AntdUI.TState.Primary, "临时文件");
            }

            if (lower.Contains("\\log") || lower.EndsWith(".log") || lower.EndsWith(".etl"))
            {
                return new AntdUI.CellBadge(AntdUI.TState.Warn, "日志");
            }

            if (lower.Contains("\\dump") || lower.Contains("\\crash") || lower.EndsWith(".dmp"))
            {
                return new AntdUI.CellBadge(AntdUI.TState.Error, "转储");
            }

            if (lower.Contains("\\softwaredistribution") || lower.Contains("\\installer") || lower.Contains("\\setup"))
            {
                return new AntdUI.CellBadge(AntdUI.TState.Success, "安装残留");
            }

            if (lower.Contains("\\downloads\\") || lower.Contains("\\desktop\\"))
            {
                return new AntdUI.CellBadge(AntdUI.TState.Warn, "需人工确认");
            }

            return new AntdUI.CellBadge(AntdUI.TState.Default, "常规项");
        }

        private static AntdUI.CellBadge CreateRiskBadge(CleanupRisk risk)
        {
            if (risk == CleanupRisk.Low) return new AntdUI.CellBadge(AntdUI.TState.Success, "低风险");
            if (risk == CleanupRisk.Medium) return new AntdUI.CellBadge(AntdUI.TState.Warn, "需确认");
            return new AntdUI.CellBadge(AntdUI.TState.Error, "高风险");
        }

        private static AntdUI.CellBadge CreateSandboxBadge(SandboxEvaluation sandbox)
        {
            if (sandbox == null) return new AntdUI.CellBadge(AntdUI.TState.Default, "未评估");
            if (sandbox.Action == SandboxAction.Allow) return new AntdUI.CellBadge(AntdUI.TState.Success, "沙盒放行");
            if (sandbox.Action == SandboxAction.Bypass) return new AntdUI.CellBadge(AntdUI.TState.Warn, "完全权限");
            return new AntdUI.CellBadge(AntdUI.TState.Warn, "需确认");
        }

        private static AntdUI.CellBadge CreateStatusBadge(CleanupStatus status)
        {
            if (status == CleanupStatus.Deleted) return new AntdUI.CellBadge(AntdUI.TState.Success, "已删除");
            if (status == CleanupStatus.Failed) return new AntdUI.CellBadge(AntdUI.TState.Error, "失败");
            if (status == CleanupStatus.Skipped) return new AntdUI.CellBadge(AntdUI.TState.Default, "已跳过");
            return new AntdUI.CellBadge(AntdUI.TState.Processing, "待处理");
        }
    }
}
