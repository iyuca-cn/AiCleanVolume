using System;
using System.Collections.Generic;
using System.Text;
using AiCleanVolume.Core.Domain.Cleanup;
using AiCleanVolume.Core.Domain.Sandbox;
using AiCleanVolume.Core.Domain.Settings;
using AiCleanVolume.Core.Domain.Storage;
using AiCleanVolume.Core.Application.CleanupPlanning;
using AiCleanVolume.Core.Application.Deletion;
using AiCleanVolume.Core.Application.Scanning;
using AiCleanVolume.Core.Kernel.Ports;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;


namespace AiCleanVolume.Desktop.Infrastructure.Ai
{
    public sealed partial class OpenAiCompatibleAdvisor : IAiCleanupAdvisor
    {
        private sealed class ChatCompletionResponse
        {
            public List<ChatChoice> choices { get; set; }
        }

        private sealed class ChatChoice
        {
            public ChatMessage message { get; set; }
        }

        private sealed class ChatMessage
        {
            public string content { get; set; }
        }

        private sealed class AiSuggestionEnvelope
        {
            public List<AiSuggestionDto> candidates { get; set; }
        }

        private sealed class AiSuggestionDto
        {
            public string path { get; set; }
            public string risk { get; set; }
            public double score { get; set; }
            public string reason { get; set; }
        }

        private sealed class AiHttpResponse
        {
            public int StatusCode { get; set; }
            public string StatusDescription { get; set; }
            public string ResponseStatus { get; set; }
            public string Content { get; set; }
            public string ErrorMessage { get; set; }
            public bool IsCompleted { get; set; }
        }
    }
}
