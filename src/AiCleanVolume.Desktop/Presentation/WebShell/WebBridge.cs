using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using AiCleanVolume.Core.Application.Scanning;
using AiCleanVolume.Core.Domain.Ai;
using AiCleanVolume.Core.Domain.Cleanup;
using AiCleanVolume.Core.Domain.Sandbox;
using AiCleanVolume.Core.Domain.Settings;
using AiCleanVolume.Core.Domain.Storage;
using AiCleanVolume.Desktop.Composition;
using AiCleanVolume.Desktop.Infrastructure.Ai;
using Microsoft.Win32;
using Newtonsoft.Json.Linq;

namespace AiCleanVolume.Desktop.Presentation.WebShell
{
    // JS 桥的方法分发。WebShellWindow 解析信封后调用 Invoke，返回值序列化进 result；
    // 抛出的异常 message 进 error。后端服务全部来自注入的 MainWindowDependencies。
    internal sealed class WebBridge
    {
        private readonly MainWindowDependencies dependencies;
        private readonly WebShellWindow host;

        // 扫描会话状态：ai.report / suggest 复用根路径与会话标识做懒加载与目录摘要。
        private OpenAiCompatibleAdvisor cachedAdvisor;
        private StorageItem lastRoot;
        private ScanRequest sessionTemplate;

        // 进程内唯一的设置实例：启动时读一次，业务方法一律用它，避免每请求读文件放大并发冲突。
        private ApplicationSettings settings;

        public WebBridge(MainWindowDependencies dependencies, WebShellWindow host)
        {
            this.dependencies = dependencies;
            this.host = host;
            settings = dependencies.SettingsStore.Load();
        }

        private OpenAiCompatibleAdvisor Advisor
        {
            get { return cachedAdvisor ?? (cachedAdvisor = dependencies.CreateAiAdvisor(null)); }
        }

        public object Invoke(string method, JObject parameters)
        {
            switch (method)
            {
                case "window.minimize": host.MinimizeWindow(); return null;
                case "window.maximize": host.ToggleMaximize(); return null;
                case "window.close": host.CloseWindow(); return null;
                case "window.dragMove": host.DragMove(); return null;

                case "env.info": return EnvInfo();
                case "env.precheck": return EnvPrecheck();
                case "env.openPath": dependencies.ExplorerService.OpenPath(Str(parameters, "path"), false); return null;
                case "env.restartElevated": host.RestartElevated(); return null;

                case "settings.get": return settings;
                case "settings.save": return SaveSettings(parameters);
                case "settings.testAi": return TestAi(parameters);

                case "scan.start": return ScanStart(parameters);
                case "scan.children": return ScanChildren(parameters);

                case "ai.chat": return AiChat(parameters);
                case "ai.report": return AiReport(parameters);

                case "suggest.analyze": return SuggestAnalyze(parameters);

                case "del.evaluate": return DelEvaluate(parameters);
                case "del.run": return DelRun(parameters);

                default:
                    throw new NotSupportedException("not_implemented");
            }
        }

        private object EnvInfo()
        {
            var drives = DriveInfo.GetDrives()
                .Where(d => d.DriveType == DriveType.Fixed && d.IsReady)
                .Select(d => new
                {
                    name = d.Name.TrimEnd('\\'),
                    fs = d.DriveFormat,
                    used = d.TotalSize - d.TotalFreeSpace,
                    total = d.TotalSize
                })
                .ToArray();

            return new
            {
                elevated = host.IsElevated,
                version = typeof(WebBridge).Assembly.GetName().Version.ToString(3),
                drives
            };
        }

        // 扫描前的方向预判：只做路径存在性与第一层浅枚举，跳过无权限项，整体控制在数秒内。
        // title/chip/desc 在此组装成设计稿卡片文案，前端按 key 分配配色。
        private object EnvPrecheck()
        {
            List<object> items = new List<object>();

            string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            string documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            string localApp = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string programData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
            string windows = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
            string systemDrive = Path.GetPathRoot(windows) ?? "C:\\";

