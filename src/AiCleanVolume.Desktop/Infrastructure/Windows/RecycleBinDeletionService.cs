using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using AiCleanVolume.Core.Application.Deletion;
using AiCleanVolume.Core.Domain.Cleanup;
using AiCleanVolume.Core.Kernel.Ports;

namespace AiCleanVolume.Desktop.Infrastructure.Windows
{
    public sealed class RecycleBinDeletionService : IDeletionService
    {
        private const string UnlockerCliRelativePath = @"Tools\IObitUnlocker\IObitUnlockerCLI.exe";

        public CleanupResult Delete(CleanupSuggestion suggestion, bool useRecycleBin, DeletionProgressState progress)
        {
            CleanupResult result = new CleanupResult();
            result.Path = suggestion == null ? null : suggestion.Path;

            try
            {
                if (suggestion == null || string.IsNullOrWhiteSpace(suggestion.Path))
                {
                    result.Success = false;
                    result.Message = "删除目标为空。";
                    return result;
                }

                string path = NormalizeInputPath(suggestion.Path);
                UpdateProgress(progress, "正在解锁并删除", path);

                if (!File.Exists(path) && !Directory.Exists(path))
                {
                    result.Success = true;
                    result.Message = "目标不存在，视为已删除。";
                    return result;
                }

                string unlockerCliPath = ResolveUnlockerCliPath();
                if (!File.Exists(unlockerCliPath))
                {
                    result.Success = false;
                    result.Message = "未找到 IObitUnlocker CLI，无法删除。路径：" + unlockerCliPath;
                    return result;
                }

                ProcessExecution execution = RunUnlockerDelete(unlockerCliPath, path);
                bool targetRemoved = !File.Exists(path) && !Directory.Exists(path);
                if (execution.ExitCode == 0 && targetRemoved)
                {
                    result.Success = true;
                    result.Message = suggestion.IsDirectory ? "已通过 IObitUnlocker 删除文件夹。" : "已通过 IObitUnlocker 删除文件。";
                    return result;
                }

                result.Success = false;
                result.Message = BuildFailureMessage(path, execution, targetRemoved);
                return result;
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.Message = ex.Message;
                result.Exception = ex;
                return result;
            }
        }

        private static ProcessExecution RunUnlockerDelete(string unlockerCliPath, string path)
        {
            ProcessStartInfo startInfo = new ProcessStartInfo();
            startInfo.FileName = unlockerCliPath;
            startInfo.Arguments = "delete " + QuoteArgument(path);
            startInfo.WorkingDirectory = Path.GetDirectoryName(unlockerCliPath);
            startInfo.UseShellExecute = false;
            startInfo.CreateNoWindow = true;
            startInfo.RedirectStandardOutput = true;
            startInfo.RedirectStandardError = true;

            StringBuilder output = new StringBuilder();
            StringBuilder error = new StringBuilder();
            using (Process process = new Process())
            {
                process.StartInfo = startInfo;
                process.OutputDataReceived += delegate(object sender, DataReceivedEventArgs args)
                {
                    if (args.Data != null) output.AppendLine(args.Data);
                };
                process.ErrorDataReceived += delegate(object sender, DataReceivedEventArgs args)
                {
                    if (args.Data != null) error.AppendLine(args.Data);
                };

                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
                process.WaitForExit();

                return new ProcessExecution(process.ExitCode, output.ToString(), error.ToString());
            }
        }

        private static string BuildFailureMessage(string path, ProcessExecution execution, bool targetRemoved)
        {
            StringBuilder message = new StringBuilder();
            message.Append("IObitUnlocker 删除失败。");
            if (execution.ExitCode == 0 && !targetRemoved)
            {
                message.Append("CLI 已返回成功，但目标仍存在。");
            }
            else
            {
                message.Append("退出码：").Append(execution.ExitCode).Append("。");
            }

            string details = execution.GetDetails();
            if (!string.IsNullOrWhiteSpace(details))
            {
                message.Append(Environment.NewLine).Append(details.Trim());
            }

            message.Append(Environment.NewLine).Append("路径：").Append(path);
            return message.ToString();
        }

        private static void UpdateProgress(DeletionProgressState progress, string stage, string path)
        {
            if (progress != null) progress.Update(stage, path);
        }

        private static string ResolveUnlockerCliPath()
        {
            return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, UnlockerCliRelativePath);
        }

        private static string NormalizeInputPath(string path)
        {
            string normalized = path.Trim().Trim('"').Replace('/', '\\');
            if (normalized.Length > 3) normalized = normalized.TrimEnd('\\');
            return normalized;
        }

        private static string QuoteArgument(string value)
        {
            if (string.IsNullOrEmpty(value)) return "\"\"";

            StringBuilder builder = new StringBuilder();
            builder.Append('"');
            int backslashes = 0;
            for (int i = 0; i < value.Length; i++)
            {
                char current = value[i];
                if (current == '\\')
                {
                    backslashes++;
                    continue;
                }

                if (current == '"')
                {
                    builder.Append('\\', backslashes * 2 + 1);
                    builder.Append('"');
                    backslashes = 0;
                    continue;
                }

                if (backslashes > 0)
                {
                    builder.Append('\\', backslashes);
                    backslashes = 0;
                }
                builder.Append(current);
            }

            if (backslashes > 0) builder.Append('\\', backslashes * 2);
            builder.Append('"');
            return builder.ToString();
        }

        private sealed class ProcessExecution
        {
            public ProcessExecution(int exitCode, string output, string error)
            {
                ExitCode = exitCode;
                Output = output ?? string.Empty;
                Error = error ?? string.Empty;
            }

            public int ExitCode { get; private set; }

            public string Output { get; private set; }

            public string Error { get; private set; }

            public string GetDetails()
            {
                if (string.IsNullOrWhiteSpace(Output)) return Error;
                if (string.IsNullOrWhiteSpace(Error)) return Output;
                return Output.TrimEnd() + Environment.NewLine + Error;
            }
        }
    }
}
