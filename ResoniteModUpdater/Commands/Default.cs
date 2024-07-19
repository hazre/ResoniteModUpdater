using ResoniteModUpdater.Commands.Search;
using ResoniteModUpdater.Commands.Update;
using Spectre.Console;
using Spectre.Console.Cli;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Velopack;

namespace ResoniteModUpdater.Commands.Default
{
  public class DefaultCommand : AsyncCommand<DefaultCommand.Settings>
  {
    public class Settings : CommandSettings
    {
      [Description(Strings.Descriptions.Version)]
      [CommandOption("-v|--version")]
      public bool Version { get; set; }
    }

    public override async Task<int> ExecuteAsync([NotNull] CommandContext context, [NotNull] Settings settings)
    {
      if (!AnsiConsole.Profile.Capabilities.Interactive)
      {
        AnsiConsole.MarkupLine($"[red]{Strings.Errors.NotInteractiveConsole}[/]");
        return 1;
      }

      if (settings.Version)
      {
        AnsiConsole.MarkupLine($"[yellow]{Strings.Application.AppName}[/] [b]v{GetVersion()}[/] ({VelopackRuntimeInfo.GetOsShortName(VelopackRuntimeInfo.SystemOs)}-{VelopackRuntimeInfo.SystemArch})");
        return 0;
      }


      while (true)
      {
        AnsiConsole.Clear();
        DisplayHeaderCommands();
        var choice = ShowMainMenu();

        try
        {
          switch (choice)
          {
            case Strings.MenuOptions.UpdateMods:
              AnsiConsole.Clear();
              DisplayHeaderUpdateOptions();
              var updateSettings = PromptForUpdateSettings();
              AnsiConsole.WriteLine();
              await new UpdateCommand().ExecuteAsync(context, updateSettings);
              break;
            case Strings.MenuOptions.SearchModManifest:
              AnsiConsole.Clear();
              var searchSettings = PromptForSearchSettings();
              AnsiConsole.WriteLine();
              await new SearchCommand().ExecuteAsync(context, searchSettings);
              break;
            case Strings.MenuOptions.ExitApplication:
              return 0;
          }
        }
        catch (Exception ex)
        {
          AnsiConsole.MarkupLine($"[red]{string.Format(Strings.Errors.Exception, ex.Message)}[/]");
        }

        if (!AnsiConsole.Confirm(Strings.Prompts.ReturnToMainMenu))
        {
          return 0;
        }
      }
    }

    private void DisplayHeaderCommands()
    {
      var version = Assembly.GetEntryAssembly()?.GetName().Version;
      var versionString = version != null ? $"{version.Major}.{version.Minor}.{version.Build}" : "";

      var table = new Table().HideHeaders().NoBorder();
      table.Title($"[yellow]{Strings.Application.AppName}[/] [b]v{versionString}[/]");
      table.AddColumn("col1", c => c.NoWrap().RightAligned().PadRight(3));
      table.AddColumn("col2", c => c.PadRight(0));
      table.AddEmptyRow();

      table.AddEmptyRow();
      table.AddRow(
          new Markup($"[yellow]{Strings.MenuOptions.UpdateMods}[/]"),
          new Markup(Strings.Descriptions.UpdateMods));

      table.AddRow(
          new Markup($"[yellow]{Strings.MenuOptions.UpdateLibraries}[/]"),
          new Markup(Strings.Descriptions.UpdateLibraries));

      table.AddRow(
          new Markup($"[yellow]{Strings.MenuOptions.SearchModManifest}[/]"),
          new Markup(Strings.Descriptions.SearchModManifest));



      AnsiConsole.WriteLine();
      AnsiConsole.Write(table);
      AnsiConsole.WriteLine();
    }
    private void DisplayHeaderUpdateOptions()
    {
      var table = new Table().HideHeaders().NoBorder();
      table.Title($"[yellow]{Strings.Application.AppName}[/] [b]v{GetVersion()}[/]");
      table.AddColumn("col1", c => c.NoWrap().RightAligned().Width(10).PadRight(3));
      table.AddColumn("col2", c => c.PadRight(0));
      table.AddEmptyRow();

      table.AddEmptyRow();
      table.AddRow(
          new Markup("[yellow]Options[/]"),
          new Grid().Expand().AddColumns(2)
          .AddRow(
              "ModsFolder",
              Strings.Descriptions.ModsFolder)
          .AddRow(
              "DryMode",
              Strings.Descriptions.DryMode)
          .AddRow(
              "Token",
              Strings.Descriptions.Token));


      AnsiConsole.WriteLine();
      AnsiConsole.Write(table);
      AnsiConsole.WriteLine();
    }

    private static string GetVersion()
    {
      var version = Assembly.GetEntryAssembly()?.GetName().Version;
      var versionString = version != null ? $"{version.Major}.{version.Minor}.{version.Build}" : "NaN";
      return versionString;
    }

    private string ShowMainMenu()
    {
      return AnsiConsole.Prompt(
          new SelectionPrompt<string>()
              .Title(Strings.Prompts.InteractivePrompt)
              .HighlightStyle(new Style(foreground: Color.Orange1, decoration: Decoration.Bold))
              .EnableSearch()
              .AddChoices(new[] {
                            Strings.MenuOptions.UpdateMods,
                            Strings.MenuOptions.UpdateLibraries,
                            Strings.MenuOptions.SearchModManifest,
                            Strings.MenuOptions.ExitApplication
              }));
    }

    private UpdateCommand.Settings PromptForUpdateSettings()
    {
      var settings = new UpdateCommand.Settings
      {
        ModsFolder = AnsiConsole.Ask<string>(Strings.Prompts.EnterModsFolderPath, Utils.GetDefaultPath()),
        DryMode = AnsiConsole.Confirm(Strings.Prompts.EnableDryRunMode, false),
        Token = AnsiConsole.Prompt(
          new TextPrompt<string>(Strings.Prompts.EnterGitHubToken)
              .AllowEmpty()
      ),
        ReadKeyExit = false
      };

      return settings;
    }

    private SearchCommand.Settings PromptForSearchSettings()
    {
      var settings = new SearchCommand.Settings
      {
        Query = AnsiConsole.Ask<string>(Strings.Prompts.EnterSearchQuery),
        Manifest = AnsiConsole.Prompt(new TextPrompt<string>(Strings.Prompts.EnterAlternativeManifest).AllowEmpty()),
        ReadKeyExit = false
      };


      return settings;
    }
  }
}