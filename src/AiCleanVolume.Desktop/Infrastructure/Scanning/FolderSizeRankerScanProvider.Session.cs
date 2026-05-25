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
using Newtonsoft.Json;


namespace AiCleanVolume.Desktop.Infrastructure.Scanning
{
    public sealed partial class FolderSizeRankerScanProvider : IScanProvider
    {
        private static readonly int[] EmptyChildIds = new int[0];

        private static readonly FileNodeState[] EmptyFiles = new FileNodeState[0];

        private void ClearCurrentTreeSessionNoLock()
        {
            if (currentTreeSession == null) return;
            currentTreeSession.Dispose();
            currentTreeSession = null;
        }

        private sealed class ScanSession : IDisposable
        {
            public string RootPath { get; set; }
            public string TemplateKey { get; set; }
            public string SessionIdentity { get; set; }
            public int RootNodeId { get; set; }
            public List<DirectoryNodeState> Directories { get; set; }

            public void Dispose()
            {
                Directories = null;
            }
        }

        private sealed class DirectoryNodeState
        {
            public int NodeId { get; set; }
            public int ParentNodeId { get; set; }
            public string Name { get; set; }
            public long Bytes { get; set; }
            public int DirectFileCount { get; set; }
            public int TotalFileCount { get; set; }
            public int TotalDirectoryCount { get; set; }
            public FileNodeState[] DirectFiles { get; set; }
            public int[] DirectChildNodeIds { get; set; }
        }

        private struct FileNodeState
        {
            public string Name { get; set; }
            public long Bytes { get; set; }
        }

        private sealed class CliExecutionException : Exception
        {
            public CliExecutionException(string cliError)
                : base(cliError)
            {
                CliError = cliError;
            }

            public string CliError { get; private set; }
        }
    }
}
