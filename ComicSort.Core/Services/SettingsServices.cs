using ComicSort.Core.Models;
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;

namespace ComicSort.Core.Services
{
    public class SettingsServices : ISettingsServices
    {
        private readonly string _path;
        public ComicSortSettings Settings { get; private set; } = new();

        public SettingsServices()
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            _path = Path.Combine(appData, "ComicSort", "settings.json");

            Settings = Load();
        }

        public ComicSortSettings Load()
        {
            if (!File.Exists(_path))
                return new ComicSortSettings();

            var json = File.ReadAllText(_path);
            return JsonSerializer.Deserialize<ComicSortSettings>(json)!;
        }

        public void Save(ComicSortSettings settings)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);

            var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_path, json);
        }
    }
}
