using System;
using System.Drawing;
using System.Windows.Forms;
using AiCleanVolume.Core.Domain.Settings;
using AiCleanVolume.Desktop.Presentation.Shared;
using AiCleanVolume.Desktop.Presentation.Shared.Antd;

namespace AiCleanVolume.Desktop.Presentation.Features.Settings
{
    public static class AiProfileCardFactory
    {
        private static Color SurfaceColor { get { return AntdUI.Style.Db.BgContainer; } }

        private static Color BorderDefaultColor { get { return AntdUI.Style.Db.BorderColor; } }

        private static Color PrimaryColor { get { return AntdUI.Style.Db.Primary; } }

        private static Color TextPrimaryColor { get { return AntdUI.Style.Db.Text; } }

        public static Control CreateEmptyCard(AntdUI.StackPanel listPanel)
        {
            AntdUI.Panel card = CreateCardSurface(listPanel, false);
            card.Height = 88;

            AntdUI.Label label = CreateSmallMutedLabel("还没有保存过 AI 配置。填写接入参数后点击“保存为配置”，这里会生成类似接口列表的配置卡片。");
            label.Dock = DockStyle.Fill;
            label.TextAlign = ContentAlignment.MiddleCenter;
            card.Controls.Add(label);
            return card;
        }

        public static Control CreateProfileCard(
            AntdUI.StackPanel listPanel,
            AiProfile profile,
            int index,
            bool selected,
            Action<int> selectRequested,
            Action<int> applyRequested,
            Action<int> editRequested,
            Action<int> deleteRequested)
        {
            AntdUI.Panel card = CreateCardSurface(listPanel, selected);

            AntdUI.GridPanel layout = AntdControlFactory.CreateGridPanel("54 fill 122");
            layout.Dock = DockStyle.Fill;
            layout.BackColor = Color.Transparent;

            AntdUI.Panel avatarCell = AntdControlFactory.CreateFlatPanel();
            avatarCell.Dock = DockStyle.Fill;
            avatarCell.BackColor = Color.Transparent;
            AntdUI.Avatar avatar = new AntdUI.Avatar();
            avatar.Width = 40;
            avatar.Height = 40;
            avatar.Left = 3;
            avatar.Top = 20;
            avatar.Text = AiSettingsText.BuildProfileAvatarText(profile);
            avatar.Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Bold);
            avatar.ForeColor = PrimaryColor;
            avatar.BackColor = Palette.AccentSoft;
            avatar.BorderWidth = 0F;
            avatar.Radius = 18;
            avatarCell.Controls.Add(avatar);

            AntdUI.GridPanel content = AntdControlFactory.CreateGridPanel("fill;fill;fill-30 24 fill");
            content.Dock = DockStyle.Fill;
            content.BackColor = Color.Transparent;

            AntdUI.GridPanel titleRow = AntdControlFactory.CreateGridPanel("fill 182");
            titleRow.Dock = DockStyle.Fill;
            titleRow.BackColor = Color.Transparent;
            titleRow.Margin = Padding.Empty;
            titleRow.Padding = Padding.Empty;

            AntdUI.Label title = new AntdUI.Label();
            title.Dock = DockStyle.Fill;
            title.AutoEllipsis = true;
            title.Text = AiSettingsText.BuildProfileDisplayName(profile);
            title.Font = new Font("Microsoft YaHei UI", 10.5F, FontStyle.Bold);
            title.ForeColor = TextPrimaryColor;
            title.BackColor = Color.Transparent;
            title.TextAlign = ContentAlignment.MiddleLeft;
            title.Margin = new Padding(0, 3, 8, 0);

            AntdUI.FlowPanel tagRow = new AntdUI.FlowPanel();
            tagRow.Dock = DockStyle.Fill;
            tagRow.BackColor = Color.Transparent;
            tagRow.Align = AntdUI.TAlignFlow.LeftCenter;
            tagRow.Margin = Padding.Empty;
            tagRow.Padding = Padding.Empty;

