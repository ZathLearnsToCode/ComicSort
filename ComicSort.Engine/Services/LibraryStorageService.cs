using ComicSort.Engine.Models;
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;

namespace ComicSort.Engine.Services
{
    public sealed class LibraryStorageService
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true
        };

        public async Task SaveAsync(string filePath, IReadOnlyCollection<ComicBook> books)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);

            var json = JsonSerializer.Serialize(books, JsonOptions);
            await File.WriteAllTextAsync(filePath, json);
        }

        public async Task<List<ComicBook>> LoadAsync(string filePath)
        {
            if (!File.Exists(filePath))
                return new List<ComicBook>();

            var json = await File.ReadAllTextAsync(filePath);
            return JsonSerializer.Deserialize<List<ComicBook>>(json, JsonOptions) ?? new List<ComicBook>();
        }
    }
}
