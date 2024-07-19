using Spectre.Console;
using Spectre.Console.Cli;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;

namespace ResoniteModUpdater.Commands.Update
{
  internal sealed class UpdateCommand : AsyncCommand<UpdateCommand.Settings>
  {
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

      var urls = Utils.GetFiles(settingsConfig.ModsFolder!);
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
      await UpdateAdditionalLibraries(settingsConfig);

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
              int status;
              string? releaseUrl = null;
              if (urlValue == null)
              {
                status = 2;
              }
              else if (urlValue == "_")
              {
                status = 4;
              }
              else
              {
                (status, releaseUrl) = await UpdateMod(dllFile, urlValue, settingsConfig);
              }
              AddStatusToTable(table, status, dllFile, releaseUrl, settingsConfig.DryMode);
              ctx.Refresh();
            }
          });
    }

    private async Task<(int, string?)> UpdateMod(string dllFile, string urlValue, Utils.SettingsConfig settingsConfig)
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

    private void AddStatusToTable(Table table, int status, string dllFile, string? releaseUrl, bool dryMode)
    {
      var (symbol, statusText) = status switch
      {
        0 => (Strings.ModStatus.Symbols.Update, dryMode ? $"[green]{Strings.ModStatus.UpdateAvailable}[/]" : $"[green]{Strings.ModStatus.Updated}[/]"),
        1 => (Strings.ModStatus.Symbols.NoChange, $"[dim]{Strings.ModStatus.UpToDate}[/]"),
        2 => (Strings.ModStatus.Symbols.Issue, $"[red]{Strings.ModStatus.NoLinkFound}[/]"),
        3 => (Strings.ModStatus.Symbols.Issue, $"[red]{Strings.ModStatus.InvalidLink}[/]"),
        4 => (Strings.ModStatus.Symbols.NoChange, $"[dim]{Strings.ModStatus.Ignored}[/]"),
        _ => (Strings.ModStatus.Symbols.Issue, $"[red]{Strings.ModStatus.Error}[/]")
      };

      table.AddRow($"[orange1]{symbol}[/]", Path.GetFileName(dllFile), statusText, $"[link={releaseUrl}]{releaseUrl}[/]");
    }

    private async Task UpdateAdditionalLibraries(Utils.SettingsConfig settingsConfig)
    {
      await UpdateLibrary("ResoniteModLoader.dll", "Libraries", settingsConfig);
      await UpdateLibrary("0Harmony.dll", "rml_libs", settingsConfig);
    }

    private async Task UpdateLibrary(string dllName, string subFolder, Utils.SettingsConfig settingsConfig)
    {
      string? libraryPath = Utils.GetLibraryPath(settingsConfig.ModsFolder!, subFolder, dllName);
      if (string.IsNullOrEmpty(libraryPath))
      {
        AnsiConsole.MarkupLine($"[red]{string.Format(Strings.Errors.DLLNotFoundSkipping, dllName)}[/]");
        return;
      }

      var resoniteModLoaderSource = settingsConfig.ResoniteModLoaderSource ?? Utils.ResoniteModLoaderSource;

      var (status, _) = await Utils.DownloadFromRSS(libraryPath, resoniteModLoaderSource, true);
      if (status == 0 && AnsiConsole.Confirm($"There is an update available for {dllName}. Would you like to update it?"))
      {
        await Utils.DownloadFromRSS(libraryPath, resoniteModLoaderSource, false);
      }
    }
  }
}