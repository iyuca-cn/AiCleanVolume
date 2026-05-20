using System;
using System.Collections.Generic;

namespace AiCleanVolume.Core.Models
{
    public sealed class AiProfile
    {
        public AiProfile()
        {
            SavedAt = DateTime.Now;
            ModelCookieMappings = new List<AiModelCookieMapping>();
        }

        public string Name { get; set; }
        public DateTime SavedAt { get; set; }
        public string AccessMode { get; set; }
        public string Endpoint { get; set; }
        public string ApiKey { get; set; }
        public string Model { get; set; }
        public int MaxSuggestions { get; set; }
        public string SystemPrompt { get; set; }
        public IList<AiModelCookieMapping> ModelCookieMappings { get; set; }

        public AiProfile Clone()
        {
            AiProfile clone = new AiProfile
            {
                Name = Name,
                SavedAt = SavedAt,
                AccessMode = AccessMode,
                Endpoint = Endpoint,
                ApiKey = ApiKey,
                Model = Model,
                MaxSuggestions = MaxSuggestions,
                SystemPrompt = SystemPrompt,
                ModelCookieMappings = new List<AiModelCookieMapping>()
            };

            IList<AiModelCookieMapping> mappings = AiSettings.NormalizeModelCookieMappings(ModelCookieMappings);
            for (int i = 0; i < mappings.Count; i++)
            {
                clone.ModelCookieMappings.Add(new AiModelCookieMapping
                {
                    Model = mappings[i].Model,
                    Cookie = mappings[i].Cookie
                });
            }

            return clone;
        }

        public string BuildFingerprint()
        {
            List<string> parts = new List<string>();
            parts.Add(AiSettings.NormalizeAccessMode(AccessMode));
            parts.Add(NormalizeValue(Endpoint));
            parts.Add(NormalizeValue(ApiKey));
            parts.Add(NormalizeValue(Model));
            parts.Add(MaxSuggestions.ToString());
            parts.Add(NormalizeValue(SystemPrompt));

            IList<AiModelCookieMapping> mappings = AiSettings.NormalizeModelCookieMappings(ModelCookieMappings);
            for (int i = 0; i < mappings.Count; i++)
            {
                parts.Add(NormalizeValue(mappings[i].Model) + "=" + NormalizeValue(mappings[i].Cookie));
            }

            return string.Join("\n", parts.ToArray());
        }

        private static string NormalizeValue(string value)
        {
            return (value ?? string.Empty).Trim();
        }
    }
}
