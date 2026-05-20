using System;

namespace AiCleanVolume.Core.Models
{
    public sealed class ApplicationSettings
    {
        public ApplicationSettings()
        {
            Ai = new AiSettings();
            Sandbox = new SandboxSettings();
            Scan = new ScanSettings();
            Ui = new UiSettings();
        }

        public AiSettings Ai { get; set; }
        public SandboxSettings Sandbox { get; set; }
        public ScanSettings Scan { get; set; }
        public UiSettings Ui { get; set; }

        public void EnsureDefaults()
        {
            if (Ai == null) Ai = new AiSettings();
            if (Sandbox == null) Sandbox = new SandboxSettings();
            if (Scan == null) Scan = new ScanSettings();
            if (Ui == null) Ui = new UiSettings();
            Ai.EnsureDefaults();
            Sandbox.EnsureDefaults();
            Scan.EnsureDefaults();
            Ui.EnsureDefaults();
        }
    }
}
