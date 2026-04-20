using System.Collections.Generic;
using AiCleanVolume.Core.Models;

namespace AiCleanVolume.Core.Services
{
    public sealed class HeuristicCleanupAdvisor : IAiCleanupAdvisor
    {
        public IList<CleanupSuggestion> Analyze(StorageItem root, IList<CleanupCandidate> candidates, ApplicationSettings settings)
        {
            List<CleanupSuggestion> suggestions = new List<CleanupSuggestion>();
            if (candidates == null) return suggestions;

            int max = settings != null && settings.Ai != null && settings.Ai.MaxSuggestions > 0 ? settings.Ai.MaxSuggestions : 30;
            for (int i = 0; i < candidates.Count && suggestions.Count < max; i++)
            {
                CleanupCandidate candidate = candidates[i];
                suggestions.Add(new CleanupSuggestion
                {
                    Path = candidate.Path,
                    Name = candidate.Name,
                    Bytes = candidate.Bytes,
                    IsDirectory = candidate.IsDirectory,
                    Risk = candidate.Risk,
                    Score = candidate.Risk == CleanupRisk.Low ? 0.85 : 0.6,
                    Reason = candidate.ReasonHint,
                    Source = "本地启发式规则",
                    Selected = true
                });
            }

            return suggestions;
        }
    }
}
