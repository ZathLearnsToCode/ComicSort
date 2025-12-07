using ComicSort.Core.Models;
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;

namespace ComicSort.Core.Services
{
    public class SettingsServices : ISettingsServices
    {
        private readonly string _settingsFile;
        public ComicSortSettings Settings { get; private set; }

        public SettingsServices()
        {
            var appData =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                         "ComicSort");

            if (!Directory.Exists(appData))
                Directory.CreateDirectory(appData);

            _settingsFile = Path.Combine(appData, "settings.json");

            Settings = LoadOrCreateSettings();
        }

        private ComicSortSettings LoadOrCreateSettings()
        {
            if (!File.Exists(_settingsFile))
            {
                // CREATE FIRST-RUN DEFAULT SETTINGS
                var defaults = new ComicSortSettings();

                var json = JsonSerializer.Serialize(defaults, new JsonSerializerOptions
                {
                    WriteIndented = true
                });

                File.WriteAllText(_settingsFile, json);

                return defaults;
            }

            // LOAD EXISTING SETTINGS
            var file = File.ReadAllText(_settingsFile);
            return JsonSerializer.Deserialize<ComicSortSettings>(file) ?? new ComicSortSettings();
        }

        public void Save()
        {
            var json = JsonSerializer.Serialize(Settings, new JsonSerializerOptions
            {
                WriteIndented = true
            });

            File.WriteAllText(_settingsFile, json);
        }

        public bool TryAddComicFolder(string folderPath, out string? error)
        {
            error = null;

            if (Settings.ComicFolders.Contains(folderPath, StringComparer.OrdinalIgnoreCase))
            {
                error = "This folder is already added.";
                return false;
            }

            Settings.ComicFolders.Add(folderPath);
            Save();
            return true;
        }
    }
}
