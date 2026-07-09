using System;
using System.Windows.Forms;
using AiCleanVolume.Desktop.Composition;
using AiCleanVolume.Desktop.Presentation.WebShell;

namespace AiCleanVolume.Desktop
{
    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new WebShellWindow(DesktopCompositionRoot.CreateDependencies()));
        }
    }
}
