namespace AiCleanVolume.Core.Domain.Ai
{
    /// <summary>一条对话消息，role 取 system / user / assistant。</summary>
    public sealed class AiChatMessage
    {
        public const string SystemRole = "system";
        public const string UserRole = "user";
        public const string AssistantRole = "assistant";

        public AiChatMessage()
        {
        }

        public AiChatMessage(string role, string content)
        {
            Role = role;
            Content = content;
        }

        public string Role { get; set; }

        public string Content { get; set; }
    }
}
