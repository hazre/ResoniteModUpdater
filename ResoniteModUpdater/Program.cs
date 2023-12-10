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
                config.SetApplicationVersion("2.0.0");
                config.AddExample($"{Utils.GetDefaultPath()}");
                config.AddExample($"{Utils.GetDefaultPath()}", "-token xxxxxxxxxxxxxx");
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
                public string? ModsFolder { get; init; }

                [Description("GitHub authentication token to bypass the 60 requests per hour limit. Only necessary if you plan to run the command multiple times within a short period.")]
                [CommandOption("-t|--token")]
                public string? Token { get; init; }

                [Description("Enables dry run mode. Checks for mod updates without installing them.")]
                [CommandOption("-d|--dry")]
                [DefaultValue(false)]
                public bool DryMode { get; init; }
            }

            public override int Execute([NotNull] CommandContext context, [NotNull] Settings settings)
            {
                if (!AnsiConsole.Profile.Capabilities.Interactive)
                {
                    AnsiConsole.MarkupLine("[red]Environment does not support interaction.[/]");
                    return 1;
                }

                AnsiConsole.Status()
                    .Start("Starting...", ctx =>
                    {
                        Thread.Sleep(1000);
                        ctx.Status("Checking Arguments...");

                        AnsiConsole.Write(new Padder(new Markup("[yellow]Arguments[/]")).Padding(0, 0));

                        if (!string.IsNullOrEmpty(settings.ModsFolder)) AnsiConsole.Write(new Padder(new Markup("[yellow]+[/] ModsFolder Found, skipping Prompt...")).Padding(1, 0));
                        if (!string.IsNullOrEmpty(settings.Token)) AnsiConsole.Write(new Padder(new Markup("[yellow]+[/] Github Token Found.")).Padding(1, 0));
                    });

                AnsiConsole.WriteLine();

                string folderPath = settings.ModsFolder ?? AskPath();
                AnsiConsole.WriteLine();

                var urls = Utils.GetFiles(folderPath).GetAwaiter().GetResult();
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
                    return 1; // Return an error code
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
                            var result = Utils.Download(dllFile, urlValue, settings.DryMode, settings.Token).GetAwaiter().GetResult();
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
                AnsiConsole.WriteLine();
                AnsiConsole.MarkupLine($"[slateblue3]{(settings.DryMode ? "Finished Checking Mod Updates" : "Finished Updating mods")}. Press any key to Exit.[/]");
                Console.ReadKey();
                return 0;
            }
        }
    }
}
