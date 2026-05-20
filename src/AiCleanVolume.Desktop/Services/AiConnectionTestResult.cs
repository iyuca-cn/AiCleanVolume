namespace AiCleanVolume.Desktop.Services
{
    public sealed class AiConnectionTestResult
    {
        public bool Success { get; private set; }
        public string Message { get; private set; }

        private AiConnectionTestResult(bool success, string message)
        {
            Success = success;
            Message = message;
        }

        public static AiConnectionTestResult Ok(string message)
        {
            return new AiConnectionTestResult(true, message);
        }

        public static AiConnectionTestResult Fail(string message)
        {
            return new AiConnectionTestResult(false, message);
        }
    }
}