            // ---- 日常社交 ----
            string wechatDir = FirstExisting(
                Path.Combine(documents, "WeChat Files"),
                Path.Combine(documents, "xwechat_files"),
                Path.Combine(userProfile, "Documents", "WeChat Files"),
                Path.Combine(userProfile, "Documents", "xwechat_files"));
            if (wechatDir != null) AddItem(items, "wechat", "social", "微信 PC 版", "已检测到安装", "缓存目录通常占用 5–20 GB，扫描后给出准确大小与可清理明细。", wechatDir);

            string qqDir = FirstExisting(
                Path.Combine(documents, "Tencent Files"),
                Path.Combine(userProfile, "Documents", "Tencent Files"),
                Path.Combine(documents, "QQ"),
                Path.Combine(userProfile, "Documents", "QQ"),
                Path.Combine(appData, "Tencent", "QQ"),
                Path.Combine(localApp, "QQ"));
            if (qqDir != null) AddItem(items, "qq", "social", "QQ / QQNT", "已检测到安装", "聊天图片、文件与语音缓存，可安全清理且不影响消息记录。", qqDir);

            string dingtalkDir = FirstExisting(Path.Combine(appData, "DingTalk"), Path.Combine(localApp, "DingTalk"));
            if (dingtalkDir != null) AddItem(items, "dingtalk", "social", "钉钉", "已检测到安装", "钉钉缓存与临时文件，清理后不影响登录与消息。", dingtalkDir);

            string wxworkDir = FirstExisting(Path.Combine(appData, "Tencent", "WXWork"), Path.Combine(documents, "WXWork"));
            if (wxworkDir != null) AddItem(items, "wxwork", "social", "企业微信", "已检测到安装", "企业微信文件与图片缓存，可安全清理。", wxworkDir);

            // ---- 游戏 ----
            string steamRoot = TrySteamRoot();
            string steamApps = steamRoot != null ? Path.Combine(steamRoot, "steamapps") : null;
            if (steamApps != null && DirExists(steamApps))
            {
                string steamCommon = Path.Combine(steamApps, "common");
                AddItem(items, "steam", "game", "Steam 游戏库", "已检测到安装", "将分析各游戏最后启动时间，找出长期未玩的大体积游戏。", DirExists(steamCommon) ? steamCommon : steamApps);
            }

            string epicDir = FirstExistingOnDrives("Program Files\\Epic Games", "Epic Games") ?? FirstExisting(Path.Combine(programData, "Epic"));
            if (epicDir != null) AddItem(items, "epic", "game", "Epic 游戏库", "已检测到安装", "分析 Epic 已安装游戏，找出长期未玩的大体积游戏。", epicDir);

            string wegameDir = FirstExistingOnDrives("WeGame", "WeGameApps", "Program Files\\WeGame", "Program Files (x86)\\WeGame");
            if (wegameDir != null) AddItem(items, "wegame", "game", "WeGame 游戏库", "已检测到安装", "分析 WeGame 已安装游戏与缓存，找出可清理的大体积内容。", wegameDir);

            string mihoyoDir = FirstExisting(Path.Combine(appData, "miHoYo")) ?? FirstExistingOnDrives("Genshin Impact Game", "Star Rail", "miHoYo Launcher", "Program Files\\Genshin Impact");
            if (mihoyoDir != null) AddItem(items, "mihoyo", "game", "米哈游游戏", "已检测到安装", "原神 / 崩坏：星穹铁道等，含大量可清理的缓存与日志。", mihoyoDir);

            // ---- 开发缓存（合并成一张卡列出检测到的种类）----
            List<string> devKinds = new List<string>();
            string devTarget = null;
            Action<string, string> dev = (name, path) => { if (path != null) { devKinds.Add(name); if (devTarget == null) devTarget = path; } };
            dev("npm", FirstExisting(Path.Combine(appData, "npm-cache"), Path.Combine(localApp, "npm-cache")));
            dev("NuGet", FirstExisting(Path.Combine(userProfile, ".nuget", "packages")));
            dev("Gradle", FirstExisting(Path.Combine(userProfile, ".gradle")));
            dev("pip", FirstExisting(Path.Combine(localApp, "pip", "Cache")));
            dev("pnpm", FirstExistingOnDrives(".pnpm-store"));
            if (devKinds.Count > 0) AddItem(items, "devcache", "dev", "开发缓存", "可重新拉取", "检测到 " + string.Join(" / ", devKinds) + " 缓存，删除后可自动重新下载，通常占用数 GB。", devTarget);

