using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Forms;
using AiCleanVolume.Desktop.Composition;
using AiCleanVolume.Desktop.Infrastructure.Windows;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace AiCleanVolume.Desktop.Presentation.WebShell
{
    // WebView2 承载设计稿页面：无边框窗体铺满 WebView2，虚拟主机映射输出目录 WebUi，
    // 页面通过 postMessage 走 WebBridge 调后端服务。
    public sealed class WebShellWindow : Form
    {
        private const string AppDisplayName = "AI智能清盘";
        private const string VirtualHost = "app.local";

        [DllImport("user32.dll")]
        private static extern bool ReleaseCapture();

        [DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

        private const int WM_NCLBUTTONDOWN = 0x00A1;
        private const int HTCAPTION = 0x2;

        private readonly WebView2 webView;
        private readonly WebBridge bridge;

        public WebShellWindow(MainWindowDependencies dependencies)
        {
            if (dependencies == null) throw new ArgumentNullException("dependencies");

            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.CenterScreen;
            Size = new System.Drawing.Size(1600, 980);
            MinimumSize = new System.Drawing.Size(1280, 760);
            Text = AppDisplayName;

            bridge = new WebBridge(dependencies, this);

            webView = new WebView2 { Dock = DockStyle.Fill };
            Controls.Add(webView);

            Load += async delegate { await InitializeWebViewAsync(); };
        }

        public bool IsElevated
        {
            get { return new WindowsPrivilegeService().IsProcessElevated(); }
        }

        private async Task InitializeWebViewAsync()
        {
            string userDataFolder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "AiCleanVolume", "WebView2");
            CoreWebView2Environment environment = await CoreWebView2Environment.CreateAsync(null, userDataFolder);
            await webView.EnsureCoreWebView2Async(environment);

            CoreWebView2 core = webView.CoreWebView2;
            core.Settings.AreDefaultContextMenusEnabled = false;
            core.Settings.IsZoomControlEnabled = false;
#if DEBUG
            core.Settings.AreDevToolsEnabled = true;
#else
            core.Settings.AreDevToolsEnabled = false;
#endif
            core.SetVirtualHostNameToFolderMapping(
                VirtualHost,
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "WebUi"),
                CoreWebView2HostResourceAccessKind.Allow);

            core.WebMessageReceived += OnWebMessageReceived;
            core.Navigate("https://" + VirtualHost + "/index.html");
        }

        private void OnWebMessageReceived(object sender, CoreWebView2WebMessageReceivedEventArgs e)
        {
            string raw = e.WebMessageAsJson;
            JObject envelope;
            try
            {
                envelope = JObject.Parse(raw);
            }
            catch
            {
                return;
            }

            string id = envelope["id"]?.ToString();
            string method = envelope["method"]?.ToString();
            JObject parameters = envelope["params"] as JObject;
            if (string.IsNullOrEmpty(method)) return;

            Task.Run(delegate
            {
                try
                {
                    object result = bridge.Invoke(method, parameters);
                    PostReply(new { id, ok = true, result });
                }
                catch (Exception ex)
                {
                    PostReply(new { id, ok = false, error = ex.Message });
                }
            });
        }

        private void PostReply(object payload)
        {
            PostJson(JsonConvert.SerializeObject(payload));
        }

        // 主动推送事件（扫描/删除进度等）：包成 {event, data} 信封，前端按 event 分发。
        public void PostEvent(string name, object data)
        {
            PostJson(JsonConvert.SerializeObject(new { @event = name, data }));
        }

        private void PostJson(string json)
        {
            if (IsDisposed) return;
            BeginInvoke((Action)delegate
            {
                if (webView.CoreWebView2 != null) webView.CoreWebView2.PostWebMessageAsJson(json);
            });
        }

        public void MinimizeWindow()
        {
            RunOnUi(delegate { WindowState = FormWindowState.Minimized; });
        }

        public void ToggleMaximize()
        {
            RunOnUi(delegate
            {
                WindowState = WindowState == FormWindowState.Maximized
                    ? FormWindowState.Normal
                    : FormWindowState.Maximized;
            });
        }

        public void CloseWindow()
        {
            RunOnUi(Close);
        }

        public void DragMove()
        {
            RunOnUi(delegate
            {
                ReleaseCapture();
                SendMessage(Handle, WM_NCLBUTTONDOWN, (IntPtr)HTCAPTION, IntPtr.Zero);
            });
        }

        public void RestartElevated()
        {
            RunOnUi(delegate
            {
                try
                {
                    ProcessStartInfo startInfo = new ProcessStartInfo(Application.ExecutablePath)
                    {
                        Verb = "runas",
                        UseShellExecute = true
                    };
                    Process.Start(startInfo);
                    Application.Exit();
                }
                catch (Exception)
                {
                    // 用户取消 UAC 时维持现状
                }
            });
        }

        private void RunOnUi(Action action)
        {
            if (IsDisposed) return;
            if (InvokeRequired) BeginInvoke(action);
            else action();
        }
    }
}
