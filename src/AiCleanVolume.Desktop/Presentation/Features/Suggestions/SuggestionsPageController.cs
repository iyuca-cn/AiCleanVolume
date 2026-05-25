using System;
using System.Windows.Forms;
using AiCleanVolume.Desktop.Presentation.Shared;

namespace AiCleanVolume.Desktop.Presentation.Features.Suggestions
{
    public sealed class SuggestionsPageController
    {
        public SuggestionsPageController(
            IMainWindowShell shell,
            SuggestionsPageView view,
            Action analyzeRegularRequested,
            Action analyzeSuperRequested,
            Action analyzeRequested,
            Action promptRequested,
            Action deleteRequested,
            Action selectAllRequested,
            Action clearAllRequested,
            Action invertRequested,
            Action<object, AntdUI.ObjectNEventArgs> driveSelectChanged,
            Action<object, AntdUI.BoolEventArgs> privilegedChanged,
            Action<object, AntdUI.TableClickEventArgs> tableDoubleClick,
            Action<object, AntdUI.TableButtonEventArgs> tableButtonClick)
        {
            if (shell == null) throw new ArgumentNullException("shell");
            if (view == null) throw new ArgumentNullException("view");

            if (view.RegularCleanButton != null && analyzeRegularRequested != null) view.RegularCleanButton.Click += delegate { analyzeRegularRequested(); };
            if (view.SuperCleanButton != null && analyzeSuperRequested != null) view.SuperCleanButton.Click += delegate { analyzeSuperRequested(); };
            if (view.AnalyzeButton != null && analyzeRequested != null) view.AnalyzeButton.Click += delegate { analyzeRequested(); };
            if (view.DeleteButton != null && deleteRequested != null) view.DeleteButton.Click += delegate { deleteRequested(); };
            if (view.DriveSelect != null && driveSelectChanged != null) view.DriveSelect.SelectedValueChanged += delegate(object sender, AntdUI.ObjectNEventArgs e) { driveSelectChanged(sender, e); };
            if (view.PrivilegedQuickCheckbox != null && privilegedChanged != null) view.PrivilegedQuickCheckbox.CheckedChanged += delegate(object sender, AntdUI.BoolEventArgs e) { privilegedChanged(sender, e); };
            if (view.SuggestionTable != null)
            {
                if (tableDoubleClick != null) view.SuggestionTable.CellDoubleClick += delegate(object sender, AntdUI.TableClickEventArgs e) { tableDoubleClick(sender, e); };
                if (tableButtonClick != null) view.SuggestionTable.CellButtonClick += delegate(object sender, AntdUI.TableButtonEventArgs e) { tableButtonClick(sender, e); };
            }

            if (view.InvertButton != null && invertRequested != null) view.InvertButton.Click += delegate { invertRequested(); };
            if (view.ClearAllButton != null && clearAllRequested != null) view.ClearAllButton.Click += delegate { clearAllRequested(); };
            if (view.SelectAllButton != null && selectAllRequested != null) view.SelectAllButton.Click += delegate { selectAllRequested(); };
            if (view.PromptButton != null && promptRequested != null) view.PromptButton.Click += delegate { promptRequested(); };
        }
    }
}
