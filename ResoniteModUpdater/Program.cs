using ResoniteModUpdater.Commands.Default;
using ResoniteModUpdater.Commands.Search;
using ResoniteModUpdater.Commands.Update;
using Spectre.Console.Cli;
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
    }
}