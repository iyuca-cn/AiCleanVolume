using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using AiCleanVolume.Core.Domain.Cleanup;
using AiCleanVolume.Core.Domain.Sandbox;
using AiCleanVolume.Core.Domain.Settings;
using AiCleanVolume.Core.Domain.Storage;
using AiCleanVolume.Core.Application.CleanupPlanning;
using AiCleanVolume.Core.Application.Deletion;
using AiCleanVolume.Core.Application.Scanning;
using AiCleanVolume.Core.Kernel.Ports;
using AiCleanVolume.NativeBridge;
using Newtonsoft.Json;


namespace AiCleanVolume.Desktop.Infrastructure.Scanning
{
    public sealed partial class FolderSizeRankerScanProvider : IScanProvider
    {
        private const int DefaultChildWindowSize = 512;

        private void ClearAllTreeSessionsNoLock()
        {
            foreach (ScanSession session in treeSessions.Values)
            {
                session.Dispose();
            }
            treeSessions.Clear();
            sessionUsageOrder.Clear();
        }

        private sealed class ScanSession : IDisposable
        {
            public string RootPath { get; set; }
            public string TemplateKey { get; set; }
            public string CacheKey { get; set; }
            public string SessionIdentity { get; set; }
            public int RootNodeId { get; set; }
            public NativeMftScanSession NativeSession { get; set; }

            public void Dispose()
            {
                if (NativeSession != null)
                {
                    NativeSession.Dispose();
                    NativeSession = null;
                }
            }
        }
    }
}
