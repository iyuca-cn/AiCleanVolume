using System;
using System.Drawing;
using System.Windows.Forms;
using AiCleanVolume.Core.Domain.Settings;
using AiCleanVolume.Core.Domain.Storage;
using AiCleanVolume.Desktop.Presentation.Features.Settings;
using AiCleanVolume.Desktop.Presentation.Shared;
using AiCleanVolume.Desktop.Presentation.Shared.Antd;

namespace AiCleanVolume.Desktop
{
    public sealed partial class MainWindow : AntdUI.Window
    {
        // 删除方式 / 排序依据两个分段控件仅存在于设置弹窗，随弹窗构建、关闭时置空。
        private AntdUI.Segmented recycleSegmented;

        private AntdUI.Segmented sortSegmented;

        // 弹窗内清理风格提示词编辑控件，随弹窗生命周期存在。
        private AntdUI.Input promptStyleInput;

        // 设置以 Modal 承载：每次打开重建控件并灌入当前配置，OnOk 保存后置空引用。
        private void ShowSettingsModal()
        {
            if (settings == null) return;

            AntdUI.Panel content = CreateFlatPanel();
            content.Width = 640;
            content.Height = 604;
            content.BackColor = Color.Transparent;

            AntdUI.StackPanel scrollHost = CreateVerticalScrollPanel();
            scrollHost.Dock = DockStyle.Fill;
            scrollHost.AutoScroll = true;
            scrollHost.BackColor = Color.Transparent;
            scrollHost.Padding = new Padding(0, 0, 6, 4);

            AntdUI.GridPanel layout = CreateGridPanel("700:fill;250:fill;190:fill");
            layout.Dock = DockStyle.Top;
            layout.Height = 1164;
            layout.BackColor = Color.Transparent;
            layout.Width = Math.Max(560, scrollHost.ClientSize.Width - 8);
            scrollHost.Resize += delegate { layout.Width = Math.Max(560, scrollHost.ClientSize.Width - 8); };

            Control aiSection = BuildSettingsAiSection();
            Control cleanupSection = BuildSettingsCleanupSection();
            Control scanSection = BuildSettingsScanSection();
            aiSection.Margin = new Padding(0, 0, 0, 12);
            cleanupSection.Margin = new Padding(0, 0, 0, 12);
            scanSection.Margin = new Padding(0);

            AddGridControl(layout, aiSection, 0);
            AddGridControl(layout, cleanupSection, 1);
            AddGridControl(layout, scanSection, 2);
            scrollHost.Controls.Add(layout);
            content.Controls.Add(scrollHost);

            PopulateAiAccessModes();
            PopulateAiProviderPresets();
            LoadSettingsToUi();

            AntdUI.Modal.Config config = AntdUI.Modal.config(this, "设置", content, AntdUI.TType.None);
            config.OkText = "保存";
            config.CancelText = "关闭";
            config.OkType = AntdUI.TTypeMini.Primary;
            config.Width = 700;
            config.MaskClosable = false;
            config.Resizable = true;
            config.MinimumSize = new Size(640, 520);
            config.OnOk = delegate
            {
                try
                {
                    SaveSettingsFromUi();
                    SaveCurrentAiProfileAutomatic();
                    settingsStore.Save(settings);
                    UpdateAiStatusChip();
                    Log("配置已保存。");
                    return true;
                }
                catch (Exception ex)
                {
                    Log("保存配置失败：" + ex.Message);
                    ShowError("保存失败", ex.Message);
                    return false;
                }
            };

            try
            {
                AntdUI.Modal.open(config);
            }
            finally
            {
                ReleaseSettingsModalControls();
            }
        }

        // 弹窗内的控件在 Modal 关闭时被销毁，这里断开引用，非弹窗路径读写时以 null 跳过。
        private void ReleaseSettingsModalControls()
        {
            aiEnabledSwitch = null;
            aiAccessModeSelect = null;
            aiProviderPresetSelect = null;
            endpointInput = null;
            apiKeyInput = null;
            modelInput = null;
            maxSuggestionsInput = null;
            modelCookieMappingsInput = null;
            allowRootsInput = null;
            privilegedCheckbox = null;
            aiProfileListPanel = null;
            recycleSegmented = null;
            sortSegmented = null;
            promptStyleInput = null;
        }

