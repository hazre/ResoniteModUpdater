using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using Spectre.Console;
using Spectre.Console.Cli;

namespace ResoniteModUpdater
{
    public static class Program
    {
        public static int Main(string[] args)
        {
            var app = new CommandApp<DefaultCommand>();
            app.Configure(config =>
            {
                config.SetApplicationName("ResoniteModUpdater");
                config.SetApplicationVersion("2.2.0");
                config.AddExample($"{Utils.GetDefaultPath()}");
                config.AddExample($"{Utils.GetDefaultPath()}", "-token xxxxxxxxxxxxxx");

                config.AddCommand<SearchCommand>("search")
                    .WithExample("search", "example")
                    .WithAlias("find")
                    .WithDescription("Searches the manifest for mods (Alias: find)");
            });

            return app.Run(args);
        }

        public static string AskPath()
        {
            return AnsiConsole.Prompt(
                new TextPrompt<string>($"Please enter the mods folder path:")
                    .DefaultValue(Utils.GetDefaultPath())
                    .DefaultValueStyle("gray")
                    .PromptStyle("green")
                    .AllowEmpty()
                    .ValidationErrorMessage($"[red]That's not a valid directory[/]")
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


        internal sealed class DefaultCommand : Command<DefaultCommand.Settings>
        {
            public sealed class Settings : CommandSettings
            {
                [Description("Path to resonite mods folder.")]
                [CommandArgument(0, "[ModsFolder]")]
                public string? ModsFolder { get; set; }

                [Description("GitHub authentication token to allow downloading from GitHub's official API as an alternative to using the RSS feed. This option is optional and can be used if preferred over the RSS feed method.")]
                [CommandOption("-t|--token")]
                public string? Token { get; set; }

                [Description("Enables dry run mode. Checks for mod updates without installing them.")]
                [CommandOption("-d|--dry")]
                [DefaultValue(false)]
                public bool DryMode { get; set; }
            }

            public override int Execute([NotNull] CommandContext context, [NotNull] Settings settings)
            {
                if (!AnsiConsole.Profile.Capabilities.Interactive)
                {
                    AnsiConsole.MarkupLine("[red]Environment does not support interaction.[/]");
                    return 1;
                }

                var settingsConfig = new Utils.SettingsConfig();

                // try to load settings from file
                var loadedSettings = Utils.LoadSettings();
                if (loadedSettings != null)
                {
                    AnsiConsole.Status()
                        .Start("Settings file found, Loading settings...", ctx =>
                        {
                            settingsConfig = loadedSettings;
                            Thread.Sleep(1000);
                        });
                };

                // Override with command-line arguments if provided
                var overriddenSettings = Utils.OverrideSettings(settingsConfig, settings);

                // Notify user about overridden settings
                Utils.NotifyOverriddenSettings(overriddenSettings);

                AnsiConsole.Status()
                    .Start("Starting...", ctx =>
                    {
                        Thread.Sleep(1000);
                        ctx.Status("Checking Arguments...");

                        AnsiConsole.Write(new Padder(new Markup($"[yellow]Arguments[/]")).Padding(0, 0));

                        if (!string.IsNullOrEmpty(settingsConfig.ModsFolder)) AnsiConsole.Write(new Padder(new Markup("[yellow]+[/] ModsFolder Found, skipping Prompt...")).Padding(1, 0));
                        if (!string.IsNullOrEmpty(settingsConfig.Token)) AnsiConsole.Write(new Padder(new Markup("[yellow]+[/] Github Token Found")).Padding(1, 0));
                        if (settingsConfig.DryMode) AnsiConsole.Write(new Padder(new Markup("[yellow]+[/] Dry run mode")).Padding(1, 0));
                    });

                settingsConfig.ModsFolder ??= AskPath();
                AnsiConsole.WriteLine();

                var urls = Utils.GetFiles(settingsConfig.ModsFolder).GetAwaiter().GetResult();
                AnsiConsole.Status()
                    .Start("Finding Mods...", ctx =>
                    {
                        Thread.Sleep(1000);
                    });

                try
                {
                    if (!urls.Any())
                    {
                        throw new Exception("No Mods found to update.");
                    }
                }
                catch (Exception ex)
                {
                    AnsiConsole.MarkupLine($"[red]{ex.Message} Press any key to Exit.[/]");
                    Console.ReadKey();
                    return 1;
                }


                AnsiConsole.Write(new Padder(new Markup($"[orange1]Mods ({urls.Count})[/]")).Padding(0, 0));

                AnsiConsole.Status()
                    .Start(settingsConfig.DryMode ? "Checking for Mod Updates..." : "Updating Mods...", ctx =>
                    {
                        Thread.Sleep(1000);
                    });

                var table = new Table();
                table.Border(TableBorder.None);
                table.LeftAligned();
                table.Collapse();
                table.HideHeaders();
                table.AddColumns("", "", "", "");
                table.Columns[0].Width(1);
                AnsiConsole.Live(new Padder(table).Padding(1, 0))
                    .Start(ctx =>
                    {
                        foreach (var url in urls)
                        {
                            string dllFile = url.Key;
                            string urlValue = url.Value;
                            (int, string?) result;
                            if (!string.IsNullOrEmpty(settingsConfig.Token))
                            {
                                result = Utils.Download(dllFile, urlValue, settingsConfig.DryMode, settingsConfig.Token).GetAwaiter().GetResult();
                            }
                            else
                            {
                                result = Utils.DownloadFromRSS(dllFile, urlValue, settingsConfig.DryMode).GetAwaiter().GetResult();
                            }
                            var text = result.Item1 switch
                            {
                                0 => new List<string> { "+", settingsConfig.DryMode ? "[green]Update Available[/]" : "[green]Updated[/]" },
                                1 => new List<string> { "-", "[dim]Up To Date[/]" },
                                _ => new List<string> { "/", "[red]Something went Wrong[/]" },
                            };
                            string? releaseUrl = null;
                            if (!string.IsNullOrEmpty(result.Item2))
                            {
                                string owner = result.Item2.Split('/')[3];
                                string repo = result.Item2.Split('/')[4];
                                string tag = result.Item2.Split('/')[7];
                                releaseUrl = $"https://github.com/{owner}/{repo}/releases/{tag}";
                            }
                            table.AddRow($"[orange1]{text[0]}[/]", Path.GetFileName(dllFile), $"{text[1]}", $"[link={releaseUrl}]{releaseUrl}[/]");
                            ctx.Refresh();
                        }
                    });

                string resoniteModLoaderDLL = "ResoniteModLoader.dll";
                string? resoniteModLoaderPath = Utils.GetLibraryPath(settingsConfig.ModsFolder, "Libraries", resoniteModLoaderDLL);
                if (!string.IsNullOrEmpty(resoniteModLoaderPath))
                {
                    (int, string?) result = Utils.DownloadFromRSS(resoniteModLoaderPath, settingsConfig.ResoniteModLoaderSource, true).GetAwaiter().GetResult();
                    if (result.Item1 == 0)
                    {
                        bool updateResoniteModLoader = AnsiConsole.Confirm($"There is a update available for {resoniteModLoaderDLL}, Would you like to update it?");
                        if (updateResoniteModLoader)
                        {
                            result = Utils.DownloadFromRSS(resoniteModLoaderPath, settingsConfig.ResoniteModLoaderSource, false).GetAwaiter().GetResult();
                        }
                    }
                }
                else
                {
                    AnsiConsole.MarkupLine($"[red]{resoniteModLoaderDLL} not found. Skipping..[/]");
                }

                string harmonyDLL = "0Harmony.dll";
                string? harmonyPath = Utils.GetLibraryPath(settingsConfig.ModsFolder, "rml_libs", harmonyDLL);
                if (!string.IsNullOrEmpty(harmonyPath))
                {
                    (int, string?) result = Utils.DownloadFromRSS(harmonyPath, settingsConfig.ResoniteModLoaderSource, true).GetAwaiter().GetResult();
                    if (result.Item1 == 0)
                    {
                        bool updateHarmony = AnsiConsole.Confirm($"There is a update available for {harmonyDLL}, Would you like to update it?");
                        if (updateHarmony)
                        {
                            result = Utils.DownloadFromRSS(harmonyPath, settingsConfig.ResoniteModLoaderSource, false).GetAwaiter().GetResult();
                        }
                    }
                }
                else
                {
                    AnsiConsole.MarkupLine($"[red]{harmonyDLL} not found. Skipping..[/]");
                }

                Utils.CheckAndSaveOverriddenSettings(overriddenSettings, settingsConfig, loadedSettings);

                AnsiConsole.WriteLine();
                AnsiConsole.MarkupLine($"[slateblue3]{(settingsConfig.DryMode ? "Finished Checking Mod Updates" : "Finished Updating mods")}. Press any key to Exit.[/]");
                Console.ReadKey();
                return 0;
            }
        }
        public class SearchCommandSettings : CommandSettings
        {
            [Description("Query to search for in the manifest.")]
            [CommandArgument(0, "[QUERY]")]
            public required string Query { get; set; }

            [Description("Set alternative manifest json url. It must match the RML manifest schema (Advanced)")]
            [CommandOption("-m|--manifest")]
            public string? manifest { get; set; }
        }

        public class SearchCommand : Command<SearchCommandSettings>
        {
            public override int Execute([NotNull] CommandContext context, [NotNull] SearchCommandSettings settings)
            {
                if (!AnsiConsole.Profile.Capabilities.Interactive)
                {
                    AnsiConsole.MarkupLine("[red]Environment does not support interaction.[/]");
                    return 1;
                }

                var settingsConfig = new Utils.SettingsConfig
                {
                    manifest = settings.manifest!,
                };

                // try to load settings from file
                var loadedSettings = Utils.LoadSettings();
                if (loadedSettings != null)
                {
                    AnsiConsole.Status()
                        .Start("Settings file found, Loading settings...", ctx =>
                        {
                            settingsConfig = loadedSettings;
                            Thread.Sleep(1000);
                        });
                };

                settingsConfig.manifest = settingsConfig.manifest ?? Utils.manifest;

                if (string.IsNullOrEmpty(settings.Query))
                {
                    AnsiConsole.Write(new Padder(new Markup("[red]No search term provided[/]")).Padding(1, 0));
                    return 0;
                }

                AnsiConsole.Status()
                    .Start($"Searching for {settings.Query}...", ctx =>
                    {
                        Thread.Sleep(1000);
                    });

                var results = Utils.SearchManifest(settings.Query, settingsConfig.manifest).GetAwaiter().GetResult();

                if (results.Count == 0)
                {
                    AnsiConsole.WriteLine();
                    AnsiConsole.Write(new Markup($"[red]No results found for query: {settings.Query}. Press any key to Exit.[/]"));
                    Console.ReadKey();
                    return 0;
                }

                var table = new Table()
                 .AddColumn(new TableColumn("[cyan]Name[/]"))
                 .AddColumn(new TableColumn("[cyan]Author[/]"))
                 .AddColumn(new TableColumn("[cyan]ID[/]"))
                 .AddColumn(new TableColumn("[cyan]Version[/]"))
                 .AddColumn(new TableColumn("[cyan]Description[/]"));

                table.ShowRowSeparators();

                if (AnsiConsole.Profile.Capabilities.Links)
                {
                    foreach (var result in results)
                    {
                        Uri? releaseUrl = null;

                        if (result.Entry.Versions[result.LatestVersion].ReleaseUrl != null)
                        {
                            releaseUrl = result.Entry.Versions[result.LatestVersion].ReleaseUrl;
                        }
                        else if (result.Entry.Versions[result.LatestVersion].Artifacts.Last().Url.Host.EndsWith("github.com"))
                        {
                            Uri uri = result.Entry.Versions[result.LatestVersion].Artifacts.Last().Url;
                            string path = uri.AbsolutePath;
                            string repoOwner = path.Split('/')[1];
                            string repoName = path.Split('/')[2];
                            string tagOrVersion = path.Split('/')[5];

                            releaseUrl = new Uri($"https://github.com/{repoOwner}/{repoName}/releases/tag/{tagOrVersion}");
                        }
                        else if (result.Entry.SourceLocation != null)
                        {
                            releaseUrl = result.Entry.SourceLocation;
                        }

                        table.AddRow(result.Entry.Name, result.AuthorName, result.ID, $"[link={releaseUrl}]{result.LatestVersion}[/]", result.Entry.Description);
                    }
                }
                else
                {
                    foreach (var result in results)
                    {
                        table.AddRow(result.Entry.Name, result.AuthorName, result.ID, result.LatestVersion, result.Entry.Description);
                    }
                }


                AnsiConsole.Write(table);

                return 0;
            }
        }
    }
}
