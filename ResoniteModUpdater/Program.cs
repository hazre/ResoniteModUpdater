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

                // try to load settings from file
                var loadedSettings = Utils.LoadSettings();
                if (loadedSettings != null)
                {
                    AnsiConsole.Status()
                        .Start("Settings file found, Loading settings...", ctx =>
                        {
                            settings = loadedSettings;
                            Thread.Sleep(1000);
                        });
                };

                AnsiConsole.Status()
                    .Start("Starting...", ctx =>
                    {
                        Thread.Sleep(1000);
                        ctx.Status("Checking Arguments...");

                        AnsiConsole.Write(new Padder(new Markup($"[yellow]Arguments {(loadedSettings != null ? "(Loaded from [green]settings.json[/])" : "")}[/]")).Padding(0, 0));

                        if (!string.IsNullOrEmpty(settings.ModsFolder)) AnsiConsole.Write(new Padder(new Markup("[yellow]+[/] ModsFolder Found, skipping Prompt...")).Padding(1, 0));
                        if (!string.IsNullOrEmpty(settings.Token)) AnsiConsole.Write(new Padder(new Markup("[yellow]+[/] Github Token Found")).Padding(1, 0));
                        if (settings.DryMode) AnsiConsole.Write(new Padder(new Markup("[yellow]+[/] Dry run mode")).Padding(1, 0));
                    });

                settings.ModsFolder ??= AskPath();
                AnsiConsole.WriteLine();

                var urls = Utils.GetFiles(settings.ModsFolder).GetAwaiter().GetResult();
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
                    .Start(settings.DryMode ? "Checking for Mod Updates..." : "Updating Mods...", ctx =>
                    {
                        Thread.Sleep(1000);
                    });

                var table = new Table();
                table.Border(TableBorder.None);
                table.LeftAligned();
                table.Collapse();
                table.HideHeaders();
                table.AddColumns("", "", "");
                table.Columns[0].Width(1);
                AnsiConsole.Live(new Padder(table).Padding(1, 0))
                    .Start(ctx =>
                    {
                        foreach (var url in urls)
                        {
                            string dllFile = url.Key;
                            string urlValue = url.Value;
                            int result;
                            if (!string.IsNullOrEmpty(settings.Token))
                            {
                                result = Utils.Download(dllFile, urlValue, settings.DryMode, settings.Token).GetAwaiter().GetResult();
                            }
                            else
                            {
                                result = Utils.DownloadFromRSS(dllFile, urlValue, settings.DryMode).GetAwaiter().GetResult();
                            }
                            var text = result switch
                            {
                                0 => new List<string> { "+", settings.DryMode ? "[green]Update Available[/]" : "[green]Updated[/]" },
                                1 => new List<string> { "-", "[dim]Up To Date[/]" },
                                _ => new List<string> { "/", "[red]Something went Wrong[/]" },
                            };
                            table.AddRow($"[orange1]{text[0]}[/]", Path.GetFileName(dllFile), $"{text[1]}");
                            ctx.Refresh();
                        }
                    });

                if (loadedSettings == null)
                {
                    // Ask user if they want to save settings
                    bool saveSettings = AnsiConsole.Confirm("Do you want to save the current settings?");
                    if (saveSettings)
                    {
                        // Save settings to file
                        Utils.SaveSettings(settings);
                    }
                }

                AnsiConsole.WriteLine();
                AnsiConsole.MarkupLine($"[slateblue3]{(settings.DryMode ? "Finished Checking Mod Updates" : "Finished Updating mods")}. Press any key to Exit.[/]");
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
            [DefaultValue("https://raw.githubusercontent.com/resonite-modding-group/resonite-mod-manifest/main/manifest.json")]
            public required string manifest { get; set; }
        }

        public class SearchCommand : Command<SearchCommandSettings>
        {
            public override int Execute([NotNull] CommandContext context, [NotNull] SearchCommandSettings settings)
            {
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

                var results = Utils.SearchManifest(settings.Query, settings.manifest).GetAwaiter().GetResult();

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
