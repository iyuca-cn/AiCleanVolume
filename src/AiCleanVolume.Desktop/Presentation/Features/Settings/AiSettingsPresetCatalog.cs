using System;
using System.Text.RegularExpressions;
using AiCleanVolume.Core.Domain.Settings;
using AiCleanVolume.Desktop.Presentation.Shared;

namespace AiCleanVolume.Desktop.Presentation.Features.Settings
{
    public static class AiSettingsPresetCatalog
    {
        public const string CustomPromptPresetKey = "__custom__";

        public const string CustomProviderPresetKey = "__custom__";

        public const string DefaultSystemPrompt = AiSettings.DefaultSystemPrompt;

        private static readonly AiPromptPresetOption[] PromptPresets =
        {
            new AiPromptPresetOption("standard", "标准清理", DefaultSystemPrompt),
            new AiPromptPresetOption("conservative", "保守清理", "你是谨慎的 Windows 磁盘清理审核助手。只选择候选清单中明确可再生成、低风险且常见的缓存、临时文件、浏览器缓存、下载缓存和崩溃转储。任何不确定、用户生成、业务数据、源码、项目文件、应用主体和系统核心路径都不要建议删除。只输出严格 JSON 字符串数组，数组元素必须完全等于候选 path，不要输出解释。"),
            new AiPromptPresetOption("cache_aggressive", "激进缓存", "你是偏激进但仍安全的 Windows 缓存清理审核助手。优先建议候选清单里的大型可再生成缓存、构建缓存、包管理缓存、浏览器缓存、临时下载和安装残留。不要选择用户文档、媒体、源码、应用程序主体、数据库或系统核心文件。只输出严格 JSON 字符串数组，数组元素必须完全等于候选 path，不要输出解释。"),
            new AiPromptPresetOption("developer", "开发环境", "你是面向开发者电脑的 Windows 清理审核助手。优先识别候选清单中可重建的 node_modules 缓存、NuGet 缓存、Gradle 缓存、Maven 缓存、pip 缓存、npm/yarn/pnpm 缓存、构建输出、测试临时文件和 IDE 缓存。不要删除源码、配置、数据库、密钥、用户文档或项目根目录。只输出严格 JSON 字符串数组，数组元素必须完全等于候选 path，不要输出解释。"),
            new AiPromptPresetOption("system_temp", "仅系统临时", "你是 Windows 系统临时文件清理审核助手。只建议候选清单中的 Windows Temp、用户 Temp、INetCache、SoftwareDistribution 下载缓存、崩溃转储和明确的临时文件。不要建议删除 Program Files、Windows 核心目录、用户文档、桌面、下载目录中的个人文件。只输出严格 JSON 字符串数组，数组元素必须完全等于候选 path，不要输出解释。"),
            new AiPromptPresetOption("logs_first", "日志优先", "你是 Windows 日志清理审核助手。优先选择候选清单中的大型日志、轮转日志、旧崩溃转储、诊断报告和应用运行临时日志。不要删除当前应用主体、配置、数据库、用户文档或无法判断用途的文件。只输出严格 JSON 字符串数组，数组元素必须完全等于候选 path，不要输出解释。"),
            new AiPromptPresetOption("installer_leftovers", "安装残留", "你是 Windows 安装残留清理审核助手。优先识别候选清单里的安装包缓存、安装临时目录、升级残留、解压残留和失败安装产生的临时文件。不要删除已安装程序主体、用户数据、许可证文件或系统核心组件。只输出严格 JSON 字符串数组，数组元素必须完全等于候选 path，不要输出解释。"),
            new AiPromptPresetOption("browser_cache", "浏览器缓存", "你是浏览器缓存清理审核助手。优先选择候选清单中的浏览器缓存、GPUCache、Code Cache、Service Worker Cache、崩溃报告和临时网络缓存。不要删除书签、历史数据库、扩展数据、密码、用户配置或下载的个人文件。只输出严格 JSON 字符串数组，数组元素必须完全等于候选 path，不要输出解释。"),
            new AiPromptPresetOption("media_safe", "媒体保护", "你是保护用户媒体资料的 Windows 清理审核助手。可以建议删除候选清单里的临时文件、缓存、日志和崩溃转储，但不要删除图片、视频、音频、文档、压缩包、设计素材、工程文件和下载目录中无法确定用途的文件。只输出严格 JSON 字符串数组，数组元素必须完全等于候选 path，不要输出解释。"),
            new AiPromptPresetOption("large_files_review", "大文件审查", "你是大文件清理审核助手。只从候选清单中挑选明显可再生成或无业务价值的大型缓存、临时文件、日志和残留文件；对下载、文档、桌面、项目目录、虚拟机镜像、数据库和媒体文件保持高风险并避免建议删除。只输出严格 JSON 字符串数组，数组元素必须完全等于候选 path，不要输出解释。"),
            new AiPromptPresetOption("recycle_bin_safe", "回收站友好", "你是回收站删除模式下的 Windows 清理审核助手。优先选择候选清单中放入回收站后不影响系统运行的缓存、日志、临时文件和安装残留。不要依赖回收站作为安全理由去选择不确定或用户重要数据。只输出严格 JSON 字符串数组，数组元素必须完全等于候选 path，不要输出解释。"),
            new AiPromptPresetOption("enterprise_safe", "办公电脑", "你是办公电脑清理审核助手。只建议删除候选清单中的缓存、临时文件、日志、崩溃转储和安装残留。不要删除企业应用数据、邮件数据、同步盘、桌面、文档、下载、项目资料、数据库、证书、密钥和配置文件。只输出严格 JSON 字符串数组，数组元素必须完全等于候选 path，不要输出解释。")
        };

