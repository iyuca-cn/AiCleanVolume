using System;
using System.Collections.Generic;
using System.Text;
using AiCleanVolume.Core.Models;
using AiCleanVolume.Core.Services;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using RestSharp;


namespace AiCleanVolume.Desktop.Services
{
    public sealed partial class OpenAiCompatibleAdvisor : IAiCleanupAdvisor
    {
        private readonly IAiCleanupAdvisor fallback;

        private readonly Action<string> log;

        public OpenAiCompatibleAdvisor(IAiCleanupAdvisor fallback)
            : this(fallback, null)
        {
        }

        public OpenAiCompatibleAdvisor(IAiCleanupAdvisor fallback, Action<string> log)
        {
            this.fallback = fallback;
            this.log = log;
        }

        public IList<CleanupSuggestion> Analyze(StorageItem root, IList<CleanupCandidate> candidates, ApplicationSettings settings)
        {
            if (settings == null || settings.Ai == null || !settings.Ai.Enabled || string.IsNullOrWhiteSpace(settings.Ai.Endpoint) || string.IsNullOrWhiteSpace(settings.Ai.Model))
            {
                WriteLog("AI 未启用或配置不完整，使用本地规则。Enabled=" + (settings != null && settings.Ai != null && settings.Ai.Enabled) + " EndpointEmpty=" + (settings == null || settings.Ai == null || string.IsNullOrWhiteSpace(settings.Ai.Endpoint)) + " ModelEmpty=" + (settings == null || settings.Ai == null || string.IsNullOrWhiteSpace(settings.Ai.Model)));
                return fallback.Analyze(root, candidates, settings);
            }

            try
            {
                string prompt = BuildPrompt(root, candidates, settings.Ai.MaxSuggestions);
                string endpoint = NormalizeEndpoint(settings.Ai.Endpoint);
                string accessMode = AiSettings.NormalizeAccessMode(settings.Ai.AccessMode);
                string path = ResolveChatCompletionsPath(endpoint);
                WriteLog("AI 请求准备：mode=" + accessMode + " endpoint=" + endpoint + " path=" + path + " model=" + settings.Ai.Model + " candidates=" + (candidates == null ? 0 : candidates.Count) + " promptChars=" + prompt.Length + " maxSuggestions=" + settings.Ai.MaxSuggestions);
                RestClient client = new RestClient(endpoint);
                RestRequest request = new RestRequest(path, Method.POST);
                request.AddHeader("Content-Type", "application/json");
                string authMessage;
                if (!AddAuthHeaders(request, settings.Ai, accessMode, out authMessage))
                {
                    WriteLog(authMessage + "，使用本地规则。");
                    return fallback.Analyze(root, candidates, settings);
                }
                WriteLog(authMessage);
                string body = JsonConvert.SerializeObject(new
                {
                    model = settings.Ai.Model,
                    temperature = 0.1,
                    messages = new object[]
                    {
                        new { role = "system", content = settings.Ai.SystemPrompt },
                        new { role = "user", content = prompt }
                    }
                });
                request.AddParameter("application/json", body, ParameterType.RequestBody);
                WriteLog("AI 请求发送：POST " + endpoint + path + " bodyChars=" + body.Length + "。");

                DateTime startedAt = DateTime.UtcNow;
                IRestResponse response = client.Execute(request);
                TimeSpan elapsed = DateTime.UtcNow - startedAt;
                if (response == null || response.ResponseStatus != ResponseStatus.Completed || (int)response.StatusCode >= 400)
                {
                    WriteLog("AI 请求失败，使用本地规则。responseNull=" + (response == null) + BuildResponseSummary(response, elapsed));
                    return fallback.Analyze(root, candidates, settings);
                }
                WriteLog("AI 请求成功：" + BuildResponseSummary(response, elapsed));

                ChatCompletionResponse chat = JsonConvert.DeserializeObject<ChatCompletionResponse>(response.Content);
                if (chat == null || chat.choices == null || chat.choices.Count == 0 || chat.choices[0].message == null)
                {
                    WriteLog("AI 响应结构无效，使用本地规则。contentPreview=" + Preview(response.Content, 500));
                    return fallback.Analyze(root, candidates, settings);
                }

                string content = ExtractJson(chat.choices[0].message.content);
                IList<CleanupSuggestion> mapped = MapSuggestions(content, candidates);
                WriteLog("AI 响应解析完成：contentChars=" + (chat.choices[0].message.content == null ? 0 : chat.choices[0].message.content.Length) + " jsonChars=" + content.Length + " mapped=" + mapped.Count + "。");
                if (mapped.Count == 0)
                {
                    WriteLog("AI 没有映射到候选路径，使用本地规则。jsonPreview=" + Preview(content, 500));
                    return fallback.Analyze(root, candidates, settings);
                }
                return mapped;
            }
            catch (Exception ex)
            {
                WriteLog("AI 调用异常，使用本地规则：" + ex.GetType().Name + " " + ex.Message);
                return fallback.Analyze(root, candidates, settings);
            }
        }

        public AiConnectionTestResult TestConnection(ApplicationSettings settings)
        {
            if (settings == null || settings.Ai == null || string.IsNullOrWhiteSpace(settings.Ai.Endpoint) || string.IsNullOrWhiteSpace(settings.Ai.Model))
            {
                return AiConnectionTestResult.Fail("AI 配置不完整：请填写接口地址和模型。");
            }

            try
            {
                string endpoint = NormalizeEndpoint(settings.Ai.Endpoint);
                string accessMode = AiSettings.NormalizeAccessMode(settings.Ai.AccessMode);
                string path = ResolveChatCompletionsPath(endpoint);
                RestClient client = new RestClient(endpoint);
                RestRequest request = new RestRequest(path, Method.POST);
                request.AddHeader("Content-Type", "application/json");
                string authMessage;
                if (!AddAuthHeaders(request, settings.Ai, accessMode, out authMessage))
                {
                    return AiConnectionTestResult.Fail(authMessage);
                }
                WriteLog("AI 配置测试鉴权：" + authMessage);

                string body = JsonConvert.SerializeObject(new
                {
                    model = settings.Ai.Model,
                    temperature = 0,
                    max_tokens = 8,
                    messages = new object[]
                    {
                        new { role = "user", content = "请只回复 OK，用于连接测试。" }
                    }
                });
                request.AddParameter("application/json", body, ParameterType.RequestBody);
                WriteLog("AI 配置测试请求发送：POST " + endpoint + path + " model=" + settings.Ai.Model + "。");

                DateTime startedAt = DateTime.UtcNow;
                IRestResponse response = client.Execute(request);
                TimeSpan elapsed = DateTime.UtcNow - startedAt;
                if (response == null || response.ResponseStatus != ResponseStatus.Completed || (int)response.StatusCode >= 400)
                {
                    return AiConnectionTestResult.Fail("AI 配置测试失败：" + BuildResponseSummary(response, elapsed));
                }

                return AiConnectionTestResult.Ok("AI 配置测试成功：" + BuildResponseSummary(response, elapsed));
            }
            catch (Exception ex)
            {
                return AiConnectionTestResult.Fail("AI 配置测试异常：" + ex.GetType().Name + " " + ex.Message);
            }
        }
    }
}
