using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using AiCleanVolume.Core.Models;
using AiCleanVolume.Core.Services;
using Newtonsoft.Json;


namespace AiCleanVolume.Desktop.Services
{
    public sealed partial class FolderSizeRankerScanProvider : IScanProvider
    {
        private readonly string executablePath;

        private readonly object syncRoot = new object();

        private ScanSession currentTreeSession;

        public FolderSizeRankerScanProvider()
        {
            executablePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "folder-size-ranker-cli.exe");
        }

        public StorageItem Scan(ScanRequest request)
        {
            if (request == null) throw new ArgumentNullException("request");
            if (string.IsNullOrWhiteSpace(request.Location)) throw new InvalidOperationException("扫描位置不能为空。");
            if (!File.Exists(executablePath)) throw new FileNotFoundException("未找到 folder-size-ranker-cli.exe。", executablePath);

            request.Location = NormalizeLocation(request.Location);
            return request.LoadDepth >= 0 ? ScanPartial(request) : ScanFull(request);
        }

        public void ClearCache()
        {
            lock (syncRoot)
            {
                ClearCurrentTreeSessionNoLock();
            }
        }

        internal static StorageItem CloneTree(StorageItem source)
        {
            if (source == null) return null;

            StorageItem clone = new StorageItem();
            clone.Path = source.Path;
            clone.Name = source.Name;
            clone.Bytes = source.Bytes;
            clone.IsDirectory = source.IsDirectory;
            clone.HasChildren = source.HasChildren;
            clone.ChildrenLoaded = source.ChildrenLoaded;
            clone.DirectFileCount = source.DirectFileCount;
            clone.TotalFileCount = source.TotalFileCount;
            clone.TotalDirectoryCount = source.TotalDirectoryCount;
            clone.SessionIdentity = source.SessionIdentity;
            clone.SessionNodeId = source.SessionNodeId;
            for (int i = 0; i < source.Children.Count; i++)
            {
                clone.Children.Add(CloneTree(source.Children[i]));
            }

            return clone;
        }

        private StorageItem ScanFull(ScanRequest request)
        {
            ProcessStartInfo startInfo = CreateStartInfo(request);
            using (Process process = Process.Start(startInfo))
            {
                ScanSession session = null;
                Exception parseError = null;

                try
                {
                    using (JsonTextReader reader = new JsonTextReader(process.StandardOutput))
                    {
                        session = BuildCompactSession(reader, request.Location, BuildTreeTemplateKey(request));
                    }
                }
                catch (Exception ex)
                {
                    parseError = ex;
                }

                string error = process.StandardError.ReadToEnd();
                process.WaitForExit();
                if (process.ExitCode != 0)
                {
                    StorageItem fallback = TryScanWithPlatformApi(request, error);
                    if (fallback != null) return fallback;
                    throw new InvalidOperationException("folder-size-ranker-cli 执行失败：" + error);
                }

                if (parseError != null) throw new InvalidOperationException("扫描结果解析失败：" + parseError.Message, parseError);
                if (session == null) throw new InvalidOperationException("扫描结果为空或 JSON 无法解析。");
                return MaterializeDirectory(session, session.RootNodeId, int.MaxValue, true);
            }
        }

        private StorageItem ScanPartial(ScanRequest request)
        {
            try
            {
                ScanSession session = EnsureTreeSession(request);
                int nodeId = ResolveNodeId(session, request);
                if (nodeId < 0)
                {
                    throw new InvalidOperationException("目录树会话未包含路径：" + request.Location);
                }

                return MaterializeDirectory(session, nodeId, request.LoadDepth, IsSamePath(session.RootPath, request.Location));
            }
            catch (CliExecutionException ex)
            {
                StorageItem fallback = TryScanWithPlatformApi(request, ex.Message);
                if (fallback != null) return fallback;
                throw;
            }
        }

        private ScanSession EnsureTreeSession(ScanRequest request)
        {
            string templateKey = BuildTreeTemplateKey(request);

            lock (syncRoot)
            {
                if (IsCompatibleTreeSession(currentTreeSession, templateKey, request))
                {
                    return currentTreeSession;
                }

                ScanSession session = null;
                try
                {
                    session = BuildTreeSession(request, templateKey);
                    ClearCurrentTreeSessionNoLock();
                    currentTreeSession = session;
                    return currentTreeSession;
                }
                catch
                {
                    if (session != null) session.Dispose();
                    throw;
                }
            }
        }

        private ScanSession BuildTreeSession(ScanRequest request, string templateKey)
        {
            ProcessStartInfo startInfo = CreateStartInfo(request);
            using (Process process = Process.Start(startInfo))
            {
                ScanSession session = null;
                Exception parseError = null;

                try
                {
                    using (JsonTextReader reader = new JsonTextReader(process.StandardOutput))
                    {
                        session = BuildCompactSession(reader, request.Location, templateKey);
                    }
                }
                catch (Exception ex)
                {
                    parseError = ex;
                }

                string error = process.StandardError.ReadToEnd();
                process.WaitForExit();
                if (process.ExitCode != 0)
                {
                    throw new CliExecutionException(error);
                }

                if (parseError != null) throw new InvalidOperationException("扫描结果解析失败：" + parseError.Message, parseError);
                if (session == null) throw new InvalidOperationException("扫描结果为空或 JSON 无法解析。");
                return session;
            }
        }
    }
}
