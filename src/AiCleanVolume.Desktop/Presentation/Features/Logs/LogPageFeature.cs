using System;
using System.Windows.Forms;
using AiCleanVolume.Desktop.Presentation.Shared;
using AiCleanVolume.Desktop.Presentation.Shared.Antd;

namespace AiCleanVolume.Desktop.Presentation.Features.Logs
{
    /// <summary>日志面板：只读多行文本，按行追加运行日志。</summary>
    internal sealed class LogPageFeature
    {
        public AntdUI.Panel View { get; private set; }
        public AntdUI.Input LogInput { get; private set; }

        public LogPageFeature()
        {
            LogInput = AntdControlFactory.CreateInput("暂无日志");
            LogInput.Multiline = true;
            LogInput.ReadOnly = true;
            LogInput.Dock = DockStyle.Fill;

            View = AntdControlFactory.CreateFlatPanel();
            View.Dock = DockStyle.Fill;
            View.Padding = new Padding(8);
            View.Controls.Add(LogInput);
        }

        public void Append(string line)
        {
            if (string.IsNullOrEmpty(line) || LogInput == null || LogInput.IsDisposed) return;
            if (string.IsNullOrWhiteSpace(LogInput.Text)) LogInput.Text = line;
            else LogInput.Text += Environment.NewLine + line;
        }
    }
}
