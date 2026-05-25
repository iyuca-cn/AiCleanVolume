using System;

namespace AiCleanVolume.Core.Domain.Cleanup
{
    public sealed class CleanupResult
    {
        public string Path { get; set; }
        public bool Success { get; set; }
        public string Message { get; set; }
        public Exception Exception { get; set; }
    }
}