            // ---- 系统 ----
            string tempDir = Path.GetTempPath();
            long tempBytes = ShallowBytes(tempDir) + ShallowBytes(Path.Combine(windows, "Temp"));
            if (tempBytes > 0) AddItem(items, "temp", "system", "系统临时文件", "常见占用点", "Temp、更新残留、崩溃转储等，仅第一层文件已约 " + StorageFormatting.FormatBytes(tempBytes) + "，通常可安全清理。", tempDir);

            string chromeCache = Path.Combine(localApp, "Google", "Chrome", "User Data", "Default", "Cache");
            string edgeCache = Path.Combine(localApp, "Microsoft", "Edge", "User Data", "Default", "Cache");
            bool chrome = DirExists(chromeCache);
            bool edge = DirExists(edgeCache);
            if (chrome || edge)
            {
                string which = chrome && edge ? "Chrome 与 Edge" : (chrome ? "Chrome" : "Edge");
                AddItem(items, "browser", "system", "浏览器缓存", "常见占用点", which + " 的缓存目录（Cache / Code Cache 等），可安全清理并会自动重建。", chrome ? chromeCache : edgeCache);
            }

            long rbBytes, rbItems;
            QueryRecycleBin(out rbBytes, out rbItems);
            if (rbItems > 0) AddItem(items, "recyclebin", "system", "回收站", "可清空", "回收站有 " + rbItems + " 个项目，合计 " + StorageFormatting.FormatBytes(rbBytes) + "，确认后可清空释放。", Path.Combine(systemDrive, "$Recycle.Bin"));

            string winOld = Path.Combine(systemDrive, "Windows.old");
            if (DirExists(winOld)) AddItem(items, "winold", "system", "Windows.old", "升级残留", "上次系统升级保留的旧系统，通常占用 10–30 GB，确认无需回滚后可删除。", winOld);

            string wuCache = Path.Combine(windows, "SoftwareDistribution", "Download");
            if (DirExists(wuCache))
            {
                long wuBytes = ShallowBytes(wuCache);
                string size = wuBytes > 0 ? "（第一层约 " + StorageFormatting.FormatBytes(wuBytes) + "）" : "";
                AddItem(items, "winupdate", "system", "Windows 更新缓存", "常见占用点", "Windows 更新下载缓存" + size + "，更新完成后可安全清理。", wuCache);
            }

            // ---- 下载 ----
            string downloadsDir = Path.Combine(userProfile, "Downloads");
            int dlCount;
            long dlBytes;
            CountInstallers(downloadsDir, out dlCount, out dlBytes);
            if (dlCount > 0) AddItem(items, "downloads", "download", "Downloads 安装包", "常见占用点", "下载目录有 " + dlCount + " 个安装包 / 压缩包，合计 " + StorageFormatting.FormatBytes(dlBytes) + "，已安装的可安全移除。", downloadsDir);

            return new { items = items.ToArray() };
        }

        private static void AddItem(List<object> items, string key, string category, string title, string chip, string desc, string targetPath)
        {
            items.Add(new { key, category, title, chip, desc, installed = true, targetPath, drive = DriveLetterOf(targetPath) });
        }

