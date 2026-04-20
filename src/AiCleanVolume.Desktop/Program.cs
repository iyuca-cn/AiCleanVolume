using System;
using System.Drawing;
using System.Drawing.Text;
using System.Windows.Forms;

namespace AiCleanVolume.Desktop
{
    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            AntdUI.Config.IsLight = true;
            AntdUI.Config.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
            AntdUI.Config.TextRenderingHighQuality = true;
            AntdUI.Config.SetCorrectionTextRendering("Microsoft YaHei UI", "微软雅黑", "宋体");
            AntdUI.Config.Theme()
                .Light("#f5f5f5", "#1f1f1f")
                .Header("#ffffff", "#141414");
            AntdUI.Style.SetPrimary(Color.FromArgb(22, 119, 255));
            Application.Run(new MainWindow());
        }
    }
}
