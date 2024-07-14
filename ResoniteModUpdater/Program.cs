using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Spectre.Console;
using Spectre.Console.Cli;

namespace ResoniteModUpdater
{
    public static class Program
    {
        public static async Task<int> Main(string[] args)
        {
            var app = new CommandApp<DefaultCommand>();
            app.Configure(config =>
            {
                config.SetApplicationName("ResoniteModUpdater");
                config.SetApplicationVersion("2.2.0");
                config.AddExample(Utils.GetDefaultPath());
                config.AddExample(Utils.GetDefaultPath(), "-token xxxxxxxxxxxxxx");

                config.AddCommand<SearchCommand>("search")
                    .WithExample("search", "example")
                    .WithAlias("find")
                    .WithDescription("Searches the manifest for mods (Alias: find)");
            });

            return await app.RunAsync(args);
        }

        public static string AskPath()
        {
            return AnsiConsole.Prompt(
                new TextPrompt<string>("Please enter the mods folder path:")
                    .DefaultValue(Utils.GetDefaultPath())
                    .DefaultValueStyle("gray")
                    .PromptStyle("green")
                    .AllowEmpty()
                    .ValidationErrorMessage("[red]That's not a valid directory[/]")
                    .Validate(path =>
                    {
                        return path switch
                        {
                            _ when string.IsNullOrWhiteSpace(path) => ValidationResult.Success(),
                            _ when !Directory.Exists(path) => ValidationResult.Error(),
                            _ => ValidationResult.Success(),
                        };
                    }));
        }

        internal sealed class DefaultCommand : AsyncCommand<DefaultCommand.Settings>
        {
            public sealed class Settings : CommandSettings
            {
                [Description("Path to resonite mods folder.")]
                [CommandArgument(0, "[ModsFolder]")]
                public string? ModsFolder { get; set; }

                [Description("GitHub authentication token for using GitHub's official API. Optional, alternative to RSS feed method.")]
                [CommandOption("-t|--token")]
                public string? Token { get; set; }

                [Description("Enables dry run mode. Checks for mod updates without installing them.")]
                [CommandOption("-d|--dry")]
                [DefaultValue(false)]
                public bool DryMode { get; set; }
            }

            public override async Task<int> ExecuteAsync([NotNull] CommandContext context, [NotNull] Settings settings)
            {
                if (!AnsiConsole.Profile.Capabilities.Interactive)
                {
                    AnsiConsole.MarkupLine("[red]Environment does not support interaction.[/]");
                    return 1;
                }

                (var settingsConfig, var loadedSettings, var overriddenSettings) = await LoadAndOverrideSettingsAsync(settings);

                var urls = Utils.GetFiles(settingsConfig.ModsFolder!);
                if (!urls.Any())
                {
                    AnsiConsole.MarkupLine("[red]No Mods found to update. Press any key to Exit.[/]");
                    Console.ReadKey();
                    return 1;
                }

                await DisplayModUpdateStatus(urls, settingsConfig);
                await UpdateAdditionalLibraries(settingsConfig);

                Utils.CheckAndSaveOverriddenSettings(settingsConfig, loadedSettings, overriddenSettings);

                AnsiConsole.MarkupLine($"[slateblue3]{(settingsConfig.DryMode ? "Finished Checking Mod Updates" : "Finished Updating mods")}. Press any key to Exit.[/]");
                Console.ReadKey();
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
                    0 => ("+", dryMode ? "[green]Update Available[/]" : "[green]Updated[/]"),
                    1 => ("-", "[dim]Up To Date[/]"),
                    2 => ("/", "[red]No Link variable found[/]"),
                    3 => ("/", "[red]Invalid Link variable, no releases found[/]"),
                    4 => ("/", "[dim]Ignored[/]"),
                    _ => ("/", "[red]Something went Wrong[/]")
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
                    AnsiConsole.MarkupLine($"[red]{dllName} not found. Skipping..[/]");
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
                [Description("Query to search for in the manifest.")]
                [CommandArgument(0, "[QUERY]")]
                public required string Query { get; set; }

                [Description("Set alternative manifest json url. It must match the RML manifest schema (Advanced)")]
                [CommandOption("-m|--manifest")]
                public string? Manifest { get; set; }
            }

            public override async Task<int> ExecuteAsync([NotNull] CommandContext context, [NotNull] Settings settings)
            {
                if (!AnsiConsole.Profile.Capabilities.Interactive)
                {
                    AnsiConsole.MarkupLine("[red]Environment does not support interaction.[/]");
                    return 1;
                }

                (var settingsConfig, var loadedSettings, var overriddenSettings) = await LoadAndOverrideSettingsAsync(settings);

                if (string.IsNullOrEmpty(settings.Query))
                {
                    AnsiConsole.MarkupLine("[red]No search term provided[/]");
                    return 0;
                }

                var results = await SearchAndDisplayResults(settings.Query, settingsConfig.Manifest ?? Utils.Manifest);

                if (results.Count == 0)
                {
                    AnsiConsole.MarkupLine($"[red]No results found for query: {settings.Query}. Press any key to Exit.[/]");
                    Console.ReadKey();
                }

                Utils.CheckAndSaveOverriddenSettings(settingsConfig, loadedSettings, overriddenSettings);

                return 0;
            }

            private async Task<List<SearchResult>> SearchAndDisplayResults(string query, string manifest)
            {
                var results = await AnsiConsole.Status()
                    .StartAsync($"Searching for {query}...", _ => Utils.SearchManifest(query, manifest));

                if (results.Count > 0)
                {
                    DisplayResultsTable(results);
                }

                return results;
            }

            private void DisplayResultsTable(List<SearchResult> results)
            {
                var table = new Table()
                    .AddColumn(new TableColumn("[cyan]Name[/]"))
                    .AddColumn(new TableColumn("[cyan]Author[/]"))
                    .AddColumn(new TableColumn("[cyan]ID[/]"))
                    .AddColumn(new TableColumn("[cyan]Version[/]"))
                    .AddColumn(new TableColumn("[cyan]Description[/]"))
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
                await AnsiConsole.Status().StartAsync("Loading settings...", async _ =>
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
                .StartAsync("Starting...", async ctx =>
                {
                    await Task.Delay(1000);
                    ctx.Status("Checking Arguments...");

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
                        AnsiConsole.Write(new Padder(new Markup($"[yellow]Arguments[/]")).Padding(0, 0));

                        foreach (string property in propertiesList)
                        {
                            AnsiConsole.Write(new Padder(new Markup(property)).Padding(1, 0));
                        }
                    }
                });

            if (typeof(TSettings) == typeof(DefaultCommand.Settings))
            {
                settingsConfig.ModsFolder ??= AskPath();
            }

            return (settingsConfig, loadedSettings == null ? true : false, overriddenSettings.Any() ? true : false);
        }
    }
}