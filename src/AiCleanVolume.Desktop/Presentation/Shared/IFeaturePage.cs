using System.Windows.Forms;

namespace AiCleanVolume.Desktop.Presentation.Shared
{
    public interface IFeaturePage
    {
        string PageId { get; }
        Control View { get; }
        void OnActivated();
    }
}
