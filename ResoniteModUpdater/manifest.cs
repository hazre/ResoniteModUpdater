using System.Text.Json.Serialization;


namespace ResoniteModUpdater
{

  public class ManifestData
  {
    public required Dictionary<string, ManifestObject> Objects { get; init; }

    [JsonPropertyName("schemaVersion")]
    public required string SchemaVersion { get; init; }
  }

  public class ManifestObject
  {
    [JsonPropertyName("author")]
    public required Dictionary<string, ManifestAuthor> Author { get; init; }

    public required Dictionary<string, ManifestEntry> Entries { get; init; }
  }

  public class ManifestAuthor
  {
    public required Uri Url { get; init; }
    public Uri? Icon { get; init; }
    public Uri? Support { get; init; }
  }

  public class ManifestEntry
  {
    public required string Name { get; init; }
    public required string Description { get; init; }
    public required string Category { get; init; }

    [JsonPropertyName("sourceLocation")]
    public Uri? SourceLocation { get; init; }

    public Uri? Website { get; init; }
    public List<string>? Tags { get; init; } = new();
    public List<string>? Flags { get; init; } = new();
    public List<string>? Platforms { get; init; } = new();

    [JsonPropertyName("additionalAuthors")]
    public Dictionary<string, ManifestAuthor>? AdditionalAuthors { get; init; } = new();

    public required Dictionary<string, ManifestEntryVersion> Versions { get; init; }
  }

  public class ManifestEntryVersion
  {
    public required List<ManifestEntryArtifact> Artifacts { get; init; }
    public Dictionary<string, ManifestEntryDependency>? Dependencies { get; init; } = new();
    public Dictionary<string, ManifestEntryDependency>? Conflicts { get; init; } = new();

    [JsonPropertyName("releaseUrl")]
    public Uri? ReleaseUrl { get; init; }
  }

  public class ManifestEntryArtifact
  {
    public required Uri Url { get; init; }
    public required string Sha256 { get; init; }
    public string? Filename { get; init; }

    [JsonPropertyName("installLocation")]
    public string? InstallLocation { get; init; }
  }

  public class ManifestEntryDependency
  {
    public required string Version { get; init; }
  }
  public class SearchResult
  {
    public required ManifestEntry Entry { get; init; }
    public required string ID { get; init; }
    public required string AuthorName { get; init; }
    public required string LatestVersion { get; init; }
    public required Uri AuthorUrl { get; init; }
  }
}
