using System;
using System.IO;
using System.Text;
using AiCleanVolume.Core.Models;
using Newtonsoft.Json;

namespace AiCleanVolume.Desktop.Services
{
    public sealed class SettingsStore
    {
        private static readonly Encoding Utf8 = new UTF8Encoding(false);
        private readonly string path;

        public SettingsStore()
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
