using System;
using System.Collections.Generic;

namespace AiCleanVolume.Core.Models
{
    public enum ScanSortMode
    {
        Logical,
        Allocated
    }

    public enum CleanupRisk
    {
        Low,
        Medium,
        High
    }

    public enum CleanupStatus
    {
        Pending,
        Deleted,
        Skipped,
        Failed
    }

    public enum SandboxAction
    {
        Allow,
        RequireConfirmation,
        Bypass
    }

    public sealed class ScanRequest
    {
        public ScanRequest()
        {
            SortMode = ScanSortMode.Allocated;
            MinSizeBytes = 104857600;
            PerLevelLimit = 50;
        }

        public string Location { get; set; }
        public ScanSortMode SortMode { get; set; }
        public long MinSizeBytes { get; set; }
        public int PerLevelLimit { get; set; }
    }

    public sealed class StorageItem
    {
        public StorageItem()
        {
            Children = new List<StorageItem>();
        }

        public string Path { get; set; }
        public string Name { get; set; }
        public long Bytes { get; set; }
        public bool IsDirectory { get; set; }
        public int DirectFileCount { get; set; }
        public int TotalFileCount { get; set; }
        public int TotalDirectoryCount { get; set; }
        public IList<StorageItem> Children { get; private set; }
    }

    public sealed class CleanupCandidate
    {
        public string Path { get; set; }
        public string Name { get; set; }
        public long Bytes { get; set; }
        public bool IsDirectory { get; set; }
        public CleanupRisk Risk { get; set; }
        public string ReasonHint { get; set; }
        public string Source { get; set; }
    }

    public sealed class CleanupSuggestion
    {
        public CleanupSuggestion()
        {
            Selected = true;
            Status = CleanupStatus.Pending;
        }

        public string Path { get; set; }
        public string Name { get; set; }
        public long Bytes { get; set; }
        public bool IsDirectory { get; set; }
        public CleanupRisk Risk { get; set; }
        public double Score { get; set; }
        public bool Selected { get; set; }
        public string Reason { get; set; }
        public string Source { get; set; }
        public CleanupStatus Status { get; set; }
        public string ErrorMessage { get; set; }
        public SandboxEvaluation Sandbox { get; set; }
    }

    public sealed class SandboxEvaluation
    {
        public SandboxAction Action { get; set; }
        public string Message { get; set; }
        public string MatchedRoot { get; set; }
    }

    public sealed class CleanupResult
    {
        public string Path { get; set; }
        public bool Success { get; set; }
        public string Message { get; set; }
        public Exception Exception { get; set; }
    }
}
