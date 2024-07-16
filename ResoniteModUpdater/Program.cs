using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Spectre.Console;
using Spectre.Console.Cli;
using Spectre.Console.Cli.Internal.Configuration;
using Spectre.Console.Rendering;
using Velopack;
using Velopack.Sources;

namespace ResoniteModUpdater
{
    public static class Program
    {
        public static async Task<int> Main(string[] args)
        {
            VelopackApp.Build().Run();
            var app = new CommandApp<DefaultCommand>();
            app.Configure(config =>
            {
                config.SetApplicationName(Strings.Application.AppName);
                config.AddExample(Strings.Examples.Empty);
                config.AddExample(Strings.Examples.Update);
                config.AddExample(Strings.Commands.Search, Strings.Examples.SearchExample);

                config.AddCommand<UpdateCommand>(Strings.Commands.Update)
                    .WithAlias(Strings.Commands.UpdateAlias1)
                    .WithAlias(Strings.Commands.UpdateAlias2)
                    .WithDescription(Strings.Descriptions.UpdateMods)
                    .WithExample(Strings.Examples.Update)
                    .WithExample(string.Format(Strings.Examples.UpdateWithPath, Utils.GetDefaultPath()))
                    .WithExample(string.Format(Strings.Examples.UpdateWithPathAndToken, Utils.GetDefaultPath()));

                config.AddCommand<SearchCommand>(Strings.Commands.Search)
                    .WithExample(Strings.Commands.Search, Strings.Examples.SearchExample)
                    .WithAlias(Strings.Commands.SearchAlias)
                    .WithDescription(Strings.Descriptions.UpdateMods);
            });

            return await app.RunAsync(args);
        }

        private static async Task UpdateMyApp()
        {
            var mgr = new UpdateManager(new SimpleFileSource(new DirectoryInfo(@"C:\Users\haz\dev\ResoniteModUpdater\Releases")));

            // check for new version
            var newVersion = await mgr.CheckForUpdatesAsync();
            if (newVersion == null)
                return; // no update available

            // download new version
            await mgr.DownloadUpdatesAsync(newVersion);

            // install new version and restart app
            mgr.ApplyUpdatesAndRestart(newVersion);
        }

        public static string AskPath()
        {
            return AnsiConsole.Prompt(
                new TextPrompt<string>(Strings.Prompts.EnterModsFolderPath)
                    .DefaultValue(Utils.GetDefaultPath())
                    .DefaultValueStyle("gray")
                    .PromptStyle("green")
                    .AllowEmpty()
                    .ValidationErrorMessage($"[red]{Strings.Errors.NotValidDirectory}[/]")
                    .Validate(path =>
                    {
                        return path switch
                        {
                            _ when string.IsNullOrWhiteSpace(path) => ValidationResult.Success(),
                            _ when !Directory.Exists(path) => ValidationResult.Error("test"),
                            _ => ValidationResult.Success(),
                        };
                    }));
        }

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

                (var settingsConfig, var loadedSettings, var overriddenSettings) = await LoadAndOverrideSettingsAsync(settings);

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

                (var settingsConfig, var loadedSettings, var overriddenSettings) = await LoadAndOverrideSettingsAsync(settings);

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
                if (result.Entry.Versions[result.LatestVersion].ReleaseUrl != null)
                {
                    return result.Entry.Versions[result.LatestVersion].ReleaseUrl;
                }
                else if (result.Entry.Versions[result.LatestVersion].Artifacts.Last().Url.Host.EndsWith("github.com"))
                {
                    Uri uri = result.Entry.Versions[result.LatestVersion].Artifacts.Last().Url;
                    string path = uri.AbsolutePath;
                    string[] pathParts = path.Split('/');
                    string repoOwner = pathParts[1];
                    string repoName = pathParts[2];
                    string tagOrVersion = pathParts[5];

                    return new Uri($"https://github.com/{repoOwner}/{repoName}/releases/tag/{tagOrVersion}");
                }
                else if (result.Entry.SourceLocation != null)
                {
                    return result.Entry.SourceLocation;
                }

                return null;
            }
        }
        public static async Task<(Utils.SettingsConfig, bool, bool)> LoadAndOverrideSettingsAsync<TSettings>(TSettings settings)
        {
            var settingsConfig = new Utils.SettingsConfig();

            var loadedSettings = Utils.LoadSettings();
            if (loadedSettings != null)
            {
                await AnsiConsole.Status().StartAsync(Strings.Status.LoadingSettings, async _ =>
                {
                    settingsConfig = loadedSettings;
                    await Task.Delay(1000);
                });
            }

            (settingsConfig, var overriddenSettings) = Utils.OverrideSettings(settingsConfig, settings);
            if (overriddenSettings.Any() && loadedSettings != null)
            {
                Utils.NotifyOverriddenSettings(overriddenSettings);
            }

            await AnsiConsole.Status()
                .StartAsync(Strings.Status.Starting, async ctx =>
                {
                    await Task.Delay(1000);
                    ctx.Status(Strings.Status.CheckingArguments);

                    List<string> propertiesList = new List<string>();
                    foreach (PropertyInfo property in typeof(Utils.SettingsConfig).GetProperties())
                    {
                        if (property.GetValue(settingsConfig) != null)
                        {

                            string propertyName = property.Name;

                            if (typeof(TSettings) == typeof(SearchCommand.Settings))
                            {
                                if (propertyName != "Manifest") continue;
                            }

                            if (propertyName == "DryMode" && property.GetValue(settingsConfig) is bool && (bool)property.GetValue(settingsConfig)! == false) continue;

                            var propertyValue = property.GetValue(settingsConfig);
                            if (propertyName == "Token") propertyValue = "********";

                            string message = $"[yellow]+[/] {propertyName} [gray]{propertyValue}[/]";
                            propertiesList.Add(message);
                        }
                    }

                    if (propertiesList.Any())
                    {
                        AnsiConsole.Write(new Padder(new Markup($"[yellow]{Strings.Messages.Arguments}[/]")).Padding(0, 0));

                        foreach (string property in propertiesList)
                        {
                            AnsiConsole.Write(new Padder(new Markup(property)).Padding(1, 0));
                        }
                    }
                });

            if (typeof(TSettings) == typeof(UpdateCommand.Settings))
            {
                settingsConfig.ModsFolder ??= AskPath();
            }

            return (settingsConfig, loadedSettings == null ? true : false, overriddenSettings.Any() ? true : false);
        }
    }
}