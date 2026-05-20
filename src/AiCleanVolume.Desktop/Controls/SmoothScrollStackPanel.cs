using System;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace AiCleanVolume.Desktop.Controls
{
    internal sealed class SmoothScrollStackPanel : AntdUI.StackPanel
    {
        private const int WheelStep = 50;
        private const int WmSetRedraw = 0x000B;

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            if (!TryWheel(e))
            {
                base.OnMouseWheel(e);
            }
        }

        private bool TryWheel(MouseEventArgs e)
        {
            if (e == null || e.Delta == 0 || ScrollBar == null || !ScrollBar.EnabledY || !ScrollBar.ShowY) return false;

            int currentValueY = ScrollBar.ValueY;
            int wheelDistance = Math.Max(1, (int)Math.Round(Math.Abs(e.Delta) * WheelStep / (double)SystemInformation.MouseWheelScrollDelta));
            int direction = e.Delta > 0 ? -1 : 1;
            int nextValueY = ClampValueY(currentValueY + direction * wheelDistance);

            if (nextValueY != currentValueY) SetScrollValueY(nextValueY);
            MarkHandled(e);
            return true;
        }

        private int ClampValueY(int value)
        {
            if (ScrollBar == null) return 0;
            if (value < 0) return 0;

            int maxValue = Math.Max(0, ScrollBar.VrValueI);
            return value > maxValue ? maxValue : value;
        }

        private static void MarkHandled(MouseEventArgs e)
        {
            if (e is HandledMouseEventArgs handled) handled.Handled = true;
        }

        private void SetScrollValueY(int value)
        {
            if (ScrollBar == null) return;

            bool suspendRedraw = IsHandleCreated;
            if (suspendRedraw) SendMessage(Handle, WmSetRedraw, IntPtr.Zero, IntPtr.Zero);

            try
            {
                ScrollBar.ValueY = value;
            }
            finally
            {
                if (suspendRedraw)
                {
                    SendMessage(Handle, WmSetRedraw, new IntPtr(1), IntPtr.Zero);
                    Invalidate(true);
                    Update();
                }
            }
        }

        [DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);
    }
}
