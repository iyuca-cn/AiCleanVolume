using System;
using System.IO;
using System.Text;
using AiCleanVolume.Core.Domain.Settings;
using AiCleanVolume.Core.Kernel.Ports;
using Newtonsoft.Json;

namespace AiCleanVolume.Desktop.Infrastructure.Settings
{
    public sealed class JsonSettingsStore : ISettingsStore
    {
        private static readonly Encoding Utf8 = new UTF8Encoding(false);
        private readonly string path;

        public JsonSettingsStore()
        {
            path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "appsettings.json");
        }

        public ApplicationSettings Load()
        {
            ApplicationSettings settings = null;
            if (File.Exists(path))
            {
                string json = File.ReadAllText(path, Utf8);
                settings = JsonConvert.DeserializeObject<ApplicationSettings>(json);
            }

            if (settings == null) settings = new ApplicationSettings();
            settings.EnsureDefaults();
            Save(settings);
            return settings;
        }

        public void Save(ApplicationSettings settings)
        {
            if (settings == null) settings = new ApplicationSettings();
            settings.EnsureDefaults();
            string json = JsonConvert.SerializeObject(settings, Formatting.Indented);
            File.WriteAllText(path, json, Utf8);
        }
    }
}
