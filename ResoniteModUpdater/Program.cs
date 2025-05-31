using ResoniteModUpdater.Commands.Default;
using ResoniteModUpdater.Commands.Search;
using ResoniteModUpdater.Commands.Update;
using Spectre.Console;
using Spectre.Console.Cli;
using Velopack;
using Velopack.Sources;

namespace ResoniteModUpdater
{
    public static class Program
    {
        private static readonly string AppUpdateRepoUrl = "https://github.com/hazre/ResoniteModUpdater";
        public static async Task<int> Main(string[] args)
        {
            VelopackApp.Build().Run();
#if !DEBUG
            await UpdateMyApp();
#endif
            var app = new CommandApp<DefaultCommand>();
            app.Configure(config =>
            {
                config.SetApplicationName(Strings.Application.AppName);
                config.AddExample(Strings.Examples.Empty);
                config.AddExample(Strings.Examples.Update);
                config.AddExample(Strings.Examples.SearchExample);

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
        public static async Task UpdateMyApp()
        {
            try
            {
                var mgr = new UpdateManager(new GithubSource(AppUpdateRepoUrl, null, false));

                AnsiConsole.MarkupLine(Strings.Messages.CheckingForUpdate);
                var newVersion = await mgr.CheckForUpdatesAsync();
                if (newVersion == null)
                {
                    AnsiConsole.MarkupLine(Strings.Messages.NoUpdateAvailable);
                    return;
                }

                if (!AnsiConsole.Confirm($"{Strings.Prompts.Update} ({Utils.GetVersion()} -> {newVersion.TargetFullRelease.Version})")) return;

                AnsiConsole.MarkupLine(Strings.Messages.DownloadingUpdate);
                await mgr.DownloadUpdatesAsync(newVersion);

                AnsiConsole.MarkupLine(Strings.Messages.InstallingUpdate);
                mgr.ApplyUpdatesAndRestart(newVersion);
            }
            catch (Exception ex)
            {
                AnsiConsole.WriteException(ex, ExceptionFormats.ShortenEverything);
            }
        }
    }
}