        private Control BuildSettingsAiSection()
        {
            aiEnabledSwitch = CreateSettingsSwitch();
            aiAccessModeSelect = CreateSettingsSelect();
            aiProviderPresetSelect = CreateSettingsSelect();
            endpointInput = CreateInput("https://api.openai.com");
            apiKeyInput = CreateInput("sk-...");
            apiKeyInput.UseSystemPasswordChar = true;
            modelInput = CreateInput(AiSettings.DefaultModel);
            maxSuggestionsInput = CreateInput("30");
            modelCookieMappingsInput = CreateInput("2API 模式：粘贴当前模型整行 Cookie；也兼容 model=Cookie");
            AntdUI.Select promptStyleSelect = CreateSettingsSelect();
            promptStyleInput = CreateInput("系统提示词");
            promptStyleInput.Multiline = true;
            promptStyleInput.AutoScroll = true;
            promptStyleInput.MaxLength = int.MaxValue;

            aiAccessModeSelect.SelectedValueChanged += AiAccessModeSelect_SelectedValueChanged;
            aiProviderPresetSelect.SelectedValueChanged += AiProviderPresetSelect_SelectedValueChanged;
            endpointInput.TextChanged += AiEndpointOrModelInput_TextChanged;
            modelInput.TextChanged += AiEndpointOrModelInput_TextChanged;

            AntdUI.Panel body;
            AntdUI.Panel section = CreateSettingsGroupPanel("AI 智能建议", "接入 OpenAI 兼容接口，为清理项生成建议与说明。", out body);

            aiProfileListPanel = CreateVerticalScrollPanel();
            aiProfileListPanel.BackColor = Palette.Surface;
            aiProfileListPanel.AutoScroll = true;
            aiProfileListPanel.Resize += delegate { ResizeAiProfileCards(); };

            AntdUI.Button saveProfileButton = CreateSettingsActionButton("+ 另存当前为档案", AntdUI.TTypeMini.Default);
            saveProfileButton.Click += delegate { SaveCurrentAiProfileWithPrompt(); };
            AntdUI.Button testButton = CreateSettingsActionButton("测试连接", AntdUI.TTypeMini.Default);
            testButton.IconSvg = "SearchOutlined";
            testButton.Click += delegate { TestAiSettings(); };

            PopulateAiPromptPresets(promptStyleSelect);
            bool syncingPreset = true;
            promptStyleInput.Text = GetCurrentSystemPromptText();
            pendingSystemPrompt = promptStyleInput.Text;
            SelectAiPromptPresetForPrompt(promptStyleSelect, promptStyleInput.Text);
            syncingPreset = false;

            promptStyleSelect.SelectedValueChanged += delegate(object sender, AntdUI.ObjectNEventArgs e)
            {
                if (syncingPreset || e.Value == null) return;
                string key = e.Value.ToString();
                if (string.Equals(key, AiSettingsPresetCatalog.CustomPromptPresetKey, StringComparison.OrdinalIgnoreCase)) return;
                AiSettingsPresetCatalog.AiPromptPresetOption preset = AiSettingsPresetCatalog.FindPromptPreset(key);
                if (preset == null) return;
                syncingPreset = true;
                try { promptStyleInput.Text = preset.BuildPrompt(GetPromptDriveRoot()); }
                finally { syncingPreset = false; }
            };
            promptStyleInput.TextChanged += delegate
            {
                pendingSystemPrompt = promptStyleInput.Text;
                if (syncingPreset) return;
                syncingPreset = true;
                try { SelectAiPromptPresetForPrompt(promptStyleSelect, promptStyleInput.Text); }
                finally { syncingPreset = false; }
            };

            AntdUI.GridPanel form = CreateGridPanel(
                "40:fill 60;" +
                "150:fill;" +
                "40:96 fill;" +
                "40:72 fill 72 fill;" +
                "40:72 fill;" +
                "40:72 fill;" +
                "40:72 fill 72 fill;" +
                "40:72 fill;" +
                "40:72 fill;" +
                "96:fill");
            form.Dock = DockStyle.Top;
            form.Height = 570;
            form.BackColor = Color.Transparent;

            AntdUI.GridPanel buttonRow = CreateGridPanel("fill 196 120");
            buttonRow.Dock = DockStyle.Fill;
            buttonRow.BackColor = Color.Transparent;
            saveProfileButton.Margin = new Padding(0, 4, 8, 4);
            testButton.Margin = new Padding(0, 4, 0, 4);
            AddGridControl(buttonRow, CreateGridSpacer(), 0);
            AddGridControl(buttonRow, saveProfileButton, 1);
            AddGridControl(buttonRow, testButton, 2);

            AddGridControl(form, CreateCaption("启用 AI 智能建议"), 0);
            AddGridControl(form, aiEnabledSwitch, 1);
            AddGridControl(form, aiProfileListPanel, 2);
            AddGridControl(form, CreateCaption("配置档案"), 3);
            AddGridControl(form, buttonRow, 4);
            AddGridControl(form, CreateCaption("访问模式"), 5);
            AddGridControl(form, aiAccessModeSelect, 6);
            AddGridControl(form, CreateCaption("服务商预设"), 7);
            AddGridControl(form, aiProviderPresetSelect, 8);
            AddGridControl(form, CreateCaption("接口地址"), 9);
            AddGridControl(form, endpointInput, 10);
            AddGridControl(form, CreateCaption("API Key"), 11);
            AddGridControl(form, apiKeyInput, 12);
            AddGridControl(form, CreateCaption("模型名称"), 13);
            AddGridControl(form, modelInput, 14);
            AddGridControl(form, CreateCaption("建议条数上限"), 15);
            AddGridControl(form, maxSuggestionsInput, 16);
            AddGridControl(form, CreateCaption("Cookie 映射"), 17);
            AddGridControl(form, modelCookieMappingsInput, 18);
            AddGridControl(form, CreateCaption("清理风格预设"), 19);
            AddGridControl(form, promptStyleSelect, 20);
            AddGridControl(form, promptStyleInput, 21);

            body.Controls.Add(form);
            return section;
        }