            AntdControlFactory.AddGridControl(titleRow, title, 0);
            tagRow.Controls.Add(CreateTag(AiSettingsText.IsConfigured(profile) ? "正常" : "待补全", AiSettingsText.IsConfigured(profile) ? AntdUI.TTypeMini.Success : AntdUI.TTypeMini.Warn));
            tagRow.Controls.Add(CreateTag(AiSettingsText.FormatAccessModeLabel(profile == null ? null : profile.AccessMode), AntdUI.TTypeMini.Info));
            AntdControlFactory.AddGridControl(titleRow, tagRow, 1);

            AntdUI.Label endpoint = new AntdUI.Label();
            endpoint.Dock = DockStyle.Fill;
            endpoint.Text = AiSettingsText.NormalizeEndpoint(profile == null ? null : profile.Endpoint);
            endpoint.Font = new Font("Microsoft YaHei UI", 10F);
            endpoint.ForeColor = PrimaryColor;
            endpoint.BackColor = Color.Transparent;
            endpoint.TextAlign = ContentAlignment.MiddleLeft;
            endpoint.AutoEllipsis = true;

            AntdUI.Label meta = CreateSmallMutedLabel(AiSettingsText.BuildProfileMeta(profile));

            AntdControlFactory.AddGridControl(content, titleRow, 0);
            AntdControlFactory.AddGridControl(content, endpoint, 1);
            AntdControlFactory.AddGridControl(content, meta, 2);

            AntdUI.GridPanel actions = AntdControlFactory.CreateGridPanel("fill;fill;fill;fill;fill-fill 32 4 28 fill");
            actions.Dock = DockStyle.Fill;
            actions.BackColor = Color.Transparent;

            AntdUI.Button applyButton = CreateActionButton(selected ? "已选中" : "应用", "CheckOutlined", selected);
            applyButton.Click += delegate
            {
                if (applyRequested != null) applyRequested(index);
            };

            AntdUI.GridPanel iconRow = AntdControlFactory.CreateGridPanel("fill 28 6 28 fill");
            iconRow.Dock = DockStyle.Fill;
            iconRow.BackColor = Color.Transparent;
            iconRow.Margin = new Padding(8, 0, 0, 0);

            AntdUI.Button editButton = CreateIconButton("EditOutlined", AntdUI.TTypeMini.Primary);
            editButton.Click += delegate { if (editRequested != null) editRequested(index); };

            AntdUI.Button deleteButton = CreateIconButton("DeleteOutlined", AntdUI.TTypeMini.Error);
            deleteButton.Click += delegate { if (deleteRequested != null) deleteRequested(index); };

            AntdControlFactory.AddGridControl(iconRow, AntdControlFactory.CreateGridSpacer(), 0);
            AntdControlFactory.AddGridControl(iconRow, editButton, 1);
            AntdControlFactory.AddGridControl(iconRow, AntdControlFactory.CreateGridSpacer(), 2);
            AntdControlFactory.AddGridControl(iconRow, deleteButton, 3);
            AntdControlFactory.AddGridControl(iconRow, AntdControlFactory.CreateGridSpacer(), 4);

            AntdControlFactory.AddGridControl(actions, AntdControlFactory.CreateGridSpacer(), 0);
            AntdControlFactory.AddGridControl(actions, applyButton, 1);
            AntdControlFactory.AddGridControl(actions, AntdControlFactory.CreateGridSpacer(), 2);
            AntdControlFactory.AddGridControl(actions, iconRow, 3);
            AntdControlFactory.AddGridControl(actions, AntdControlFactory.CreateGridSpacer(), 4);

            AntdControlFactory.AddGridControl(layout, avatarCell, 0);
            AntdControlFactory.AddGridControl(layout, content, 1);
            AntdControlFactory.AddGridControl(layout, actions, 2);
            card.Controls.Add(layout);

            BindCardSelection(card, index, selectRequested);
            return card;
        }