        // 遍历固定盘符，返回第一个存在的 <盘符>\relative；用于探测装在非系统盘的游戏/缓存。
        private static string FirstExistingOnDrives(params string[] relatives)
        {
            foreach (DriveInfo d in DriveInfo.GetDrives())
            {
                try { if (d.DriveType != DriveType.Fixed || !d.IsReady) continue; }
                catch { continue; }
                for (int i = 0; i < relatives.Length; i++)
                {
                    string p = Path.Combine(d.Name, relatives[i]);
                    if (DirExists(p)) return p;
                }
            }
            return null;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct SHQUERYRBINFO
        {
            public int cbSize;
            public long i64Size;
            public long i64NumItems;
        }

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        private static extern int SHQueryRecycleBin(string pszRootPath, ref SHQUERYRBINFO pSHQueryRBInfo);

        // 全部盘符的回收站合计大小与项数；失败时返回 0（静默）。
        private static void QueryRecycleBin(out long bytes, out long count)
        {
            bytes = 0;
            count = 0;
            try
            {
                SHQUERYRBINFO info = new SHQUERYRBINFO();
                info.cbSize = Marshal.SizeOf(typeof(SHQUERYRBINFO));
                if (SHQueryRecycleBin(null, ref info) == 0)
                {
                    bytes = info.i64Size;
                    count = info.i64NumItems;
                }
            }
            catch
            {
                // P/Invoke 不可用时按空回收站处理
            }
        }

        private static bool DirExists(string path)
        {
            try { return !string.IsNullOrEmpty(path) && Directory.Exists(path); }
            catch { return false; }
        }

        // 返回候选中第一个真实存在的目录，均不存在时返回 null。
        private static string FirstExisting(params string[] candidates)
        {
            if (candidates == null) return null;
            for (int i = 0; i < candidates.Length; i++)
            {
                if (DirExists(candidates[i])) return candidates[i];
            }
            return null;
        }

        // 取路径所在盘符（不含冒号），无法解析时回退到系统盘 C。
        private static string DriveLetterOf(string path)
        {
            try
            {
                string root = Path.GetPathRoot(path);
                if (!string.IsNullOrEmpty(root) && char.IsLetter(root[0]))
                    return char.ToUpperInvariant(root[0]).ToString();
            }
            catch
            {
                // 回退到系统盘
            }
            return "C";
        }

        private static string TrySteamRoot()
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam"))
                {
                    string p = key != null ? key.GetValue("SteamPath") as string : null;
                    if (!string.IsNullOrWhiteSpace(p) && DirExists(p)) return p;
                }
            }
            catch
            {
                // 无注册表项或读取受限时退回盘符探测
            }

            foreach (DriveInfo d in DriveInfo.GetDrives())
            {
                try
                {
                    if (d.DriveType != DriveType.Fixed || !d.IsReady) continue;
                    string a = Path.Combine(d.Name, "Steam");
                    if (DirExists(a)) return a;
                    string b = Path.Combine(d.Name, "Program Files (x86)", "Steam");
                    if (DirExists(b)) return b;
                }
                catch
                {
                    // 跳过异常盘符
                }
            }
            return null;
        }

        // 只累加目录第一层文件的字节，不递归；无权限或异常项跳过。
        private static long ShallowBytes(string dir)
        {
            long total = 0;
            try
            {
                DirectoryInfo di = new DirectoryInfo(dir);
                if (!di.Exists) return 0;
                foreach (FileInfo f in di.EnumerateFiles())
                {
                    try { total += f.Length; } catch { }
                }
            }
            catch
            {
                // 目录不可访问时按 0 处理
            }
            return total;
        }

        // 统计目录第一层的安装包 / 压缩包数量与合计字节。
        private static void CountInstallers(string dir, out int count, out long bytes)
        {
            count = 0;
            bytes = 0;
            string[] exts = { ".exe", ".msi", ".zip", ".7z", ".rar" };
            try
            {
                DirectoryInfo di = new DirectoryInfo(dir);
                if (!di.Exists) return;
                foreach (FileInfo f in di.EnumerateFiles())
                {
                    if (Array.IndexOf(exts, f.Extension.ToLowerInvariant()) < 0) continue;
                    try { bytes += f.Length; count++; } catch { }
                }
            }
            catch
            {
                // 目录不可访问时保持 0
            }
        }

        private object SaveSettings(JObject parameters)
        {
            JToken payload = parameters?["settings"];
            if (payload == null) throw new ArgumentException("settings payload missing");
            ApplicationSettings incoming = payload.ToObject<ApplicationSettings>();
            dependencies.SettingsStore.Save(incoming);
            settings = incoming;
            return null;
        }

