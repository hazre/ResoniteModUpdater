namespace ResoniteModUpdater
{
  public static class Strings
  {
    public static class Application
    {
      public static string AppName = "ResoniteModUpdater";
    }
    public static class Commands
    {
      public const string Update = "update";
      public const string UpdateAlias1 = "up";
      public const string UpdateAlias2 = "upgrade";
      public const string Search = "search";
      public const string SearchAlias = "find";
    }

    public static class Descriptions
    {
      public const string UpdateMods = "Updates resonite mods";
      public const string UpdateLibraries = "Updates libraries such as ResoniteModLoader and Harmony";
      public const string SearchModManifest = "Searches the manifest for mods";
      public const string Query = "Query to search for in the mod manifest.";
      public const string ModsFolder = "Path to resonite mods folder";
      public const string Token = "GitHub authentication token for using GitHub's official API. Optional, alternative to RSS feed method";
      public const string DryMode = "Enables dry run mode. Checks for mod updates without installing them";
      public const string Version = "Display version in use";
      public const string Manifest = "Set alternative manifest json url. It must match the RML manifest schema (Advanced)";
    }

    public static class Examples
    {
      public const string Empty = "";
      public const string Update = "update";
      public const string UpdateWithPath = "update {0}";
      public const string UpdateWithPathAndToken = "update {0} -token xxxxxxxxxxxxxx";
      public const string SearchExample = "search example";
    }

    public static class MenuOptions
    {
      public const string UpdateMods = "Update Mods";
      public const string UpdateLibraries = "Update Libraries";
      public const string SearchModManifest = "Search Mod Manifest";
      public const string ExitApplication = "Exit Application";
    }

    public static class Prompts
    {
      public const string InteractivePrompt = "What would you like to do?";
      public const string EnterModsFolderPath = "Enter the path to the mods folder:";
      public const string EnableDryRunMode = "Enable dry run mode?";
      public const string EnterSearchQuery = "Enter your search query:";
      public const string ReturnToMainMenu = "Return to main menu?";
      public const string SaveOverriddenSettings = "Do you want to update your saved settings with the overridden values?";
      public const string SaveSettings = "No settings file found. Do you want to save the current settings?";
      public const string Update = "There is a update available, would you like to update?";
      public const string UpdateLibraries = "There is an update available for {0}. Would you like to update it?";
    }

    public static class Messages
    {
      public const string PressKeyExit = "Press any key to Exit.";
      public const string FinishedUpdatingMods = "Finished Checking Mod Updates.";
      public const string FinishedCheckingMods = "Finished Updating mods.";
      public const string FinishedSearchingMods = "Finished searching for mods.";
      public const string Arguments = "Arguments";
      public const string OverriddenSettings = "The following settings were overridden by command-line arguments:";
      public const string SettingsUpdated = "Settings updated and saved successfully.";
      public const string SettingsSaved = "Settings saved successfully.";
      public const string DownloadingUpdate = "Downloading new version..";
      public const string InstallingUpdate = "Installing new version and restarting..";
      public const string NoUpdateLibraries = "No update available for {0}";

    }
    public static class Errors
    {
      public const string NotInteractiveConsole = "Environment does not support interaction.";
      public const string NotValidDirectory = "That's not a valid directory.";
      public const string NoModsToUpdate = "No Mods found to update.";
      public const string DLLNotFoundSkipping = "{0} not found. Skipping..";
      public const string Exception = "An error occurred: {0}";
      public const string NoSearchTerm = "No search term provided";
      public const string NoResultsQuery = "No results found for query: {0}";
      public const string ForbiddenRetry = "Attempt {0}: Access to the resource is forbidden. Retrying in 1 minute...";
      public const string Forbidden = "Access to the resource is forbidden after multiple attempts.";
      public const string InvalidToken = "Invalid token provided";
      public const string UpdateFailed = "Something went wrong trying to update {0}";
    }
    public static class ModStatus
    {
      public const string UpdateAvailable = "Update Available";
      public const string Updated = "Updated";
      public const string UpToDate = "Up To Date";
      public const string NoLinkFound = "No Link variable found";
      public const string InvalidLink = "Invalid Link variable, no releases found";
      public const string Ignored = "Ignored";
      public const string Error = "Something went Wrong";

      public static class Symbols
      {
        public const string Update = "+";
        public const string NoChange = "-";
        public const string Issue = "/";
      }
    }
    public static class Status
    {
      public const string Searching = "Searching for {0}...";
      public const string LoadingSettings = "Loading settings...";
      public const string Starting = "Starting...";
      public const string CheckingArguments = "Checking Arguments...";
    }
    public static class SearchTableHeaders
    {
      public const string Name = "Name";
      public const string Author = "Author";
      public const string ID = "ID";
      public const string Version = "Version";
      public const string Description = "Description";
    }
  }
}