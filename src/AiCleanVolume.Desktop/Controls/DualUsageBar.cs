using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using AiCleanVolume.Desktop.Presentation.Shared;

namespace AiCleanVolume.Desktop.Controls
{
    /// <summary>双色用量条：深色段=正常占用，青色段=AI 判定可清理，底槽=剩余。</summary>
    internal sealed class DualUsageBar : Control
    {
        private float usedRatio;
        private float reclaimRatio;

        public DualUsageBar()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            Height = 8;
        }

        /// <summary>正常占用比例（0~1，不含可清理段）。</summary>
        public float UsedRatio
        {
            get { return usedRatio; }
            set { usedRatio = Clamp(value); Invalidate(); }
        }

        /// <summary>AI 判定可清理比例（0~1，画在占用段之后）。</summary>
        public float ReclaimRatio
        {
            get { return reclaimRatio; }
            set { reclaimRatio = Clamp(value); Invalidate(); }
        }

        private static float Clamp(float value)
        {
            if (value < 0F) return 0F;
            if (value > 1F) return 1F;
            return value;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle rect = ClientRectangle;
            if (rect.Width <= 0 || rect.Height <= 0) return;

            int radius = rect.Height / 2;
            using (GraphicsPath track = RoundedRect(rect, radius))
            using (SolidBrush trackBrush = new SolidBrush(Palette.BarTrack))
            {
                g.FillPath(trackBrush, track);
                g.SetClip(track);

                float usedWidth = rect.Width * usedRatio;
                float reclaimWidth = rect.Width * Math.Min(reclaimRatio, 1F - usedRatio);
                if (usedWidth > 0F)
                {
                    using (SolidBrush usedBrush = new SolidBrush(Palette.Ink))
                    {
                        g.FillRectangle(usedBrush, rect.X, rect.Y, usedWidth, rect.Height);
                    }
                }

                if (reclaimWidth > 0F)
                {
                    using (SolidBrush reclaimBrush = new SolidBrush(Palette.Accent))
                    {
                        g.FillRectangle(reclaimBrush, rect.X + usedWidth, rect.Y, reclaimWidth, rect.Height);
                    }
                }

                g.ResetClip();
            }
        }

        private static GraphicsPath RoundedRect(Rectangle rect, int radius)
        {
            GraphicsPath path = new GraphicsPath();
            if (radius <= 0)
            {
                path.AddRectangle(rect);
                return path;
            }

            int d = radius * 2;
            path.AddArc(rect.X, rect.Y, d, d, 180, 90);
            path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
            path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
            path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }
    }
}
