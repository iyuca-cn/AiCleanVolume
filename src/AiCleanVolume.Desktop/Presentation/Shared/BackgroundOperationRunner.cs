using System;
using System.Windows.Forms;

namespace AiCleanVolume.Desktop.Presentation.Shared
{
    public sealed class BackgroundOperationRunner
    {
        private readonly ReusableBackgroundWorker worker;
        private readonly IMainWindowShell shell;
        private readonly Control uiDispatcher;
        private readonly Func<string> getIdleDescription;

        public BackgroundOperationRunner(ReusableBackgroundWorker worker, IMainWindowShell shell, Control uiDispatcher, Func<string> getIdleDescription)
        {
            if (worker == null) throw new ArgumentNullException("worker");
            if (shell == null) throw new ArgumentNullException("shell");
            if (uiDispatcher == null) throw new ArgumentNullException("uiDispatcher");

            this.worker = worker;
            this.shell = shell;
            this.uiDispatcher = uiDispatcher;
            this.getIdleDescription = getIdleDescription;
        }

        public void Run(string caption, Action action, Action onSuccess)
        {
            Run(caption, action, onSuccess, null);
        }

        public void Run(string caption, Action action, Action onSuccess, Action onError)
        {
            shell.SetBusy(true, caption);
            Exception error = null;
            worker.Enqueue(delegate
            {
                try
                {
                    if (action != null) action();
                }
                catch (Exception ex)
                {
                    error = ex;
                }

                if (uiDispatcher.IsDisposed) return;
                uiDispatcher.BeginInvoke((MethodInvoker)delegate
                {
                    shell.SetBusy(false, getIdleDescription == null ? string.Empty : getIdleDescription());
                    if (error != null)
                    {
                        if (onError != null) onError();
                        shell.Log(caption + "失败：" + error.Message + Environment.NewLine + error);
                        shell.ShowError("操作失败", error.Message);
                        return;
                    }

                    if (onSuccess != null) onSuccess();
                });
            });
        }
    }
}
