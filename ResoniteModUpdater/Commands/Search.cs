using Spectre.Console;
using Spectre.Console.Cli;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;


namespace ResoniteModUpdater.Commands.Search
{
  public class SearchCommand : AsyncCommand<SearchCommand.Settings>
  {
    public class Settings : CommandSettings
    {
      [Description(Strings.Descriptions.Query)]
      [CommandArgument(0, "[QUERY]")]
      public required string Query { get; set; }

      [Description(Strings.Descriptions.Manifest)]
      [CommandOption("-m|--manifest")]
      public string? Manifest { get; set; }

      [CommandOption("--readkeyexit", IsHidden = true)]
      [DefaultValue(true)]
      public bool ReadKeyExit { get; set; }
    }

    public override async Task<int> ExecuteAsync([NotNull] CommandContext context, [NotNull] Settings settings)
    {
      if (!AnsiConsole.Profile.Capabilities.Interactive)
      {
        AnsiConsole.MarkupLine($"[red]{Strings.Errors.NotInteractiveConsole}[/]");
        return 1;
      }

      (var settingsConfig, var loadedSettings, var overriddenSettings) = await Utils.LoadAndOverrideSettingsAsync(settings);

      if (string.IsNullOrEmpty(settings.Query))
      {
        AnsiConsole.MarkupLine($"[red]{Strings.Errors.NoSearchTerm}[/]");
        return 0;
      }

      var results = await SearchAndDisplayResults(settings.Query, settingsConfig.Manifest ?? Utils.Manifest);

      if (results.Count == 0)
      {
        AnsiConsole.MarkupLine($"[red]{string.Format(Strings.Errors.NoResultsQuery, settings.Query)}.[/]");
        if (settings.ReadKeyExit)
        {
          AnsiConsole.MarkupLine($"[slateblue3]{Strings.Messages.PressKeyExit}[/]");
          Console.ReadKey();
        }
        return 0;
      }

      Utils.CheckAndSaveOverriddenSettings(settingsConfig, loadedSettings, overriddenSettings);

      AnsiConsole.MarkupLine($"[slateblue3]{Strings.Messages.FinishedSearchingMods}[/]");
      if (settings.ReadKeyExit)
      {
        AnsiConsole.MarkupLine($"[slateblue3]{Strings.Messages.PressKeyExit}[/]");
        Console.ReadKey();
      }

      return 0;
    }

    private async Task<List<SearchResult>> SearchAndDisplayResults(string query, string manifest)
    {
      var results = await AnsiConsole.Status()
          .StartAsync(string.Format(Strings.Status.Searching, query), _ => Utils.SearchManifest(query, manifest));

      if (results.Count > 0)
      {
        DisplayResultsTable(results);
      }

      return results;
    }

    private void DisplayResultsTable(List<SearchResult> results)
    {
      var table = new Table()
          .AddColumn(new TableColumn($"[cyan]{Strings.SearchTableHeaders.Name}[/]"))
          .AddColumn(new TableColumn($"[cyan]{Strings.SearchTableHeaders.Author}[/]"))
          .AddColumn(new TableColumn($"[cyan]{Strings.SearchTableHeaders.ID}[/]"))
          .AddColumn(new TableColumn($"[cyan]{Strings.SearchTableHeaders.Version}[/]"))
          .AddColumn(new TableColumn($"[cyan]{Strings.SearchTableHeaders.Description}[/]"))
          .ShowRowSeparators();

      foreach (var result in results)
      {
        Uri? releaseUrl = GetReleaseUrl(result);
        string versionDisplay = AnsiConsole.Profile.Capabilities.Links
            ? $"[link={releaseUrl}]{result.LatestVersion}[/]"
            : result.LatestVersion;

        table.AddRow(result.Entry.Name, result.AuthorName, result.ID, versionDisplay, result.Entry.Description);
      }

      AnsiConsole.Write(table);
    }

    private Uri? GetReleaseUrl(SearchResult result)
    {
      if (result.Entry.Versions.TryGetValue(result.LatestVersion, out var versionEntry))
      {
        if (versionEntry.ReleaseUrl != null)
        {
          return versionEntry.ReleaseUrl;
        }

        var lastArtifact = versionEntry.Artifacts?.LastOrDefault();
        if (lastArtifact?.Url != null && lastArtifact.Url.Host.EndsWith("github.com"))
        {
          Uri artifactUri = lastArtifact.Url;
          string[] pathParts = artifactUri.AbsolutePath.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
          // Expected structure: {owner}/{repo}/releases/download/{tag}/{filename}
          if (pathParts.Length >= 5 && pathParts[2].Equals("releases", StringComparison.OrdinalIgnoreCase) && pathParts[3].Equals("download", StringComparison.OrdinalIgnoreCase))
          {
            string repoOwner = pathParts[0];
            string repoName = pathParts[1];
            string tagOrVersion = pathParts[4];
            if (Uri.TryCreate($"https://github.com/{repoOwner}/{repoName}/releases/tag/{tagOrVersion}", UriKind.Absolute, out var releasePageUrl))
            {
              return releasePageUrl;
            }
          }
        }
      }

      if (result.Entry.SourceLocation != null)
      {
        return result.Entry.SourceLocation;
      }

      return null;
    }
  }
}