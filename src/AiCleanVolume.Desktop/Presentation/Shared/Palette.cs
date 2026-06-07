using System.Drawing;

namespace AiCleanVolume.Desktop.Presentation.Shared
{
    // 极简主题色板：纯蓝强调 + 暖灰底 + 微底色块卡片 + 近黑文字，去硬边框去阴影。
    // 这里集中定义所有界面用色，AntdUI 主题令牌在 Program.cs 中按此同步覆盖。
    internal static class Palette
    {
        // 强调色（纯蓝）
        public static readonly Color Accent = Color.FromArgb(0x1E, 0x88, 0xE5);          // #1E88E5
        public static readonly Color AccentHover = Color.FromArgb(0x42, 0xA5, 0xF5);     // #42A5F5
        public static readonly Color AccentActive = Color.FromArgb(0x15, 0x65, 0xC0);    // #1565C0
        public static readonly Color AccentSoft = Color.FromArgb(0xE3, 0xF2, 0xFD);      // #E3F2FD 选中/标签底
        public static readonly Color AccentSoftBorder = Color.FromArgb(0xBB, 0xDE, 0xFB);// #BBDEFB

        // 背景层级
        public static readonly Color Page = Color.FromArgb(0xFC, 0xFC, 0xFD);            // #FCFCFD 暖灰底
        public static readonly Color Surface = Color.White;                              // 白色表面（表格/输入/嵌套卡片）
        public static readonly Color CardFill = Color.FromArgb(0xF4, 0xF4, 0xF7);        // #F4F4F7 微底色块卡片
        public static readonly Color CardFillHover = Color.FromArgb(0xEE, 0xEE, 0xF3);   // #EEEEF3

        // 文字
        public static readonly Color TextPrimary = Color.FromArgb(0x1C, 0x1C, 0x28);     // #1C1C28 近黑
        public static readonly Color TextSecondary = Color.FromArgb(0x52, 0x52, 0x5B);   // #52525B
        public static readonly Color TextMuted = Color.FromArgb(0x8E, 0x8E, 0x99);       // #8E8E99

        // 线条
        public static readonly Color Border = Color.FromArgb(0xE6, 0xE6, 0xEC);          // #E6E6EC 输入/下拉细线
        public static readonly Color Divider = Color.FromArgb(0xEC, 0xEC, 0xEF);         // #ECECEF 分割线

        // 表格
        public static readonly Color TableHeader = Color.FromArgb(0xF4, 0xF4, 0xF7);     // #F4F4F7
        public static readonly Color TableHeaderText = Color.FromArgb(0x6B, 0x6B, 0x76); // #6B6B76
        public static readonly Color RowHover = Color.FromArgb(0xF5, 0xF5, 0xFB);        // #F5F5FB
        public static readonly Color RowSelected = Color.FromArgb(0xEC, 0xEC, 0xFB);     // #ECECFB
    }
}