        private object TestAi(JObject parameters)
        {
            ApplicationSettings testSettings = parameters?["settings"] != null
                ? parameters["settings"].ToObject<ApplicationSettings>()
                : settings;
            AiConnectionTestResult result = Advisor.TestConnection(testSettings);
            return new { ok = result.Success, message = result.Message };
        }

        // ---- scan ----

        private object ScanStart(JObject parameters)
        {
            ApplicationSettings settings = this.settings;
            string location = NormalizeLocation(Str(parameters, "location"));
            ScanRequest request = BuildScanRequest(location, settings, Str(parameters, "sortMode"), 1, -1L);

            StorageItem root = dependencies.ScanProvider.Scan(request);
            lastRoot = root;
            sessionTemplate = new ScanRequest
            {
                Location = root.Path,
                SortMode = request.SortMode,
                MinSizeBytes = request.MinSizeBytes,
                PerLevelLimit = request.PerLevelLimit,
                LoadDepth = 1,
                SessionIdentity = root.SessionIdentity,
                SessionNodeId = root.SessionNodeId
            };

            return new
            {
                rootNodeId = root.SessionNodeId,
                sessionId = root.SessionIdentity,
                path = root.Path,
                bytes = root.Bytes,
                totalFiles = root.TotalFileCount,
                totalDirs = root.TotalDirectoryCount,
                children = MapChildren(root)
            };
        }

        private object ScanChildren(JObject parameters)
        {
            string path = Str(parameters, "path");
            ScanRequest request = new ScanRequest
            {
                Location = path,
                LoadDepth = 1,
                SortMode = sessionTemplate != null ? sessionTemplate.SortMode : ScanSortMode.Allocated,
                MinSizeBytes = sessionTemplate != null ? sessionTemplate.MinSizeBytes : -1L,
                PerLevelLimit = sessionTemplate != null ? sessionTemplate.PerLevelLimit : -1,
                SessionIdentity = Str(parameters, "sessionId"),
                SessionNodeId = Int(parameters, "nodeId", -1)
            };

            StorageItem node = dependencies.ScanProvider.Scan(request);
            return new { children = MapChildren(node) };
        }

        private static ScanRequest BuildScanRequest(string location, ApplicationSettings settings, string sortMode, int loadDepth, long minBytes)
        {
            ScanSortMode mode = settings.Scan != null ? settings.Scan.SortMode : ScanSortMode.Allocated;
            if (string.Equals(sortMode, "logical", StringComparison.OrdinalIgnoreCase)) mode = ScanSortMode.Logical;
            else if (string.Equals(sortMode, "allocated", StringComparison.OrdinalIgnoreCase)) mode = ScanSortMode.Allocated;

            return new ScanRequest
            {
                Location = location,
                LoadDepth = loadDepth,
                SortMode = mode,
                MinSizeBytes = minBytes,
                PerLevelLimit = settings.Scan != null ? settings.Scan.PerLevelLimit : -1
            };
        }

        private static object[] MapChildren(StorageItem item)
        {
            List<object> list = new List<object>();
            if (item != null && item.Children != null)
            {
                for (int i = 0; i < item.Children.Count; i++) list.Add(MapChild(item.Children[i]));
            }
            return list.ToArray();
        }

        private static object MapChild(StorageItem c)
        {
            return new
            {
                nodeId = c.SessionNodeId,
                name = c.Name,
                path = c.Path,
                bytes = c.Bytes,
                isDir = c.IsDirectory,
                files = c.TotalFileCount,
                hasChildren = c.HasChildren
            };
        }

        // ---- ai ----

        private object AiChat(JObject parameters)
        {
            ApplicationSettings settings = this.settings;
            List<AiChatMessage> messages = ReadMessages(parameters?["messages"] as JArray);
            AiChatResult result = Advisor.Complete(messages, settings);
            if (result == null || !result.Success) throw new InvalidOperationException(result == null ? "AI 无响应" : result.Error);
            return new { content = result.Content, tokens = result.TotalTokens };
        }

        private object AiReport(JObject parameters)
        {
            if (lastRoot == null) throw new InvalidOperationException("尚未扫描，无法生成报告。");
            ApplicationSettings settings = this.settings;

