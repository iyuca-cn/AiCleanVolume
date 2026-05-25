using System;
using System.Collections.Generic;
using System.Windows.Forms;
using AiCleanVolume.Core.Domain.Storage;
using AiCleanVolume.Desktop.ViewModels;

namespace AiCleanVolume.Desktop.Presentation.Features.Scan
{
    public sealed class ScanPageState
    {
        public ScanPageState()
        {
            ExpandedStoragePaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }

        public StorageItem CurrentRoot { get; set; }
        public ScanRequest CurrentTreeRequest { get; set; }
        public int CurrentTreeVersion { get; set; }
        public StorageEntryRow StorageContextRow { get; set; }
        public bool StorageTreeDeleteDirty { get; set; }
        public HashSet<string> ExpandedStoragePaths { get; private set; }
        public Timer ScanProgressTimer { get; set; }
        public DateTime ScanProgressStartedAt { get; set; }
        public string ScanProgressActiveText { get; set; }
    }
}
