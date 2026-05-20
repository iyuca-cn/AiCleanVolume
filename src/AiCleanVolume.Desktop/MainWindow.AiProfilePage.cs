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
        private Control CreateAiProfileCreatePage()
        {
            AntdUI.Panel panel = CreateFlatPanel();
            panel.Dock = DockStyle.Fill;
            panel.BackColor = PageBackground;
            panel.Padding = new Padding(20);

            AntdUI.GridPanel pageLayout = CreateGridPanel("fill;56:fill");
            pageLayout.Dock = DockStyle.Fill;
            pageLayout.BackColor = Color.Transparent;

            AntdUI.StackPanel scrollHost = CreateVerticalScrollPanel();
            scrollHost.Dock = DockStyle.Fill;
            scrollHost.AutoScroll = true;
            scrollHost.BackColor = PageBackground;
            scrollHost.Padding = new Padding(0, 0, 4, 12);

            AntdUI.GridPanel content = CreateGridPanel("88:fill;196:fill;196:fill;390:fill");
            content.Dock = DockStyle.Top;
            content.BackColor = PageBackground;
            content.Height = 870;
            content.Width = Math.Max(720, scrollHost.ClientSize.Width - 8);
            scrollHost.Resize += delegate { content.Width = Math.Max(720, scrollHost.ClientSize.Width - 8); };

            AntdUI.Panel header = CreateAiProfileCreateHeader();
            Control basicSection = CreateAiProfileBasicSection();
            Control endpointSection = CreateAiProfileEndpointSection();
            Control promptSection = CreateAiProfilePromptSection();
            header.Margin = new Padding(0, 0, 0, 12);
            basicSection.Margin = new Padding(0, 0, 0, 12);
            endpointSection.Margin = new Padding(0, 0, 0, 12);
            promptSection.Margin = new Padding(0);

            AddGridControl(content, header, 0);
            AddGridControl(content, basicSection, 1);
            AddGridControl(content, endpointSection, 2);
            AddGridControl(content, promptSection, 3);
            scrollHost.Controls.Add(content);

            AntdUI.Panel footer = CreateAiProfileCreateFooter();
            AddGridControl(pageLayout, scrollHost, 0);
            AddGridControl(pageLayout, footer, 1);
            panel.Controls.Add(pageLayout);
            return panel;
        }

        private AntdUI.Panel CreateAiProfileCreateHeader()
        {
            AntdUI.Panel header = CreateFlatPanel();
            header.Dock = DockStyle.Fill;
            header.BackColor = PageBackground;

            AntdUI.GridPanel layout = CreateGridPanel("48 fill");
            layout.Dock = DockStyle.Fill;
            layout.BackColor = Color.Transparent;

            backAiProfilePageButton = new AntdUI.Button();
            backAiProfilePageButton.Dock = DockStyle.Fill;
            backAiProfilePageButton.AutoSizeMode = AntdUI.TAutoSize.None;
            backAiProfilePageButton.DisplayStyle = AntdUI.TButtonDisplayStyle.Image;
            backAiProfilePageButton.Shape = AntdUI.TShape.Circle;
            backAiProfilePageButton.IconSvg = "ArrowLeftOutlined";
            backAiProfilePageButton.Type = AntdUI.TTypeMini.Default;
            backAiProfilePageButton.Ghost = true;
            backAiProfilePageButton.BorderWidth = 1F;
            backAiProfilePageButton.DefaultBorderColor = BorderLightColor;
            backAiProfilePageButton.Margin = new Padding(0, 0, 10, 0);
            backAiProfilePageButton.WaveSize = 2;
            backAiProfilePageButton.Click += delegate { CancelAiProfileCreatePage(); };

            AntdUI.Label title = CreateSectionTitle("新增 AI 配置");
            title.Dock = DockStyle.Fill;
            AntdUI.Label desc = CreateSectionDescription("填写接入参数并保存为配置卡片。");
            desc.Dock = DockStyle.Fill;

            AntdUI.GridPanel textLayout = CreateGridPanel("36:fill;30:fill");
            textLayout.Dock = DockStyle.Fill;
            AddGridControl(textLayout, title, 0);
            AddGridControl(textLayout, desc, 1);

            AddGridControl(layout, backAiProfilePageButton, 0);
            AddGridControl(layout, textLayout, 1);
            header.Controls.Add(layout);
            return header;
        }

        private Control CreateAiProfileBasicSection()
        {
            AntdUI.Panel body;
            AntdUI.Panel section = CreateSettingsGroupPanel("基础信息", "命名这套配置，并选择访问方式。", out body);

            AntdUI.GridPanel form = CreateTwoColumnProfileForm(2);
            aiProfileNameInput = CreateInput("例如：开发环境");
            aiProfileAccessModeSelect = CreateSettingsSelect();
            PopulateAiAccessModes(aiProfileAccessModeSelect);
            aiProfileMaxSuggestionsInput = CreateInput("30");

            aiProfileAccessModeSelect.SelectedValueChanged += AiProfileAccessModeSelect_SelectedValueChanged;

            AddProfileField(form, "配置名称", aiProfileNameInput, 0, 0);
            AddProfileField(form, "接入类型", aiProfileAccessModeSelect, 2, 0);
            AddProfileField(form, "建议条数", aiProfileMaxSuggestionsInput, 0, 1);

            body.Controls.Add(form);
            return section;
        }

        private Control CreateAiProfileEndpointSection()
        {
            AntdUI.Panel body;
            AntdUI.Panel section = CreateSettingsGroupPanel("接口参数", "配置 OpenAI 兼容接口地址、密钥和模型。", out body);

            AntdUI.GridPanel form = CreateTwoColumnProfileForm(2);
            aiProfileProviderPresetSelect = CreateSettingsSelect();
            PopulateAiProviderPresets(aiProfileProviderPresetSelect);
            aiProfileEndpointInput = CreateInput("https://api.openai.com");
            aiProfileApiKeyInput = CreateInput("sk-...");
            aiProfileModelInput = CreateInput(AiSettings.DefaultModel);

            aiProfileProviderPresetSelect.SelectedValueChanged += AiProfileProviderPresetSelect_SelectedValueChanged;
            aiProfileEndpointInput.TextChanged += AiProfileEndpointOrModelInput_TextChanged;
            aiProfileModelInput.TextChanged += AiProfileEndpointOrModelInput_TextChanged;

            AddProfileField(form, "接口预设", aiProfileProviderPresetSelect, 0, 0);
            AddProfileField(form, "接口地址", aiProfileEndpointInput, 2, 0);
            AddProfileField(form, "SK / API Key", aiProfileApiKeyInput, 0, 1);
            AddProfileField(form, "模型", aiProfileModelInput, 2, 1);

            body.Controls.Add(form);
            return section;
        }

        private Control CreateAiProfilePromptSection()
        {
            AntdUI.Panel body;
            AntdUI.Panel section = CreateSettingsGroupPanel("提示词与 Cookie", "多行内容使用完整宽度，避免长文本挤压。", out body);

            AntdUI.GridPanel form = CreateGridPanel("44:92 fill;104:92 fill;148:92 fill");
            form.Dock = DockStyle.Fill;
            form.BackColor = Color.Transparent;

            aiProfilePromptPresetSelect = CreateSettingsSelect();
            PopulateAiPromptPresets(aiProfilePromptPresetSelect);
            aiProfileCookieMappingsInput = CreateInput("直接粘贴当前模型的一整行 Cookie；也兼容 model=Cookie");
            aiProfileCookieMappingsInput.Multiline = true;
            aiProfileCookieMappingsInput.AutoScroll = true;
            aiProfileSystemPromptInput = CreateInput("系统提示词");
            aiProfileSystemPromptInput.Multiline = true;
            aiProfileSystemPromptInput.AutoScroll = true;

            aiProfilePromptPresetSelect.SelectedValueChanged += AiProfilePromptPresetSelect_SelectedValueChanged;
            aiProfileSystemPromptInput.TextChanged += AiProfileSystemPromptInput_TextChanged;

            AddWideProfileField(form, "AI 预设", aiProfilePromptPresetSelect, 0);
            AddWideProfileField(form, "模型 Cookie", aiProfileCookieMappingsInput, 1);
            AddWideProfileField(form, "系统提示词", aiProfileSystemPromptInput, 2);

            body.Controls.Add(form);
            return section;
        }

        private AntdUI.Panel CreateAiProfileCreateFooter()
        {
            AntdUI.Panel footer = CreateFlatPanel();
            footer.Dock = DockStyle.Fill;
            footer.BackColor = PageBackground;
            footer.Padding = new Padding(0, 10, 0, 0);

            AntdUI.GridPanel layout = CreateGridPanel("108 116");
            layout.Dock = DockStyle.Right;
            layout.BackColor = Color.Transparent;
            layout.Width = 236;

            cancelAiProfilePageButton = CreateSettingsActionButton("取消", AntdUI.TTypeMini.Default);
            cancelAiProfilePageButton.Dock = DockStyle.Fill;
            cancelAiProfilePageButton.Margin = new Padding(0, 4, 8, 4);
            cancelAiProfilePageButton.Click += delegate { CancelAiProfileCreatePage(); };

            saveAiProfilePageButton = CreateSettingsActionButton("保存", AntdUI.TTypeMini.Primary);
            saveAiProfilePageButton.Dock = DockStyle.Fill;
            saveAiProfilePageButton.Margin = new Padding(0, 4, 0, 4);
            saveAiProfilePageButton.Click += delegate { SaveAiProfileFromPage(); };

            AddGridControl(layout, cancelAiProfilePageButton, 0);
            AddGridControl(layout, saveAiProfilePageButton, 1);
            footer.Controls.Add(layout);
            return footer;
        }

        private static AntdUI.GridPanel CreateTwoColumnProfileForm(int rows)
        {
            string[] rowDefinitions = new string[rows];
            for (int row = 0; row < rows; row++) rowDefinitions[row] = "44:92 fill 92 fill";
            AntdUI.GridPanel form = CreateGridPanel(string.Join(";", rowDefinitions));
            form.Dock = DockStyle.Fill;
            form.BackColor = Color.Transparent;
            return form;
        }

        private static void AddProfileField(AntdUI.GridPanel form, string caption, Control control, int column, int row)
        {
            AntdUI.Label label = CreateCaption(caption);
            label.Margin = new Padding(0, 0, 8, 8);
            control.Margin = new Padding(0, 0, 0, 8);
            int index = row * 4 + column;
            AddGridControl(form, label, index);
            AddGridControl(form, control, index + 1);
        }

        private static void AddWideProfileField(AntdUI.GridPanel form, string caption, Control control, int row)
        {
            AntdUI.Label label = CreateCaption(caption);
            label.Margin = new Padding(0, 0, 8, 8);
            control.Margin = new Padding(0, 0, 0, 8);
            int index = row * 2;
            AddGridControl(form, label, index);
            AddGridControl(form, control, index + 1);
        }
    }
}