            string structureSummary = BuildDirectorySummary(lastRoot.Path, 2, 40);
            string reportPrompt = "以下是刚完成的磁盘扫描目录统计。请输出严格 JSON（不要任何解释文字、不要代码块标记）：" +
                "{\"safe_bytes\":整数,\"confirm_bytes\":整数,\"system_bytes\":整数,\"summary\":\"120字以内的中文总结\"," +
                "\"classified\":[{\"path\":\"目录完整路径\",\"tag\":\"safe|confirm|system\"}]}。" +
                "safe=可安全清理（缓存/临时/日志等可再生成内容），confirm=需人工确认，system=系统保留勿动。" +
                "classified 只包含你能明确判断的顶层目录，path 必须取自下方统计中出现的路径。\r\n\r\n" + structureSummary;

            List<AiChatMessage> messages = new List<AiChatMessage>
            {
                new AiChatMessage(AiChatMessage.SystemRole, BuildChatSystemPrompt()),
                new AiChatMessage(AiChatMessage.UserRole, reportPrompt)
            };

            AiChatResult result = Advisor.Complete(messages, settings);
            if (result == null || !result.Success) throw new InvalidOperationException(result == null ? "AI 无响应" : result.Error);

            JObject parsed = TryParseJsonObject(result.Content);
            if (parsed == null) return new { raw = result.Content, tokens = result.TotalTokens };

            List<object> classified = new List<object>();
            JArray items = parsed["classified"] as JArray;
            if (items != null)
            {
                foreach (JToken it in items)
                {
                    string path = it["path"]?.ToString();
                    string tag = it["tag"]?.ToString();
                    if (!string.IsNullOrWhiteSpace(path) && !string.IsNullOrWhiteSpace(tag))
                        classified.Add(new { path = path.TrimEnd('\\'), tag = tag.Trim().ToLowerInvariant() });
                }
            }

            return new
            {
                safeBytes = ReadLong(parsed, "safe_bytes"),
                confirmBytes = ReadLong(parsed, "confirm_bytes"),
                systemBytes = ReadLong(parsed, "system_bytes"),
                summary = parsed["summary"]?.ToString(),
                classified = classified.ToArray(),
                tokens = result.TotalTokens
            };
        }

        private List<AiChatMessage> ReadMessages(JArray array)
        {
            List<AiChatMessage> messages = new List<AiChatMessage>();
            if (array == null) return messages;
            foreach (JToken m in array)
            {
                string role = m["role"]?.ToString();
                string content = m["content"]?.ToString();
                if (!string.IsNullOrEmpty(role)) messages.Add(new AiChatMessage(role, content ?? string.Empty));
            }
            return messages;
        }

        private string BuildChatSystemPrompt()
        {
            string driveRoot = "C:\\";
            try
            {
                string root = lastRoot != null ? Path.GetPathRoot(lastRoot.Path) : null;
                if (!string.IsNullOrWhiteSpace(root)) driveRoot = root;
            }
            catch
            {
                // 保底根目录
            }

            return "你是 Windows 磁盘清理顾问，帮助用户理解磁盘占用并判断哪些内容可以安全清理。" +
                "回答用简体中文，简明扼要，重点给出可执行的清理建议与风险提示。" +
                "只讨论 " + driveRoot + " 范围内的内容。不要建议删除系统核心文件、应用主体和用户文档。";
        }

        // 目录统计摘要：只发名称/大小/文件数，不发文件内容。
        private string BuildDirectorySummary(string rootPath, int depth, int perLevelLimit)
        {
            StringBuilder summary = new StringBuilder();
            try
            {
                ScanRequest request = new ScanRequest
                {
                    Location = rootPath,
                    LoadDepth = depth,
                    PerLevelLimit = perLevelLimit,
                    MinSizeBytes = -1L,
                    SortMode = sessionTemplate != null ? sessionTemplate.SortMode : ScanSortMode.Allocated
                };

                if (sessionTemplate != null && SamePath(rootPath, sessionTemplate.Location))
                {
                    request.SessionIdentity = sessionTemplate.SessionIdentity;
                    request.SessionNodeId = sessionTemplate.SessionNodeId;
                }

                StorageItem item = dependencies.ScanProvider.Scan(request);
                AppendDirectorySummary(summary, item, 0, perLevelLimit);
            }
            catch (Exception ex)
            {
                summary.Append(rootPath).Append("（统计失败：").Append(ex.Message).Append("）");
            }

            if (summary.Length > 8000) summary.Length = 8000;
            return summary.ToString();
        }

