namespace AiCleanVolume.Core.Domain.Sandbox
{
    public sealed class SandboxEvaluation
    {
        public SandboxAction Action { get; set; }
        public string Message { get; set; }
        public string MatchedRoot { get; set; }
    }
}
