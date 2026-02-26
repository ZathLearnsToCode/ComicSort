using System.Text.Json;
using System.Text.Json.Serialization;

namespace ComicSort.Engine.Settings;

public sealed class AppSettings
{
    private static readonly string AppDataRoot = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "ComcSort2");

    public int Version { get; set; } = 1;

    public DateTimeOffset LastUpdatedUtc { get; set; } = DateTimeOffset.UtcNow;

    public List<LibraryFolderSetting> LibraryFolders { get; set; } = [];

    public List<ComicListItem> ComicLists { get; set; } = [];

    public string DatabasePath { get; set; } = Path.Combine(AppDataRoot, "data", "library.db");

    public string ThumbnailCacheDirectory { get; set; } = Path.Combine(AppDataRoot, "cache", "thumbnails");

    public string DefaultTheme { get; set; } = "Soft Neutral Pro";

    public string CurrentTheme { get; set; } = "Soft Neutral Pro";

    [JsonPropertyName("themeName")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? LegacyThemeName { get; set; }

    public int ScanBatchSize { get; set; } = 500;

    public int ScanWorkerCount { get; set; } = Math.Min(4, Environment.ProcessorCount);

    public int ScanStatusUpdateIntervalMs { get; set; } = 100;

    public bool ConfirmCbzConversion { get; set; } = true;

    public bool SendOriginalToRecycleBinOnCbzConversion { get; set; } = false;
}

public sealed class ComicListItem
{
    public Guid Id { get; set; }

    public string Type { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public bool Temporary { get; set; }

    public int BookCount { get; set; }

    public int NewBookCount { get; set; }

    public DateTimeOffset? NewBookCountDateUtc { get; set; }

    public string MatchMode { get; set; } = "All";

    public string? QueryText { get; set; }

    public SmartListExpressionNode? Expression { get; set; }

    public List<ComicListItem> Items { get; set; } = [];

    public List<ComicBookMatcher> Matchers { get; set; } = [];
}

public sealed class ComicBookMatcher
{
    public string MatcherType { get; set; } = string.Empty;

    public bool Not { get; set; }

    public int? MatchOperator { get; set; }

    public int? MatchValue { get; set; }

    public int? MatchValue2 { get; set; }

    public string? MatchValueText { get; set; }

    public string? MatchValueText2 { get; set; }
}

public sealed class SmartListExpressionNode
{
    public string NodeType { get; set; } = "Group";

    public bool Not { get; set; }

    public string MatchMode { get; set; } = "All";

    public string Field { get; set; } = "All";

    public string Operator { get; set; } = "contains";

    public string? Value1 { get; set; }

    public string? Value2 { get; set; }

    public string ValueKind { get; set; } = "Unknown";

    public List<SmartListExpressionNode> Children { get; set; } = [];
}

[JsonConverter(typeof(LibraryFolderSettingJsonConverter))]
public sealed class LibraryFolderSetting
{
    public string Folder { get; set; } = string.Empty;

    public bool Watched { get; set; }
}

public sealed class LibraryFolderSettingJsonConverter : JsonConverter<LibraryFolderSetting>
{
    public override LibraryFolderSetting? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String)
        {
            return new LibraryFolderSetting
            {
                Folder = reader.GetString() ?? string.Empty,
                Watched = false
            };
        }

        if (reader.TokenType != JsonTokenType.StartObject)
        {
            throw new JsonException("Expected string or object for library folder.");
        }

        string folder = string.Empty;
        bool watched = false;

        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndObject)
            {
                break;
            }

            if (reader.TokenType != JsonTokenType.PropertyName)
            {
                continue;
            }

            var propertyName = reader.GetString();
            reader.Read();

            if (string.Equals(propertyName, "folder", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(propertyName, "folders", StringComparison.OrdinalIgnoreCase))
            {
                folder = reader.TokenType == JsonTokenType.String
                    ? reader.GetString() ?? string.Empty
                    : string.Empty;
                continue;
            }

            if (string.Equals(propertyName, "watched", StringComparison.OrdinalIgnoreCase))
            {
                watched = reader.TokenType switch
                {
                    JsonTokenType.True => true,
                    JsonTokenType.False => false,
                    JsonTokenType.String when bool.TryParse(reader.GetString(), out var parsed) => parsed,
                    _ => false
                };
                continue;
            }

            reader.Skip();
        }

        return new LibraryFolderSetting
        {
            Folder = folder,
            Watched = watched
        };
    }

    public override void Write(Utf8JsonWriter writer, LibraryFolderSetting value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        writer.WriteString("folder", value.Folder);
        writer.WriteBoolean("watched", value.Watched);
        writer.WriteEndObject();
    }
}