        private static void AppendDirectorySummary(StringBuilder summary, StorageItem item, int indent, int perLevelLimit)
        {
            if (item == null) return;
            if (summary.Length > 7600) return;

            summary.Append('\r').Append('\n');
            for (int i = 0; i < indent; i++) summary.Append("  ");
            summary.Append(indent == 0 ? item.Path : item.Name)
                .Append("  ").Append(StorageFormatting.FormatBytes(item.Bytes));
            if (item.IsDirectory && item.TotalFileCount > 0) summary.Append("  ").Append(item.TotalFileCount).Append("文件");

            if (!item.IsDirectory || item.Children == null) return;
            int emitted = 0;
            for (int i = 0; i < item.Children.Count && emitted < perLevelLimit; i++)
            {
                AppendDirectorySummary(summary, item.Children[i], indent + 1, perLevelLimit);
                emitted++;
            }
        }

        // ---- suggestions ----

        private object SuggestAnalyze(JObject parameters)
        {
            ApplicationSettings settings = this.settings;
            bool aiEnabled = settings.Ai != null && settings.Ai.Enabled;
            string location = NormalizeLocation(Str(parameters, "location"));

            ScanRequest request = BuildScanRequest(location, settings, null, -1, -1L);
            StorageItem root = dependencies.ScanProvider.Scan(request);

            long configured = settings.Scan != null && settings.Scan.MinSizeMb > 0
                ? settings.Scan.MinSizeMb * 1024L * 1024L / 2L
                : -1L;
            long minBytes = Math.Max(aiEnabled ? 67108864L : 16777216L, configured);
            int maxCount = (settings.Ai != null ? settings.Ai.MaxSuggestions : 30) * 4;

            IList<CleanupCandidate> candidates = dependencies.CandidatePlanner.BuildCandidates(root, minBytes, maxCount);
            IList<CleanupSuggestion> suggestions = aiEnabled
                ? Advisor.Analyze(root, candidates, settings)
                : dependencies.LocalAdvisor.Analyze(root, candidates, settings);

            List<object> items = new List<object>();
            if (suggestions != null)
            {
                for (int i = 0; i < suggestions.Count; i++)
                {
                    CleanupSuggestion s = suggestions[i];
                    SandboxEvaluation ev = dependencies.DeletionWorkflow.Evaluate(s.Path, settings.Sandbox);
                    items.Add(new
                    {
                        path = s.Path,
                        name = s.Name,
                        bytes = s.Bytes,
                        isDir = s.IsDirectory,
                        risk = s.Risk.ToString().ToLowerInvariant(),
                        source = s.Source,
                        reason = s.Reason,
                        sandboxOk = ev != null && ev.Action == SandboxAction.Allow,
                        sandboxNote = ev == null ? null : ev.Message
                    });
                }
            }

            return new { items = items.ToArray() };
        }

        // ---- deletion ----

        private object DelEvaluate(JObject parameters)
        {
            ApplicationSettings settings = this.settings;
            List<object> results = new List<object>();
            JArray paths = parameters?["paths"] as JArray;
            if (paths != null)
            {
                foreach (JToken t in paths)
                {
                    string path = t.ToString();
                    SandboxEvaluation ev = dependencies.DeletionWorkflow.Evaluate(path, settings.Sandbox);
                    results.Add(new
                    {
                        path,
                        ok = ev != null && ev.Action == SandboxAction.Allow,
                        action = ev == null ? "require" : ev.Action.ToString().ToLowerInvariant(),
                        note = ev == null ? null : ev.Message
                    });
                }
            }
            return new { items = results.ToArray() };
        }

