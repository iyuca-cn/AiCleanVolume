namespace AiCleanVolume.Desktop.Presentation.Shared
{
    public interface IMainWindowShell
    {
        void SetBusy(bool busy, string description);
        void ShowInfo(string title, string message);
        void ShowWarning(string title, string message);
        void ShowError(string title, string message);
        void Log(string message);
        void LogBackground(string message);
    }
}
