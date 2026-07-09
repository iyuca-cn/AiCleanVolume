using System;
using System.Drawing;
using System.Drawing.Text;
using System.Windows.Forms;
using AiCleanVolume.Desktop.Composition;
using AiCleanVolume.Desktop.Presentation.Shared;
using AiCleanVolume.Desktop.Presentation.WebShell;

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
            AntdUI.Config.TextRenderingHighQuality = false;
            AntdUI.Config.ShadowEnabled = false;
            AntdUI.Config.Animation = true;
            AntdUI.Config.DisableAnimation(nameof(AntdUI.Table), nameof(AntdUI.Menu), nameof(AntdUI.ScrollBar));
            AntdUI.Config.SetCorrectionTextRendering("Microsoft YaHei UI", "微软雅黑", "宋体");
            AntdUI.Config.Theme()
                .Light(ToHex(Palette.Page), ToHex(Palette.TextPrimary))
                .Dark("#141414", "#f0f0f0")
                .Header(ToHex(Palette.Surface), "#141414")
                .FormBorderColor();
            ApplyMinimalTheme();
            Application.Run(new WebShellWindow(DesktopCompositionRoot.CreateDependencies()));
        }

        // 把极简色板同步到 AntdUI 主题令牌，凡是读取 Style.Db.* 的控件都会跟随。
        private static void ApplyMinimalTheme()
        {
            AntdUI.Style.SetPrimary(Palette.Accent);

            AntdUI.Style.Set(AntdUI.Colour.BgBase, Palette.Page);
            AntdUI.Style.Set(AntdUI.Colour.BgLayout, Palette.Page);
            AntdUI.Style.Set(AntdUI.Colour.BgContainer, Palette.Surface);
            AntdUI.Style.Set(AntdUI.Colour.BgElevated, Palette.Surface);

            AntdUI.Style.Set(AntdUI.Colour.Text, Palette.TextPrimary);
            AntdUI.Style.Set(AntdUI.Colour.TextBase, Palette.TextPrimary);
            AntdUI.Style.Set(AntdUI.Colour.TextSecondary, Palette.TextSecondary);
            AntdUI.Style.Set(AntdUI.Colour.TextTertiary, Palette.TextMuted);

            AntdUI.Style.Set(AntdUI.Colour.BorderColor, Palette.Border);
            AntdUI.Style.Set(AntdUI.Colour.BorderSecondary, Palette.Divider);
            AntdUI.Style.Set(AntdUI.Colour.Split, Palette.Divider);

            AntdUI.Style.Set(AntdUI.Colour.PrimaryBg, Palette.AccentSoft);
            AntdUI.Style.Set(AntdUI.Colour.PrimaryBorder, Palette.AccentSoftBorder);
            AntdUI.Style.Set(AntdUI.Colour.FillSecondary, Palette.CardFill);
            AntdUI.Style.Set(AntdUI.Colour.FillTertiary, Palette.CardFill);

            AntdUI.Style.Set(AntdUI.Colour.Success, Palette.Success);
            AntdUI.Style.Set(AntdUI.Colour.SuccessBg, Palette.SuccessSoft);
            AntdUI.Style.Set(AntdUI.Colour.Warning, Palette.Warning);
            AntdUI.Style.Set(AntdUI.Colour.WarningBg, Palette.WarningSoft);
            AntdUI.Style.Set(AntdUI.Colour.WarningBorder, Palette.WarningBorder);
            AntdUI.Style.Set(AntdUI.Colour.Error, Palette.Danger);
            AntdUI.Style.Set(AntdUI.Colour.ErrorBg, Palette.DangerSoft);
            AntdUI.Style.Set(AntdUI.Colour.ErrorBorder, Palette.DangerBorder);
        }

        private static string ToHex(Color color)
        {
            return "#" + color.R.ToString("X2") + color.G.ToString("X2") + color.B.ToString("X2");
        }
    }
}
