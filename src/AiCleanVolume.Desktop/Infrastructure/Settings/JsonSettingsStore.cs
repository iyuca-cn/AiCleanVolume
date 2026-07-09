using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using AiCleanVolume.Core.Domain.Settings;
using AiCleanVolume.Core.Kernel.Ports;
using Newtonsoft.Json;

namespace AiCleanVolume.Desktop.Infrastructure.Settings
{
    // 多个进程实例可能同时读写 appsettings.json：读用共享句柄、写用临时文件原子替换，
    // 两侧都做短重试，读失败回退上次内存副本，写失败不抛出以免拖垮业务请求。
    public sealed class JsonSettingsStore : ISettingsStore
    {
        private const int RetryCount = 3;
        private const int RetryDelayMs = 100;
        private static readonly Encoding Utf8 = new UTF8Encoding(false);
        private readonly string path;
        private ApplicationSettings lastKnown;

        public JsonSettingsStore()
        {
            path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "appsettings.json");
        }

        public ApplicationSettings Load()
        {
            ApplicationSettings settings = ReadWithRetry();
            if (settings == null) settings = lastKnown ?? new ApplicationSettings();
            settings.EnsureDefaults();
            lastKnown = settings;
            Save(settings);
            return settings;
        }

        public void Save(ApplicationSettings settings)
        {
            if (settings == null) settings = new ApplicationSettings();
            settings.EnsureDefaults();
            lastKnown = settings;
            string json = JsonConvert.SerializeObject(settings, Formatting.Indented);
            WriteWithRetry(json);
        }

        private ApplicationSettings ReadWithRetry()
        {
            for (int attempt = 0; ; attempt++)
            {
                try
                {
                    if (!File.Exists(path)) return null;
                    string json;
                    // 允许 Delete 共享，写侧才能在读取过程中原子替换该文件。
                    using (FileStream fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                    using (StreamReader reader = new StreamReader(fs, Utf8))
                    {
                        json = reader.ReadToEnd();
                    }
                    return JsonConvert.DeserializeObject<ApplicationSettings>(json);
                }
                catch (IOException)
                {
                    if (attempt >= RetryCount) return null;
                    Thread.Sleep(RetryDelayMs);
                }
                catch (Exception)
                {
                    // 内容损坏或反序列化失败：回退上次副本，不重试
                    return null;
                }
            }
        }

        private void WriteWithRetry(string json)
        {
            string temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            for (int attempt = 0; ; attempt++)
            {
                try
                {
                    File.WriteAllText(temp, json, Utf8);
                    // 原子替换：MoveFileEx 直接改名覆盖，不像 File.Replace 会在冲突时留下 ~RF*.TMP 备份。
                    if (!File.Exists(path))
                    {
                        File.Move(temp, path);
                        return;
                    }
                    if (MoveFileEx(temp, path, MoveFileReplaceExisting | MoveFileWriteThrough)) return;
                    throw new IOException("MoveFileEx 失败：" + Marshal.GetLastWin32Error());
                }
                catch (IOException)
                {
                    if (attempt >= RetryCount)
                    {
                        TryDelete(temp);
                        return; // 落盘失败不抛出，内存副本已更新
                    }
                    Thread.Sleep(RetryDelayMs);
                }
                catch (Exception)
                {
                    TryDelete(temp);
                    return;
                }
            }
        }

        private static void TryDelete(string file)
        {
            try { if (File.Exists(file)) File.Delete(file); }
            catch { }
        }

        private const int MoveFileReplaceExisting = 0x1;
        private const int MoveFileWriteThrough = 0x8;

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern bool MoveFileEx(string existingFileName, string newFileName, int flags);
    }
}