        private object DelRun(JObject parameters)
        {
            ApplicationSettings settings = this.settings;
            settings.Sandbox.UseRecycleBin = Bool(parameters, "useRecycleBin", settings.Sandbox.UseRecycleBin);

            JArray array = parameters?["items"] as JArray;
            List<JObject> items = new List<JObject>();
            if (array != null) foreach (JToken t in array) { JObject o = t as JObject; if (o != null) items.Add(o); }

            int total = items.Count;
            List<object> results = new List<object>();
            for (int i = 0; i < items.Count; i++)
            {
                JObject it = items[i];
                string path = it["path"]?.ToString();

                if (IsProtectedTarget(path))
                {
                    results.Add(new { path, ok = false, message = "为避免误删，拒绝删除扫描根或磁盘根目录。" });
                    host.PostEvent("del.progress", new { path, done = i + 1, total });
                    continue;
                }

                CleanupSuggestion suggestion = new CleanupSuggestion
                {
                    Path = path,
                    Name = it["name"]?.ToString(),
                    Bytes = ReadLong(it, "bytes"),
                    IsDirectory = it["isDir"]?.ToObject<bool>() ?? false,
                    Risk = CleanupRisk.High,
                    Score = 1,
                    Selected = true,
                    Reason = "用户从 Web UI 删除。",
                    Source = "Web UI",
                    Status = CleanupStatus.Pending
                };
                suggestion.Sandbox = dependencies.DeletionWorkflow.Evaluate(path, settings.Sandbox);

                CleanupResult result;
                try
                {
                    result = dependencies.DeletionWorkflow.Delete(suggestion, settings.Sandbox).Result;
                }
                catch (Exception ex)
                {
                    result = new CleanupResult { Path = path, Success = false, Message = ex.Message };
                }

                results.Add(new { path, ok = result != null && result.Success, message = result == null ? null : result.Message });
                host.PostEvent("del.progress", new { path, done = i + 1, total });
            }

            return new { results = results.ToArray() };
        }

        private bool IsProtectedTarget(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return true;
            if (lastRoot != null && SamePath(path, lastRoot.Path)) return true;
            try
            {
                string root = Path.GetPathRoot(path);
                if (!string.IsNullOrEmpty(root) && SamePath(path, root)) return true;
            }
            catch
            {
                return true;
            }
            return false;
        }

        // ---- helpers ----

        private static string NormalizeLocation(string location)
        {
            if (string.IsNullOrWhiteSpace(location)) return location;
            location = location.Trim();
            if (location.Length == 2 && location[1] == ':') location += "\\";
            return location;
        }

        private static bool SamePath(string a, string b)
        {
            return string.Equals((a ?? string.Empty).TrimEnd('\\'), (b ?? string.Empty).TrimEnd('\\'), StringComparison.OrdinalIgnoreCase);
        }

        private static JObject TryParseJsonObject(string content)
        {
            if (string.IsNullOrWhiteSpace(content)) return null;
            try
            {
                string json = content.Trim();
                int start = json.IndexOf('{');
                int end = json.LastIndexOf('}');
                if (start >= 0 && end > start) json = json.Substring(start, end - start + 1);
                return JObject.Parse(json);
            }
            catch
            {
                return null;
            }
        }

        private static long ReadLong(JObject o, string key)
        {
            JToken t = o?[key];
            if (t == null || t.Type == JTokenType.Null) return 0L;
            try { return t.ToObject<long>(); } catch { return 0L; }
        }

        private static string Str(JObject parameters, string key)
        {
            return parameters?[key]?.ToString();
        }

        private static int Int(JObject parameters, string key, int fallback)
        {
            JToken t = parameters?[key];
            if (t == null || t.Type == JTokenType.Null) return fallback;
            try { return t.ToObject<int>(); } catch { return fallback; }
        }

        private static bool Bool(JObject parameters, string key, bool fallback)
        {
            JToken t = parameters?[key];
            if (t == null || t.Type == JTokenType.Null) return fallback;
            try { return t.ToObject<bool>(); } catch { return fallback; }
        }
    }
}
