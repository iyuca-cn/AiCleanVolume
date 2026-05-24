using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using AiCleanVolume.Core.Models;
using AiCleanVolume.Core.Services;
using AiCleanVolume.Desktop.Controls;
using AiCleanVolume.Desktop.Services;
using AiCleanVolume.Desktop.ViewModels;


namespace AiCleanVolume.Desktop
{
    public sealed partial class MainWindow : AntdUI.Window
    {
        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == Keys.Delete && TryHandleStorageDeleteShortcut()) return true;
            return base.ProcessCmdKey(ref msg, keyData);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Delete && TryHandleStorageDeleteShortcut())
            {
                e.Handled = true;
                e.SuppressKeyPress = true;
                return;
            }

            base.OnKeyDown(e);
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            ApplySidebarWidth(ResolveInitialSidebarWidth());
            ApplyNormalWindowBounds(true);
            PerformLayout();
            BindInitialUiBeforeFirstFrame();
            lastWindowState = WindowState;
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            CompleteStartupPostShowRefresh();
            QueueStartupReveal();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (scanProgressTimer != null) scanProgressTimer.Dispose();
                if (backgroundWorker != null) backgroundWorker.Dispose();
            }
            base.Dispose(disposing);
        }

        private static void SuspendControlRedraw(Control control)
        {
            if (control == null || !control.IsHandleCreated) return;
            SendMessage(control.Handle, WmSetRedraw, IntPtr.Zero, IntPtr.Zero);
        }

        private static void ResumeControlRedraw(Control control)
        {
            ResumeControlRedraw(control, true, true);
        }

        private static void ResumeControlRedraw(Control control, bool invalidateChildren, bool updateImmediately)
        {
            if (control == null || !control.IsHandleCreated) return;
            SendMessage(control.Handle, WmSetRedraw, new IntPtr(1), IntPtr.Zero);
            control.Invalidate(invalidateChildren);
            if (updateImmediately) RedrawWindow(control.Handle, IntPtr.Zero, IntPtr.Zero, RestoreRedrawFlags);
        }

        [DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern bool RedrawWindow(IntPtr hWnd, IntPtr lprcUpdate, IntPtr hrgnUpdate, int flags);

        protected override void OnSizeChanged(EventArgs e)
        {
            FormWindowState previousState = lastWindowState;
            base.OnSizeChanged(e);
            if (!applyingNormalBounds && WindowState == FormWindowState.Normal && previousState != FormWindowState.Normal && IsHandleCreated)
            {
                if (previousState == FormWindowState.Minimized)
                {
                    QueueWindowRestoreCompletion();
                }
                else
                {
                    BeginInvoke((MethodInvoker)delegate { ApplyNormalWindowBounds(false); });
                }
            }
            lastWindowState = WindowState;
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WmEraseBackground)
            {
                PaintEraseBackground(m.WParam);
                m.Result = new IntPtr(1);
                return;
            }

            if (m.Msg == WmGetMinMaxInfo)
            {
                UpdateMaximizedBounds(m.LParam);
                m.Result = IntPtr.Zero;
                return;
            }

            bool restoreFromMinimized = IsRestoreFromMinimizedSizeMessage(m);

            base.WndProc(ref m);

            if (restoreFromMinimized && !restoreBoundsQueued)
            {
                ForceWindowRestoreRepaint();
                QueueWindowRestoreCompletion();
            }
        }

        private bool IsRestoreFromMinimizedSizeMessage(Message message)
        {
            if (message.Msg != WmSize || lastWindowState != FormWindowState.Minimized) return false;
            return message.WParam == SizeRestored || message.WParam == SizeMaximized;
        }

        private void QueueWindowRestoreCompletion()
        {
            if (restoreBoundsQueued) return;
            restoreBoundsQueued = true;
            BeginInvoke((MethodInvoker)delegate
            {
                try
                {
                    if (WindowState == FormWindowState.Normal) ApplyNormalWindowBounds(false);
                }
                finally
                {
                    restoreBoundsQueued = false;
                    ForceWindowRestoreRepaint();
                }
            });
        }

        private void ForceWindowRestoreRepaint()
        {
            if (!IsHandleCreated || IsDisposed || WindowState == FormWindowState.Minimized) return;
            PerformLayout();
            RefreshDWM();
            Invalidate(true);
            RedrawWindow(Handle, IntPtr.Zero, IntPtr.Zero, RestoreRedrawFlags);
        }

        private void PaintEraseBackground(IntPtr hdc)
        {
            if (hdc == IntPtr.Zero) return;
            using (Graphics graphics = Graphics.FromHdc(hdc))
            using (SolidBrush brush = new SolidBrush(PageBackground))
            {
                graphics.FillRectangle(brush, ClientRectangle);
            }
        }

        private void ResumeStartupRedraw()
        {
            if (startupRedrawCompleted) return;
            if (!startupRedrawSuspended)
            {
                startupRedrawCompleted = true;
                return;
            }

            startupRedrawSuspended = false;
            startupRedrawCompleted = true;
            ResumeControlRedraw(this, true, false);
        }

        private void QueueStartupReveal()
        {
            if (startupRedrawCompleted || startupRevealQueued || IsDisposed) return;
            startupRevealQueued = true;
            BeginInvoke((MethodInvoker)delegate
            {
                startupRevealQueued = false;
                CompleteStartupReveal();
            });
        }

        private void CompleteStartupReveal()
        {
            if (startupRedrawCompleted || IsDisposed) return;

            if (IsHandleCreated)
            {
                ApplyNormalWindowBounds(true);
                PerformLayout();
                ResumeStartupRedraw();
                RefreshDWM();
                Update();
            }
            else
            {
                startupRedrawCompleted = true;
            }
        }

        private void CompleteStartupPostShowRefresh()
        {
            if (startupPostShowRefreshCompleted || IsDisposed) return;
            startupPostShowRefreshCompleted = true;
            UpdateDriveSummaryForLocation(ResolveSelectedLocation());
            RefreshPromptForCurrentLocation();
            Log("应用已启动。若 AI 未启用，将自动回退到本地启发式规则。");
        }

        private void UpdateMaximizedBounds(IntPtr lParam)
        {
            if (lParam == IntPtr.Zero) return;

            MinMaxInfo info = (MinMaxInfo)Marshal.PtrToStructure(lParam, typeof(MinMaxInfo));
            Screen screen = Screen.FromHandle(Handle);
            Rectangle monitorArea = screen.Bounds;
            Rectangle workArea = screen.WorkingArea;

            info.MaxPosition.X = workArea.Left - monitorArea.Left;
            info.MaxPosition.Y = workArea.Top - monitorArea.Top;
            info.MaxSize.X = workArea.Width;
            info.MaxSize.Y = workArea.Height;
            Size minimumSize = GetConstrainedMinimumSize(workArea);
            info.MinTrackSize.X = minimumSize.Width;
            info.MinTrackSize.Y = minimumSize.Height;

            Marshal.StructureToPtr(info, lParam, false);
        }

        private void ApplyNormalWindowBounds(bool centerWhenShrunk)
        {
            if (!IsHandleCreated || WindowState != FormWindowState.Normal) return;

            Rectangle currentBounds = Bounds;
            Screen screen = Screen.FromRectangle(currentBounds);
            Rectangle workArea = screen.WorkingArea;
            Size constrainedMinimum = GetConstrainedMinimumSize(workArea);
            if (!MinimumSize.Equals(constrainedMinimum)) MinimumSize = constrainedMinimum;

            int width = Clamp(currentBounds.Width, constrainedMinimum.Width, workArea.Width);
            int height = Clamp(currentBounds.Height, constrainedMinimum.Height, workArea.Height);

            int left;
            int top;
            bool shrunk = width != currentBounds.Width || height != currentBounds.Height;
            if (centerWhenShrunk && shrunk)
            {
                left = workArea.Left + Math.Max(0, (workArea.Width - width) / 2);
                top = workArea.Top + Math.Max(0, (workArea.Height - height) / 2);
            }
            else
            {
                left = Clamp(currentBounds.Left, workArea.Left, workArea.Right - width);
                top = Clamp(currentBounds.Top, workArea.Top, workArea.Bottom - height);
            }

            Rectangle normalizedBounds = new Rectangle(left, top, width, height);
            if (normalizedBounds.Equals(currentBounds)) return;

            applyingNormalBounds = true;
            try
            {
                Bounds = normalizedBounds;
            }
            finally
            {
                applyingNormalBounds = false;
            }
        }

        private static Size GetConstrainedMinimumSize(Rectangle workArea)
        {
            int width = Math.Min(BaseMinimumWindowSize.Width, Math.Max(1, workArea.Width));
            int height = Math.Min(BaseMinimumWindowSize.Height, Math.Max(1, workArea.Height));
            return new Size(width, height);
        }

        private static int Clamp(int value, int min, int max)
        {
            if (max < min) return min;
            if (value < min) return min;
            if (value > max) return max;
            return value;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct NativePoint
        {
            public int X;
            public int Y;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MinMaxInfo
        {
            public NativePoint Reserved;
            public NativePoint MaxSize;
            public NativePoint MaxPosition;
            public NativePoint MinTrackSize;
            public NativePoint MaxTrackSize;
        }
    }
}
