using System;
using System.Collections.Generic;
using AiCleanVolume.Core.Domain.Ai;
using AiCleanVolume.Core.Domain.Settings;
using AiCleanVolume.Core.Kernel.Ports;
using Newtonsoft.Json;

namespace AiCleanVolume.Desktop.Infrastructure.Ai
{
    // 多轮对话补全：复用 Analyze 同一套鉴权与 HTTP 管道
    public sealed partial class OpenAiCompatibleAdvisor : IAiChatService
    {
        public AiChatResult Complete(IList<AiChatMessage> messages, ApplicationSettings settings)
        {
            if (messages == null || messages.Count == 0) return AiChatResult.Fail("对话内容为空。");
            if (settings == null || settings.Ai == null || !settings.Ai.Enabled ||
                string.IsNullOrWhiteSpace(settings.Ai.Endpoint) || string.IsNullOrWhiteSpace(settings.Ai.Model))
            {
                return AiChatResult.Fail("AI 未启用或配置不完整，请先在设置中填写接口地址与模型。");
            }

            try
            {
                string endpoint = NormalizeEndpoint(settings.Ai.Endpoint);
                string accessMode = AiSettings.NormalizeAccessMode(settings.Ai.AccessMode);
                string path = ResolveChatCompletionsPath(endpoint);
                Dictionary<string, string> headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                string authMessage;
                if (!AddAuthHeaders(headers, settings.Ai, accessMode, out authMessage))
                {
                    return AiChatResult.Fail(authMessage);
                }

                List<object> payloadMessages = new List<object>();
                for (int i = 0; i < messages.Count; i++)
                {
                    payloadMessages.Add(new { role = messages[i].Role, content = messages[i].Content });
                }

                string body = JsonConvert.SerializeObject(new
                {
                    model = settings.Ai.Model,
                    temperature = 0.3,
                    messages = payloadMessages
                });
                WriteLog("AI 对话请求：POST " + endpoint + path + " messages=" + messages.Count + " bodyChars=" + body.Length + "。");

                DateTime startedAt = DateTime.UtcNow;
                AiHttpResponse response = ExecuteJsonPost(endpoint, path, headers, body);
                TimeSpan elapsed = DateTime.UtcNow - startedAt;
                if (response == null || !response.IsCompleted || response.StatusCode >= 400)
                {
                    string summary = BuildResponseSummary(response, elapsed);
                    WriteLog("AI 对话请求失败：" + summary);
                    return AiChatResult.Fail("请求失败：" + summary);
                }

                ChatCompletionResponse chat = JsonConvert.DeserializeObject<ChatCompletionResponse>(response.Content);
                if (chat == null || chat.choices == null || chat.choices.Count == 0 ||
                    chat.choices[0].message == null || string.IsNullOrEmpty(chat.choices[0].message.content))
                {
                    WriteLog("AI 对话响应结构无效。contentPreview=" + Preview(response.Content, 300));
                    return AiChatResult.Fail("响应内容为空或结构无效。");
                }

                int totalTokens = chat.usage != null ? chat.usage.total_tokens : 0;
                WriteLog("AI 对话完成：耗时 " + elapsed.TotalSeconds.ToString("0.0") + "s tokens=" + totalTokens + "。");
                return AiChatResult.Ok(chat.choices[0].message.content, totalTokens);
            }
            catch (Exception ex)
            {
                WriteLog("AI 对话异常：" + ex.GetType().Name + " " + ex.Message);
                return AiChatResult.Fail(ex.Message);
            }
        }
    }
}