        public static void ResizeCards(AntdUI.StackPanel listPanel)
        {
            if (listPanel == null) return;
            int width = ResolveCardWidth(listPanel);
            foreach (Control control in listPanel.Controls)
            {
                control.Width = width;
            }
        }

        private static AntdUI.Panel CreateCardSurface(AntdUI.StackPanel listPanel, bool selected)
        {
            AntdUI.Panel card = new AntdUI.Panel();
            card.Width = ResolveCardWidth(listPanel);
            card.Height = 102;
            card.Margin = new Padding(0, 0, 0, 10);
            card.Padding = new Padding(14, 10, 14, 10);
            card.Radius = 12;
            card.Back = selected ? Palette.AccentSoft : Palette.CardFill;
            card.BorderWidth = 0F;
            card.Shadow = 0;
            return card;
        }

        private static AntdUI.Button CreateIconButton(string iconSvg, AntdUI.TTypeMini type)
        {
            AntdUI.Button button = new AntdUI.Button();
            button.Dock = DockStyle.Fill;
            button.AutoSizeMode = AntdUI.TAutoSize.None;
            button.DisplayStyle = AntdUI.TButtonDisplayStyle.Image;
            button.IconSvg = iconSvg;
            button.Type = type;
            button.Ghost = true;
            button.Height = 30;
            button.Radius = 8;
            button.BorderWidth = 1F;
            button.WaveSize = 2;
            button.Margin = new Padding(0, 1, 0, 1);
            return button;
        }

        private static AntdUI.Button CreateActionButton(string text, string iconSvg, bool selected)
        {
            AntdUI.Button button = new AntdUI.Button();
            button.Dock = DockStyle.Fill;
            button.AutoSizeMode = AntdUI.TAutoSize.None;
            button.Text = text;
            button.IconSvg = iconSvg;
            button.Type = selected ? AntdUI.TTypeMini.Default : AntdUI.TTypeMini.Primary;
            button.Ghost = selected;
            button.Height = 32;
            button.Radius = 8;
            button.BorderWidth = 1F;
            button.WaveSize = 2;
            button.Margin = new Padding(8, 0, 0, 0);
            if (selected)
            {
                button.DefaultBorderColor = Palette.AccentSoftBorder;
                button.ForeColor = Palette.Accent;
                button.BackColor = Color.White;
            }
            return button;
        }

        private static AntdUI.Tag CreateTag(string text, AntdUI.TTypeMini type)
        {
            AntdUI.Tag tag = new AntdUI.Tag();
            tag.AutoSizeMode = AntdUI.TAutoSize.Auto;
            tag.Text = text;
            tag.Type = type;
            tag.BorderWidth = 0F;
            tag.Radius = 8;
            tag.Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Bold);
            tag.Margin = new Padding(0, 3, 6, 0);
            return tag;
        }

        private static AntdUI.Label CreateSmallMutedLabel(string text)
        {
            AntdUI.Label label = new AntdUI.Label();
            label.Dock = DockStyle.Fill;
            label.Text = text;
            label.TextAlign = ContentAlignment.MiddleLeft;
            label.Font = new Font("Segoe UI", 9F);
            label.ForeColor = Palette.TextMuted;
            label.BackColor = Color.Transparent;
            return label;
        }

        private static void BindCardSelection(Control control, int index, Action<int> selectRequested)
        {
            if (!(control is AntdUI.Button))
            {
                control.Cursor = Cursors.Hand;
                control.Click += delegate { if (selectRequested != null) selectRequested(index); };
            }

            foreach (Control child in control.Controls)
            {
                BindCardSelection(child, index, selectRequested);
            }
        }

        private static int ResolveCardWidth(AntdUI.StackPanel listPanel)
        {
            if (listPanel == null) return 640;
            int scrollBarOffset = listPanel.ScrollBar != null && listPanel.ScrollBar.ShowY ? listPanel.ScrollBar.SIZE : 0;
            return Math.Max(420, listPanel.ClientSize.Width - scrollBarOffset - 8);
        }

    }
}
