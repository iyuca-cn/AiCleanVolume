using System;
using System.Windows.Forms;
using AiCleanVolume.Desktop.Presentation.Shared;

namespace AiCleanVolume.Desktop.Presentation.Features.Scan
{
    public sealed class ScanPageController
    {
        public ScanPageController(
            IMainWindowShell shell,
            ScanPageView view,
            Action scanRequested,
            Action<object, AntdUI.ObjectNEventArgs> driveSelectChanged,
            Action<object, AntdUI.ObjectNEventArgs> sortSelectChanged,
            EventHandler pathTextChanged,
            Action<object, AntdUI.TableExpandEventArgs> storageExpandChanged,
            Action<object, AntdUI.TableClickEventArgs> storageCellClick,
            Action<object, AntdUI.TableClickEventArgs> storageCellDoubleClick,
            KeyEventHandler storageKeyDown)
        {
            if (shell == null) throw new ArgumentNullException("shell");
            if (view == null) throw new ArgumentNullException("view");

            if (view.ScanButton != null && scanRequested != null) view.ScanButton.Click += delegate(object sender, EventArgs e) { scanRequested(); };
            if (view.DriveSelect != null && driveSelectChanged != null) view.DriveSelect.SelectedValueChanged += delegate(object sender, AntdUI.ObjectNEventArgs e) { driveSelectChanged(sender, e); };
            if (view.SortSelect != null && sortSelectChanged != null) view.SortSelect.SelectedValueChanged += delegate(object sender, AntdUI.ObjectNEventArgs e) { sortSelectChanged(sender, e); };
            if (view.PathInput != null && pathTextChanged != null) view.PathInput.TextChanged += pathTextChanged;
            if (view.StorageTable != null)
            {
                if (storageExpandChanged != null) view.StorageTable.ExpandChanged += delegate(object sender, AntdUI.TableExpandEventArgs e) { storageExpandChanged(sender, e); };
                if (storageCellClick != null) view.StorageTable.CellClick += delegate(object sender, AntdUI.TableClickEventArgs e) { storageCellClick(sender, e); };
                if (storageCellDoubleClick != null) view.StorageTable.CellDoubleClick += delegate(object sender, AntdUI.TableClickEventArgs e) { storageCellDoubleClick(sender, e); };
                if (storageKeyDown != null) view.StorageTable.KeyDown += storageKeyDown;
            }
        }
    }
}
