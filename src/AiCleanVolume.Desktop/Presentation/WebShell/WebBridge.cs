using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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

        public WebBridge(MainWindowDependencies dependencies, WebShellWindow host)
        {
            this.dependencies = dependencies;
            this.host = host;
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

                case "settings.get": return dependencies.SettingsStore.Load();
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

            if (DirExists(Path.Combine(documents, "WeChat Files"))
                || DirExists(Path.Combine(documents, "xwechat_files"))
                || DirExists(Path.Combine(userProfile, "Documents", "WeChat Files"))
                || DirExists(Path.Combine(userProfile, "Documents", "xwechat_files")))
            {
                items.Add(new
                {
                    key = "wechat",
                    title = "微信 PC 版",
                    chip = "已检测到安装",
                    desc = "缓存目录通常占用 5–20 GB，扫描后给出准确大小与可清理明细。",
                    installed = true
                });
            }

            string steamRoot = TrySteamRoot();
            if (steamRoot != null && DirExists(Path.Combine(steamRoot, "steamapps")))
            {
                items.Add(new
                {
                    key = "steam",
                    title = "Steam 游戏库",
                    chip = "已检测到安装",
                    desc = "将分析各游戏最后启动时间，找出长期未玩的大体积游戏。",
                    installed = true
                });
            }

            long tempBytes = ShallowBytes(Path.GetTempPath())
                + ShallowBytes(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Temp"));
            if (tempBytes > 0)
            {
                items.Add(new
                {
                    key = "temp",
                    title = "系统临时文件",
                    chip = "常见占用点",
                    desc = "Temp、更新残留、崩溃转储等，仅第一层文件已约 " + StorageFormatting.FormatBytes(tempBytes) + "，通常可安全清理。",
                    installed = true
                });
            }

            int dlCount;
            long dlBytes;
            CountInstallers(Path.Combine(userProfile, "Downloads"), out dlCount, out dlBytes);
            if (dlCount > 0)
            {
                items.Add(new
                {
                    key = "downloads",
                    title = "Downloads 安装包",
                    chip = "常见占用点",
                    desc = "下载目录有 " + dlCount + " 个安装包 / 压缩包，合计 " + StorageFormatting.FormatBytes(dlBytes) + "，已安装的可安全移除。",
                    installed = true
                });
            }

            bool chrome = DirExists(Path.Combine(localApp, "Google", "Chrome", "User Data", "Default", "Cache"));
            bool edge = DirExists(Path.Combine(localApp, "Microsoft", "Edge", "User Data", "Default", "Cache"));
            if (chrome || edge)
            {
                string which = chrome && edge ? "Chrome 与 Edge" : (chrome ? "Chrome" : "Edge");
                items.Add(new
                {
                    key = "browser",
                    title = "浏览器缓存",
                    chip = "常见占用点",
                    desc = which + " 的缓存目录（Cache / Code Cache 等），可安全清理并会自动重建。",
                    installed = true
                });
            }

            return new { items = items.ToArray() };
        }

        private static bool DirExists(string path)
        {
            try { return !string.IsNullOrEmpty(path) && Directory.Exists(path); }
            catch { return false; }
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
            ApplicationSettings settings = payload.ToObject<ApplicationSettings>();
            dependencies.SettingsStore.Save(settings);
            return null;
        }

        private object TestAi(JObject parameters)
        {
            ApplicationSettings settings = parameters?["settings"] != null
                ? parameters["settings"].ToObject<ApplicationSettings>()
                : dependencies.SettingsStore.Load();
            AiConnectionTestResult result = Advisor.TestConnection(settings);
            return new { ok = result.Success, message = result.Message };
        }

        // ---- scan ----

        private object ScanStart(JObject parameters)
        {
            ApplicationSettings settings = dependencies.SettingsStore.Load();
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
            ApplicationSettings settings = dependencies.SettingsStore.Load();
            List<AiChatMessage> messages = ReadMessages(parameters?["messages"] as JArray);
            AiChatResult result = Advisor.Complete(messages, settings);
            if (result == null || !result.Success) throw new InvalidOperationException(result == null ? "AI 无响应" : result.Error);
            return new { content = result.Content, tokens = result.TotalTokens };
        }

        private object AiReport(JObject parameters)
        {
            if (lastRoot == null) throw new InvalidOperationException("尚未扫描，无法生成报告。");
            ApplicationSettings settings = dependencies.SettingsStore.Load();

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
            ApplicationSettings settings = dependencies.SettingsStore.Load();
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
            ApplicationSettings settings = dependencies.SettingsStore.Load();
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
            ApplicationSettings settings = dependencies.SettingsStore.Load();
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
