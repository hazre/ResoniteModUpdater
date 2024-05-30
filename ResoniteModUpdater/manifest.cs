using System;
using System.Collections.Generic;
using System.Net;
using System.Text.Json.Serialization;


namespace ResoniteModUpdater
{

  public class ManifestData
  {
    public required Dictionary<string, ManifestObject> Objects { get; set; }

    [JsonPropertyName("schemaVersion")]
    public required string SchemaVersion { get; set; }
  }

  public class ManifestObject
  {
    [JsonPropertyName("author")]
    public required Dictionary<string, ManifestAuthor> Author { get; set; }

    public required Dictionary<string, ManifestEntry> Entries { get; set; }
  }

  public class ManifestAuthor
  {
    public required Uri Url { get; set; }
    public Uri? Icon { get; set; }
    public Uri? Support { get; set; }
  }

  public class ManifestEntry
  {
    public required string Name { get; set; }
    public required string Description { get; set; }
    public required string Category { get; set; }

    [JsonPropertyName("sourceLocation")]
    public Uri? SourceLocation { get; set; }

    public Uri? Website { get; set; }
    public List<string>? Tags { get; set; }
    public List<string>? Flags { get; set; }
    public List<string>? Platforms { get; set; }

    [JsonPropertyName("additionalAuthors")]
    public Dictionary<string, ManifestAuthor>? AdditionalAuthors { get; set; }

    public required Dictionary<string, ManifestEntryVersion> Versions { get; set; }
  }

  public class ManifestEntryVersion
  {
    public required List<ManifestEntryArtifact> Artifacts { get; set; }
    public Dictionary<string, ManifestEntryDependency>? Dependencies { get; set; }
    public Dictionary<string, ManifestEntryDependency>? Conflicts { get; set; }

    [JsonPropertyName("releaseUrl")]
    public Uri? ReleaseUrl { get; set; }
  }

  public class ManifestEntryArtifact
  {
    public required Uri Url { get; set; }
    public required string Sha256 { get; set; }
    public string? Filename { get; set; }

    [JsonPropertyName("installLocation")]
    public string? InstallLocation { get; set; }
  }

  public class ManifestEntryDependency
  {
    // Assuming VersionReq is a simple string representation of version requirements
    public required string Version { get; set; }
  }
  public class SearchResult
  {
    public required ManifestEntry Entry { get; set; }
    public required string ID { get; set; }
    public required string AuthorName { get; set; }
    public required string LatestVersion { get; set; }
  }
}