        private static readonly AiProviderPresetOption[] ProviderPresets =
        {
            new AiProviderPresetOption("chatgpt", "ChatGPT / OpenAI", "https://api.openai.com", AiSettings.DefaultModel),
            new AiProviderPresetOption("deepseek", "DeepSeek", "https://api.deepseek.com", "deepseek-chat")
        };

        public static void PopulateAccessModes(AntdUI.Select select)
        {
            if (select == null) return;

            select.Items.Clear();
            select.Items.Add(new AntdUI.SelectItem("标准 API", AiSettings.StandardApiAccessMode));
            select.Items.Add(new AntdUI.SelectItem("2API", AiSettings.TwoApiAccessMode));
        }

        public static void PopulateProviderPresets(AntdUI.Select select)
        {
            if (select == null) return;

            select.Items.Clear();
            select.Items.Add(new AntdUI.SelectItem("自定义", CustomProviderPresetKey));
            for (int index = 0; index < ProviderPresets.Length; index++)
            {
                AiProviderPresetOption preset = ProviderPresets[index];
                select.Items.Add(new AntdUI.SelectItem(preset.Name, preset.Key));
            }
        }

        public static void PopulatePromptPresets(AntdUI.Select select)
        {
            if (select == null) return;

            select.Items.Clear();
            select.Items.Add(new AntdUI.SelectItem("自定义", CustomPromptPresetKey));
            for (int index = 0; index < PromptPresets.Length; index++)
            {
                AiPromptPresetOption preset = PromptPresets[index];
                select.Items.Add(new AntdUI.SelectItem(preset.Name, preset.Key));
            }
        }

        public static void SelectPromptPresetForPrompt(AntdUI.Select select, string prompt)
        {
            if (select == null) return;

            AiPromptPresetOption preset = FindPromptPresetByPrompt(prompt);
            select.SelectedValue = preset == null ? CustomPromptPresetKey : preset.Key;
        }

