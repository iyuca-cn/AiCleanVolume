using System.Drawing;

namespace AiCleanVolume.Desktop.Presentation.Shared
{
    // 主题色板：青色强调 + 深藏青标题/状态栏 + 冷灰底 + 语义色（成功绿/警告琥珀/危险红）。
    // 这里集中定义所有界面用色，AntdUI 主题令牌在 Program.cs 中按此同步覆盖。
    internal static class Palette
    {
        // 强调色（青）
        public static readonly Color Accent = Color.FromArgb(0x0E, 0x74, 0x90);           // #0E7490
        public static readonly Color AccentHover = Color.FromArgb(0x14, 0xB8, 0xC4);      // #14B8C4
        public static readonly Color AccentActive = Color.FromArgb(0x0B, 0x5E, 0x63);     // #0B5E63
        public static readonly Color AccentSoft = Color.FromArgb(0xF0, 0xF7, 0xF8);       // #F0F7F8 选中/标签底
        public static readonly Color AccentSoftBorder = Color.FromArgb(0xC4, 0xE0, 0xE5); // #C4E0E5
        public static readonly Color AccentText = Color.FromArgb(0x0B, 0x5E, 0x63);       // #0B5E63 软底上的强调文字

        // 深色条（标题栏/状态栏）
        public static readonly Color TitleBar = Color.FromArgb(0x10, 0x20, 0x2C);         // #10202C
        public static readonly Color TitleBarText = Color.FromArgb(0xC7, 0xD3, 0xDC);     // #C7D3DC
        public static readonly Color TitleBarMuted = Color.FromArgb(0x7E, 0x93, 0xA2);    // #7E93A2 状态栏次要文字
        public static readonly Color TitleBarFaint = Color.FromArgb(0x56, 0x70, 0x7F);    // #56707F 版本号等弱化文字

        // 语义色
        public static readonly Color Success = Color.FromArgb(0x0F, 0x7A, 0x56);          // #0F7A56
        public static readonly Color SuccessSoft = Color.FromArgb(0xE4, 0xF3, 0xEC);      // #E4F3EC
        public static readonly Color Warning = Color.FromArgb(0x9A, 0x5B, 0x00);          // #9A5B00
        public static readonly Color WarningSoft = Color.FromArgb(0xFB, 0xF0, 0xDC);      // #FBF0DC
        public static readonly Color WarningBorder = Color.FromArgb(0xEB, 0xD9, 0xB4);    // #EBD9B4
        public static readonly Color Danger = Color.FromArgb(0xA6, 0x3A, 0x4B);           // #A63A4B
        public static readonly Color DangerSoft = Color.FromArgb(0xFB, 0xF1, 0xF2);       // #FBF1F2
        public static readonly Color DangerBorder = Color.FromArgb(0xE3, 0xC9, 0xCE);     // #E3C9CE

        // 图表/用量条
        public static readonly Color Ink = Color.FromArgb(0x33, 0x47, 0x5A);              // #33475A 正常占用段
        public static readonly Color BarTrack = Color.FromArgb(0xEA, 0xEE, 0xF1);         // #EAEEF1 条底槽

        // 背景层级
        public static readonly Color Page = Color.FromArgb(0xF6, 0xF8, 0xF9);             // #F6F8F9 冷灰底
        public static readonly Color Surface = Color.White;                               // 白色表面（表格/输入/卡片）
        public static readonly Color CardFill = Color.FromArgb(0xF2, 0xF4, 0xF6);         // #F2F4F6 微底色块
        public static readonly Color CardFillHover = Color.FromArgb(0xED, 0xF1, 0xF4);    // #EDF1F4
        public static readonly Color SurfaceFaint = Color.FromArgb(0xFB, 0xFC, 0xFD);     // #FBFCFD 底部操作栏底

        // 文字
        public static readonly Color TextPrimary = Color.FromArgb(0x1A, 0x25, 0x30);      // #1A2530
        public static readonly Color TextSecondary = Color.FromArgb(0x42, 0x58, 0x6A);    // #42586A
        public static readonly Color TextMuted = Color.FromArgb(0x6B, 0x80, 0x90);        // #6B8090
        public static readonly Color TextFaint = Color.FromArgb(0x8C, 0xA0, 0xAD);        // #8CA0AD

        // 线条
        public static readonly Color Border = Color.FromArgb(0xE1, 0xE7, 0xEB);           // #E1E7EB
        public static readonly Color Divider = Color.FromArgb(0xE9, 0xEE, 0xF1);          // #E9EEF1

        // 表格
        public static readonly Color TableHeader = Color.FromArgb(0xF2, 0xF4, 0xF6);      // #F2F4F6
        public static readonly Color TableHeaderText = Color.FromArgb(0x5E, 0x74, 0x85);  // #5E7485
        public static readonly Color RowHover = Color.FromArgb(0xF2, 0xF6, 0xF8);         // #F2F6F8
        public static readonly Color RowSelected = Color.FromArgb(0xE2, 0xF0, 0xF2);      // #E2F0F2
    }
}
