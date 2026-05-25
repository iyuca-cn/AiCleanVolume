using System.Collections.Generic;
using AiCleanVolume.Desktop.ViewModels;

namespace AiCleanVolume.Desktop.Presentation.Features.Suggestions
{
    public sealed class SuggestionsPageState
    {
        public SuggestionsPageState()
        {
            Rows = new List<CleanupSuggestionRow>();
        }

        public List<CleanupSuggestionRow> Rows { get; set; }
    }
}