        public static AiProviderPresetOption FindProviderPreset(string endpoint, string model)
        {
            string normalizedEndpoint = NormalizeEndpoint(endpoint);
            string normalizedModel = AiSettingsText.NormalizeValue(model);
            if (string.IsNullOrWhiteSpace(normalizedEndpoint) || string.IsNullOrWhiteSpace(normalizedModel)) return null;

            for (int index = 0; index < ProviderPresets.Length; index++)
            {
                AiProviderPresetOption preset = ProviderPresets[index];
                if (string.Equals(NormalizeEndpoint(preset.Endpoint), normalizedEndpoint, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(AiSettingsText.NormalizeValue(preset.Model), normalizedModel, StringComparison.OrdinalIgnoreCase))
                {
                    return preset;
                }
            }

            return null;
        }

        public static AiProviderPresetOption FindProviderPresetByKey(string key)
        {
            if (string.IsNullOrWhiteSpace(key)) return null;
            for (int index = 0; index < ProviderPresets.Length; index++)
            {
                if (string.Equals(ProviderPresets[index].Key, key, StringComparison.OrdinalIgnoreCase)) return ProviderPresets[index];
            }

            return null;
        }

        public static AiPromptPresetOption FindPromptPreset(string key)
        {
            if (string.IsNullOrWhiteSpace(key)) return null;
            for (int index = 0; index < PromptPresets.Length; index++)
            {
                if (string.Equals(PromptPresets[index].Key, key, StringComparison.OrdinalIgnoreCase)) return PromptPresets[index];
            }

            return null;
        }

        public static AiPromptPresetOption FindPromptPresetByPrompt(string prompt)
        {
            string normalizedPrompt = NormalizePromptForComparison(prompt);
            if (string.IsNullOrWhiteSpace(normalizedPrompt)) return null;

            for (int index = 0; index < PromptPresets.Length; index++)
            {
                if (string.Equals(NormalizePromptForComparison(PromptPresets[index].Prompt), normalizedPrompt, StringComparison.Ordinal)) return PromptPresets[index];
            }

            return null;
        }

        private static string BuildDriveScopedPrompt(string prompt, string driveRoot)
        {
            string driveLabel = DrivePathText.FormatDriveLabel(driveRoot);
            string normalizedRoot = DrivePathText.NormalizeDriveRootText(driveRoot);
            return "当前重点分析 Windows " + driveLabel + "（" + normalizedRoot + "）下的候选路径。" + prompt;
        }

        private static string NormalizePromptForComparison(string prompt)
        {
            string normalized = (prompt ?? string.Empty).Replace("\r\n", "\n").Replace("\r", "\n").Trim();
            normalized = Regex.Replace(normalized, "[A-Za-z]\\s*盘", "{driveLabel}");
            normalized = Regex.Replace(normalized, "[A-Za-z]:\\\\", "{driveRoot}");
            normalized = normalized.Replace("当前重点分析 Windows {driveLabel}（{driveRoot}）下的候选路径。", string.Empty);
            normalized = Regex.Replace(normalized, "Windows\\s*\\{driveLabel\\}\\s*清理助手", "Windows 磁盘清理助手");
            return normalized;
        }

        private static string NormalizeEndpoint(string endpoint)
        {
            return AiSettingsText.NormalizeEndpoint(endpoint);
        }

        public sealed class AiPromptPresetOption
        {
            public AiPromptPresetOption(string key, string name, string prompt)
            {
                Key = key;
                Name = name;
                Prompt = prompt;
            }

            public string Key { get; private set; }
            public string Name { get; private set; }
            public string Prompt { get; private set; }

            public string BuildPrompt(string driveRoot)
            {
                return BuildDriveScopedPrompt(Prompt, driveRoot);
            }
        }

        public sealed class AiProviderPresetOption
        {
            public AiProviderPresetOption(string key, string name, string endpoint, string model)
            {
                Key = key;
                Name = name;
                Endpoint = endpoint;
                Model = model;
            }

            public string Key { get; private set; }
            public string Name { get; private set; }
            public string Endpoint { get; private set; }
            public string Model { get; private set; }
        }
    }
}
