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
    }
}
