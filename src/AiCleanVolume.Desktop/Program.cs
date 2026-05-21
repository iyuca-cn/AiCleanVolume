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
            AntdUI.Config.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
            AntdUI.Config.TextRenderingHighQuality = true;
            AntdUI.Config.ShadowEnabled = true;
            AntdUI.Config.Animation = true;
            AntdUI.Config.SetCorrectionTextRendering("Microsoft YaHei UI", "微软雅黑", "宋体");
            AntdUI.Config.Theme()
                .Light("#ffffff", "#1f1f1f")
                .Dark("#141414", "#f0f0f0")
                .Header("#ffffff", "#141414")
                .FormBorderColor();
            AntdUI.Style.SetPrimary(Color.FromArgb(22, 119, 255));
            Application.Run(new MainWindow());
        }
    }
}
