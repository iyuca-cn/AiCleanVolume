using System.Threading;

namespace AiCleanVolume.Core.Application.Deletion
{
    public sealed class DeletionProgressState
    {
        private long version;
        private string stage;
        private string path;

        public DeletionProgressState()
        {
            stage = string.Empty;
            path = string.Empty;
        }

        public void Update(string stage, string path)
        {
            Interlocked.Exchange(ref this.stage, stage ?? string.Empty);
            Interlocked.Exchange(ref this.path, path ?? string.Empty);
            Interlocked.Increment(ref version);
        }

        public void Reset()
        {
            Update(string.Empty, string.Empty);
        }

        public DeletionProgressSnapshot Read()
        {
            long currentVersion = Interlocked.Read(ref version);
            string currentStage = Interlocked.CompareExchange(ref stage, null, null);
            string currentPath = Interlocked.CompareExchange(ref path, null, null);
            return new DeletionProgressSnapshot(currentVersion, currentStage, currentPath);
        }
    }

    public sealed class DeletionProgressSnapshot
    {
        public DeletionProgressSnapshot(long version, string stage, string path)
        {
            Version = version;
            Stage = stage ?? string.Empty;
            Path = path ?? string.Empty;
        }

        public long Version { get; private set; }

        public string Stage { get; private set; }

        public string Path { get; private set; }

        public bool HasText
        {
            get { return !string.IsNullOrWhiteSpace(Stage) || !string.IsNullOrWhiteSpace(Path); }
        }
    }
}
