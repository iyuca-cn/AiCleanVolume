using System;
using System.IO;
using System.Linq;
using AiCleanVolume.Core.Domain.Settings;
using AiCleanVolume.Desktop.Composition;
using Newtonsoft.Json.Linq;

namespace AiCleanVolume.Desktop.Presentation.WebShell
{
    // JS 桥的方法分发。WebShellWindow 解析信封后调用 Invoke，返回值序列化进 result；
    // 抛出的异常 message 进 error。未实现的方法统一抛 "not_implemented"，阶段 B 补齐。
    internal sealed class WebBridge
    {
        private readonly MainWindowDependencies dependencies;
        private readonly WebShellWindow host;

        public WebBridge(MainWindowDependencies dependencies, WebShellWindow host)
        {
            this.dependencies = dependencies;
            this.host = host;
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
                case "env.openPath": dependencies.ExplorerService.OpenPath(Str(parameters, "path"), false); return null;
                case "env.restartElevated": host.RestartElevated(); return null;

                case "settings.get": return dependencies.SettingsStore.Load();
                case "settings.save": return SaveSettings(parameters);

                default:
                    // scan.* / ai.* / suggest.* / del.* / settings.testAi 阶段 B 实现
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

        private object SaveSettings(JObject parameters)
        {
            JToken payload = parameters?["settings"];
            if (payload == null) throw new ArgumentException("settings payload missing");
            ApplicationSettings settings = payload.ToObject<ApplicationSettings>();
            dependencies.SettingsStore.Save(settings);
            return null;
        }

        private static string Str(JObject parameters, string key)
        {
            return parameters?[key]?.ToString();
        }
    }
}
