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
        private const string CustomAiPromptPresetKey = "__custom__";

        private const string CustomAiProviderPresetKey = "__custom__";

        private const string DefaultAiSystemPrompt = "你是 Windows 磁盘清理助手。请你只建议删除可再生成的缓存、临时文件、日志、崩溃转储、安装残留。不要建议删除系统目录、用户文档、应用程序主体或不确定的数据。输出严格 JSON，为那种[path1,path2]，这些表示可以删除的。";

        private static readonly AiPromptPreset[] AiPromptPresets =
        {
            new AiPromptPreset("standard", "标准清理", DefaultAiSystemPrompt),
            new AiPromptPreset("conservative", "保守清理", "你是谨慎的 Windows 磁盘清理助手。只选择明确可再生成、低风险且常见的缓存、临时文件、浏览器缓存、下载缓存和崩溃转储。任何不确定、用户生成、业务数据、源码、项目文件、应用主体和系统核心路径都不要建议删除。输出严格 JSON，为那种[path1,path2]，这些表示可以删除的。"),
            new AiPromptPreset("cache_aggressive", "激进缓存", "你是偏激进但仍安全的 Windows 缓存清理助手。优先建议大型可再生成缓存、构建缓存、包管理缓存、浏览器缓存、临时下载和安装残留。不要选择用户文档、媒体、源码、应用程序主体、数据库或系统核心文件。输出严格 JSON，为那种[path1,path2]，这些表示可以删除的。"),
            new AiPromptPreset("developer", "开发环境", "你是面向开发者电脑的 Windows 清理助手。优先识别可重建的 node_modules 缓存、NuGet 缓存、Gradle 缓存、Maven 缓存、pip 缓存、npm/yarn/pnpm 缓存、构建输出、测试临时文件和 IDE 缓存。不要删除源码、配置、数据库、密钥、用户文档或项目根目录。输出严格 JSON，为那种[path1,path2]，这些表示可以删除的。"),
            new AiPromptPreset("system_temp", "仅系统临时", "你是 Windows 系统临时文件清理助手。只建议删除 Windows Temp、用户 Temp、INetCache、SoftwareDistribution 下载缓存、崩溃转储和明确的临时文件。不要建议删除 Program Files、Windows 核心目录、用户文档、桌面、下载目录中的个人文件。输出严格 JSON，为那种[path1,path2]，这些表示可以删除的。"),
            new AiPromptPreset("logs_first", "日志优先", "你是 Windows 日志清理助手。优先选择大型日志、轮转日志、旧崩溃转储、诊断报告和应用运行临时日志。不要删除当前应用主体、配置、数据库、用户文档或无法判断用途的文件。输出严格 JSON，为那种[path1,path2]，这些表示可以删除的。"),
            new AiPromptPreset("installer_leftovers", "安装残留", "你是 Windows 安装残留清理助手。优先识别安装包缓存、安装临时目录、升级残留、解压残留和失败安装产生的临时文件。不要删除已安装程序主体、用户数据、许可证文件或系统核心组件。输出严格 JSON，为那种[path1,path2]，这些表示可以删除的。"),
            new AiPromptPreset("browser_cache", "浏览器缓存", "你是浏览器缓存清理助手。优先选择浏览器缓存、GPUCache、Code Cache、Service Worker Cache、崩溃报告和临时网络缓存。不要删除书签、历史数据库、扩展数据、密码、用户配置或下载的个人文件。输出严格 JSON，为那种[path1,path2]，这些表示可以删除的。"),
            new AiPromptPreset("media_safe", "媒体保护", "你是保护用户媒体资料的 Windows 清理助手。可以建议删除临时文件、缓存、日志和崩溃转储，但不要删除图片、视频、音频、文档、压缩包、设计素材、工程文件和下载目录中无法确定用途的文件。输出严格 JSON，为那种[path1,path2]，这些表示可以删除的。"),
            new AiPromptPreset("large_files_review", "大文件审查", "你是大文件审查助手。只从候选清单中挑选明显可再生成或无业务价值的大型缓存、临时文件、日志和残留文件；对下载、文档、桌面、项目目录、虚拟机镜像、数据库和媒体文件保持高风险并避免建议删除。输出严格 JSON，为那种[path1,path2]，这些表示可以删除的。"),
            new AiPromptPreset("recycle_bin_safe", "回收站友好", "你是回收站删除模式下的 Windows 清理助手。优先选择放入回收站后不影响系统运行的缓存、日志、临时文件和安装残留。不要依赖回收站作为安全理由去选择不确定或用户重要数据。输出严格 JSON，为那种[path1,path2]，这些表示可以删除的。"),
            new AiPromptPreset("enterprise_safe", "办公电脑", "你是办公电脑清理助手。只建议删除缓存、临时文件、日志、崩溃转储和安装残留。不要删除企业应用数据、邮件数据、同步盘、桌面、文档、下载、项目资料、数据库、证书、密钥和配置文件。输出严格 JSON，为那种[path1,path2]，这些表示可以删除的。")
        };

        private static readonly AiProviderPreset[] AiProviderPresets =
        {
            new AiProviderPreset("chatgpt", "ChatGPT / OpenAI", "https://api.openai.com", AiSettings.DefaultModel),
            new AiProviderPreset("deepseek", "DeepSeek", "https://api.deepseek.com", "deepseek-chat")
        };

        private sealed class AiPromptPreset
        {
            public AiPromptPreset(string key, string name, string prompt)
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

        private static string BuildDriveScopedPrompt(string prompt, string driveRoot)
        {
            string driveLabel = FormatDriveLabel(driveRoot);
            string normalizedRoot = NormalizeDriveRootText(driveRoot);
            return "当前重点分析 Windows " + driveLabel + "（" + normalizedRoot + "）下的候选路径。" + prompt;
        }

        private static string NormalizeDriveRootText(string driveRoot)
        {
            string root = TryGetDriveRoot(driveRoot);
            return string.IsNullOrWhiteSpace(root) ? "当前所选位置" : root;
        }

        private static string FormatDriveLabel(string driveRoot)
        {
            string root = TryGetDriveRoot(driveRoot);
            if (string.IsNullOrWhiteSpace(root) || root.Length < 2) return "当前磁盘";
            return char.ToUpperInvariant(root[0]) + "盘";
        }

        private sealed class AiProviderPreset
        {
            public AiProviderPreset(string key, string name, string endpoint, string model)
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
