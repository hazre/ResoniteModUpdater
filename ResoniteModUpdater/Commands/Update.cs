using Spectre.Console;
using Spectre.Console.Cli;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;

namespace ResoniteModUpdater.Commands.Update
{
  public enum ModUpdateResultStatus
  {
    Updated = 0,
    UpToDate = 1,
    NoLinkFound = 2,
    InvalidLink = 3,
    Ignored = 4,
    Error = -1
  }

  internal sealed class UpdateCommand : AsyncCommand<UpdateCommand.Settings>
  {
    private readonly List<(string ModName, string? UrlAttempted, Exception Error)> _updateErrors = new();

    public sealed class Settings : CommandSettings
    {
      [Description(Strings.Descriptions.ModsFolder)]
      [CommandArgument(0, "[ModsFolder]")]
      public string? ModsFolder { get; set; }

      [Description(Strings.Descriptions.Token)]
      [CommandOption("-t|--token")]
      public string? Token { get; set; }

      [Description(Strings.Descriptions.DryMode)]
      [CommandOption("-d|--dry")]
      [DefaultValue(false)]
      public bool DryMode { get; set; }

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

      Dictionary<string, string?> urls;
      try
      {
        if (string.IsNullOrEmpty(settingsConfig.ModsFolder))
        {
          AnsiConsole.MarkupLine($"[red]Mods folder path is not configured. Please set it via settings or command line.[/]");
          if (settings.ReadKeyExit)
          {
            AnsiConsole.MarkupLine($"[slateblue3]{Strings.Messages.PressKeyExit}[/]");
            Console.ReadKey();
          }
          return 1;
        }
        urls = Utils.GetFiles(settingsConfig.ModsFolder);
      }
      catch (Exception ex)
      {
        AnsiConsole.MarkupLine($"[red]Error accessing mods folder '{settingsConfig.ModsFolder}':[/]");
        AnsiConsole.WriteException(ex, ExceptionFormats.ShortenEverything);
        if (settings.ReadKeyExit)
        {
          AnsiConsole.MarkupLine($"[slateblue3]{Strings.Messages.PressKeyExit}[/]");
          Console.ReadKey();
        }
        return 1;
      }

      if (!urls.Any())
      {
        AnsiConsole.MarkupLine($"[red]{Strings.Errors.NoModsToUpdate}[/]");
        if (settings.ReadKeyExit)
        {
          AnsiConsole.MarkupLine($"[slateblue3]{Strings.Messages.PressKeyExit}[/]");
          Console.ReadKey();
        }
        return 1;
      }

      await DisplayModUpdateStatus(urls, settingsConfig);
      await Utils.UpdateAdditionalLibraries(settingsConfig);

      Utils.CheckAndSaveOverriddenSettings(settingsConfig, loadedSettings, overriddenSettings);


      AnsiConsole.MarkupLine($"[slateblue3]{(settingsConfig.DryMode ? Strings.Messages.FinishedCheckingMods : Strings.Messages.FinishedUpdatingMods)}.[/]");
      if (settings.ReadKeyExit)
      {
        AnsiConsole.MarkupLine($"[slateblue3]{Strings.Messages.PressKeyExit}[/]");
        Console.ReadKey();
      }
      return 0;
    }

    private async Task DisplayModUpdateStatus(Dictionary<string, string?> urls, Utils.SettingsConfig settingsConfig)
    {
      AnsiConsole.Write(new Padder(new Markup($"[orange1]Mods ({urls.Count})[/]")).Padding(0, 0));
      AnsiConsole.WriteLine();
      var table = new Table().Border(TableBorder.None).LeftAligned().Collapse().HideHeaders();
      table.AddColumns("", "", "", "");
      table.Columns[0].Width(1);

      await AnsiConsole.Live(new Padder(table).Padding(1, 0))
          .StartAsync(async ctx =>
          {
            foreach (var (dllFile, urlValue) in urls)
            {
              ModUpdateResultStatus status;
              string? releaseUrl = null;
              Exception? error = null;

              if (urlValue == null)
              {
                status = ModUpdateResultStatus.NoLinkFound;
              }
              else if (urlValue == "_")
              {
                status = ModUpdateResultStatus.Ignored;
              }
              else
              {
                (status, releaseUrl, error) = await UpdateMod(dllFile, urlValue, settingsConfig);
                if (error != null)
                {
                  _updateErrors.Add((Path.GetFileName(dllFile), releaseUrl, error));
                }
              }
              AddStatusToTable(table, status, dllFile, releaseUrl, settingsConfig.DryMode, error?.Message);
              ctx.Refresh();
            }
          });

      if (_updateErrors.Any())
      {
        AnsiConsole.MarkupLine("\n[bold red]Errors Occurred During Update:[/]");
        foreach (var (modName, urlAttempted, ex) in _updateErrors)
        {
          AnsiConsole.MarkupLine($"\n[bold yellow]Error updating {modName}:[/]");
          if (!string.IsNullOrEmpty(urlAttempted))
          {
            AnsiConsole.MarkupLine($"[grey]Attempted URL: {Markup.Escape(urlAttempted)}[/]");
          }
          AnsiConsole.WriteException(ex, ExceptionFormats.ShortenEverything);
        }
        _updateErrors.Clear();
      }
    }

    private async Task<(ModUpdateResultStatus Status, string? Url, Exception? Error)> UpdateMod(string dllFile, string urlValue, Utils.SettingsConfig settingsConfig)
    {
      if (!string.IsNullOrEmpty(settingsConfig.Token))
      {
        return await Utils.Download(dllFile, urlValue, settingsConfig.DryMode, settingsConfig.Token);
      }
      else
      {
        return await Utils.DownloadFromRSS(dllFile, urlValue, settingsConfig.DryMode);
      }
    }

    private void AddStatusToTable(Table table, ModUpdateResultStatus status, string dllFile, string? releaseUrl, bool dryMode, string? errorMessage = null)
    {
      var (symbol, statusText) = status switch
      {
        ModUpdateResultStatus.Updated => (Strings.ModStatus.Symbols.Update, dryMode ? $"[green]{Strings.ModStatus.UpdateAvailable}[/]" : $"[green]{Strings.ModStatus.Updated}[/]"),
        ModUpdateResultStatus.UpToDate => (Strings.ModStatus.Symbols.NoChange, $"[dim]{Strings.ModStatus.UpToDate}[/]"),
        ModUpdateResultStatus.NoLinkFound => (Strings.ModStatus.Symbols.Issue, $"[red]{Strings.ModStatus.NoLinkFound}[/]"),
        ModUpdateResultStatus.InvalidLink => (Strings.ModStatus.Symbols.Issue, $"[red]{Strings.ModStatus.InvalidLink}[/]"),
        ModUpdateResultStatus.Ignored => (Strings.ModStatus.Symbols.NoChange, $"[dim]{Strings.ModStatus.Ignored}[/]"),
        ModUpdateResultStatus.Error => (Strings.ModStatus.Symbols.Issue, $"[red]{Strings.ModStatus.Error}{(string.IsNullOrEmpty(errorMessage) ? "" : $": {Markup.Escape(errorMessage.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "")}")}[/]"),
        _ => (Strings.ModStatus.Symbols.Issue, $"[red]Unknown Status[/]")
      };

      string linkMarkup = string.IsNullOrEmpty(releaseUrl) ? string.Empty : $"[link={releaseUrl}]{Markup.Escape(releaseUrl)}[/]";
      table.AddRow($"[orange1]{symbol}[/]", Path.GetFileName(dllFile), statusText, linkMarkup);
    }
  }
}