        private Control BuildSettingsCleanupSection()
        {
            privilegedCheckbox = CreateCheckbox("启用完全权限（管理员，可处理受保护的系统文件）");
            privilegedCheckbox.CheckedChanged += PrivilegedCheckbox_CheckedChanged;
            allowRootsInput = CreateInput("每行一个允许位置，命中后可直接删除，其他位置继续二次确认");
            allowRootsInput.Multiline = true;
            allowRootsInput.AutoScroll = true;

            recycleSegmented = CreateSettingsSegmented("移到回收站", "永久删除");

            AntdUI.Panel body;
            AntdUI.Panel section = CreateSettingsGroupPanel("清理行为", "删除方式、权限模式与沙盒白名单。", out body);

            AntdUI.GridPanel form = CreateGridPanel(
                "40:96 fill;" +
                "40:fill;" +
                "80:fill");
            form.Dock = DockStyle.Top;
            form.Height = 160;
            form.BackColor = Color.Transparent;

            AddGridControl(form, CreateCaption("默认删除方式"), 0);
            AddGridControl(form, recycleSegmented, 1);
            AddGridControl(form, privilegedCheckbox, 2);
            AddGridControl(form, allowRootsInput, 3);

            body.Controls.Add(form);
            return section;
        }

        private Control BuildSettingsScanSection()
        {
            sortSegmented = CreateSettingsSegmented("占用大小", "实际大小");

            // 最小体积 / 每层条目实际由隐藏的 minSizeInput / limitInput 承载（扫描请求直接读取它们），
            // 弹窗用独立输入框展示并回写这两个隐藏控件，避免把常驻控件挂进弹窗随其一起销毁。
            AntdUI.Input minInput = CreateInput("-1 表示不限制");
            AntdUI.Input limitCountInput = CreateInput("-1 表示不限制");
            minInput.Text = minSizeInput == null ? settings.Scan.MinSizeMb.ToString() : minSizeInput.Text;
            limitCountInput.Text = limitInput == null ? settings.Scan.PerLevelLimit.ToString() : limitInput.Text;
            minInput.TextChanged += delegate { if (minSizeInput != null) minSizeInput.Text = minInput.Text; };
            limitCountInput.TextChanged += delegate { if (limitInput != null) limitInput.Text = limitCountInput.Text; };

            AntdUI.Panel body;
            AntdUI.Panel section = CreateSettingsGroupPanel("扫描选项", "阈值、每层条目上限与占用大小排序依据。", out body);

            AntdUI.GridPanel form = CreateGridPanel(
                "40:96 fill 108 fill;" +
                "40:96 fill");
            form.Dock = DockStyle.Top;
            form.Height = 88;
            form.BackColor = Color.Transparent;

            AddGridControl(form, CreateCaption("最小体积(MB)"), 0);
            AddGridControl(form, minInput, 1);
            AddGridControl(form, CreateCaption("每层最多条目"), 2);
            AddGridControl(form, limitCountInput, 3);
            AddGridControl(form, CreateCaption("排序依据"), 4);
            AddGridControl(form, sortSegmented, 5);

            body.Controls.Add(form);
            return section;
        }

        private static AntdUI.Segmented CreateSettingsSegmented(params string[] options)
        {
            AntdUI.Segmented segmented = new AntdUI.Segmented();
            segmented.Dock = DockStyle.Fill;
            segmented.Margin = new Padding(0, 6, 0, 6);
            segmented.Radius = 8;
            segmented.BarRadius = 6;
            for (int i = 0; i < options.Length; i++)
            {
                segmented.Items.Add(new AntdUI.SegmentedItem { Text = options[i] });
            }
            segmented.SelectIndex = 0;
            return segmented;
        }
    }
}
