using System;
using System.Collections.Generic;
using System.Threading;

namespace AiCleanVolume.Desktop.Services
{
    public sealed class ReusableBackgroundWorker : IDisposable
    {
        private readonly object syncRoot = new object();
        private readonly Queue<Action> queue = new Queue<Action>();
        private readonly Thread workerThread;

        private bool disposed;
        private bool stopping;

        public ReusableBackgroundWorker(string name)
        {
            workerThread = new Thread(WorkerLoop);
            workerThread.IsBackground = true;
            workerThread.Name = string.IsNullOrWhiteSpace(name) ? "AiCleanVolume.BackgroundWorker" : name;
            workerThread.Start();
        }

        public void Enqueue(Action action)
        {
            if (action == null) throw new ArgumentNullException("action");

            lock (syncRoot)
            {
                ThrowIfDisposed();
                queue.Enqueue(action);
                Monitor.Pulse(syncRoot);
            }
        }

        public void Dispose()
        {
            lock (syncRoot)
            {
                if (disposed) return;
                disposed = true;
                stopping = true;
                Monitor.PulseAll(syncRoot);
            }
        }

        private void WorkerLoop()
        {
            while (true)
            {
                Action action = null;

                lock (syncRoot)
                {
                    while (!stopping && queue.Count == 0)
                    {
                        Monitor.Wait(syncRoot);
                    }

                    if (stopping && queue.Count == 0) return;
                    if (queue.Count > 0) action = queue.Dequeue();
                }

                if (action == null) continue;

                try
                {
                    action();
                }
                catch
                {
                }
            }
        }

        private void ThrowIfDisposed()
        {
            if (disposed) throw new ObjectDisposedException(GetType().Name);
        }
    }
}
