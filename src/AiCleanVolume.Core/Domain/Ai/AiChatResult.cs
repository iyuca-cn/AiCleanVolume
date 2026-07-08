namespace AiCleanVolume.Core.Domain.Ai
{
    public sealed class AiChatResult
    {
        public bool Success { get; set; }

        /// <summary>助手回复正文；失败时为空。</summary>
        public string Content { get; set; }

        public string Error { get; set; }

        /// <summary>本次请求消耗的 token 总数，接口未返回时为 0。</summary>
        public int TotalTokens { get; set; }

        public static AiChatResult Ok(string content, int totalTokens)
        {
            AiChatResult result = new AiChatResult();
            result.Success = true;
            result.Content = content;
            result.TotalTokens = totalTokens;
            return result;
        }

        public static AiChatResult Fail(string error)
        {
            AiChatResult result = new AiChatResult();
            result.Success = false;
            result.Error = error;
            return result;
        }
    }
}
