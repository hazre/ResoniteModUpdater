using System.Net;
using System.Net.Http.Headers;
using System.Reflection;
using System.Security.Cryptography;
using System.ServiceModel.Syndication;
using System.Xml;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Newtonsoft.Json;
using ResoniteModUpdater.Commands.Search;
using ResoniteModUpdater.Commands.Update;
using Spectre.Console;

namespace ResoniteModUpdater
{
  public static class Utils
  {
    private static readonly HttpClient _httpClient = new()
    {
      DefaultRequestHeaders =
        {
            UserAgent = { new ProductInfoHeaderValue(Strings.Application.AppName, GetVersion()) },
            Accept = { new MediaTypeWithQualityHeaderValue("application/vnd.github+json") }
        }
    };

    public class SettingsConfig
    {
      public string? ModsFolder { get; set; }
      public string? Token { get; set; }
      public bool DryMode { get; set; }
      public string? ResoniteModLoaderSource { get; set; }
      public string? Manifest { get; set; }
    }
    private const string SettingsFileName = "settings.json";
    public const string ResoniteModLoaderSource = "https://github.com/resonite-modding-group/ResoniteModLoader";
    public const string Manifest = "https://raw.githubusercontent.com/resonite-modding-group/resonite-mod-manifest/main/manifest.json";
    public static string GetDefaultPath()
    {
      string defaultPath = "";
      if (Environment.OSVersion.Platform == PlatformID.Win32NT)
      {
        defaultPath = Environment.ExpandEnvironmentVariables(@"%PROGRAMFILES(X86)%\Steam\steamapps\common\Resonite\rml_mods");
      }
      else if (Environment.OSVersion.Platform == PlatformID.Unix)
      {
        defaultPath = Environment.ExpandEnvironmentVariables(@"~/.steam/steam/steamapps/common/Resonite/rml_mods");
      }
      return defaultPath;
    }

    public static string GetSettingsPath()
    {
      if (Environment.OSVersion.Platform == PlatformID.Unix)
      {
        string? configDir = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME");
        if (string.IsNullOrEmpty(configDir))
        {
          string? homeDir = Environment.GetEnvironmentVariable("HOME");
          if (string.IsNullOrEmpty(homeDir))
          {
            AnsiConsole.MarkupLine($"[red]Error: HOME environment variable not found. Cannot determine settings path.[/]");
            return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, SettingsFileName);
          }
          configDir = Path.Combine(homeDir, ".config");
        }
        string appConfigDir = Path.Combine(configDir, Strings.Application.AppName);
        Directory.CreateDirectory(appConfigDir);
        return Path.Combine(appConfigDir, SettingsFileName);
      }
      else
      {
        string executablePath = AppDomain.CurrentDomain.BaseDirectory;
        string parentDir = Directory.GetParent(executablePath.TrimEnd(Path.DirectorySeparatorChar))?.FullName ?? executablePath;
        return Path.Combine(parentDir, SettingsFileName);
      }
    }

    public static void SaveSettings(SettingsConfig settings)
    {
      var settingsJson = JsonConvert.SerializeObject(settings, Newtonsoft.Json.Formatting.Indented);

      string settingsFilePath = GetSettingsPath();
      try
      {
        File.WriteAllText(settingsFilePath, settingsJson);
      }
      catch (Exception ex)
      {
        AnsiConsole.MarkupLine($"[red]Error saving settings to {settingsFilePath}:[/]");
        AnsiConsole.WriteException(ex, ExceptionFormats.ShortenEverything);
      }
    }
    public static SettingsConfig? LoadSettings()
    {
      string settingsFilePath = GetSettingsPath();

      if (File.Exists(settingsFilePath))
      {
        try
        {
          var settingsJson = File.ReadAllText(settingsFilePath);
          return JsonConvert.DeserializeObject<SettingsConfig>(settingsJson);
        }
        catch (Exception ex)
        {
          AnsiConsole.MarkupLine($"[red]Error loading or parsing settings from {settingsFilePath}:[/]");
          AnsiConsole.WriteException(ex, ExceptionFormats.ShortenEverything);
        }
      }
      return null;
    }
    public static string? GetLibraryPath(string folderPath, string libraryFolderName, string dllFileName)
    {
      var parentFolderPath = Path.GetDirectoryName(folderPath);
      if (string.IsNullOrEmpty(parentFolderPath)) return null;

      var librariesFolderPath = Path.Combine(parentFolderPath, libraryFolderName);
      if (!Directory.Exists(librariesFolderPath)) return null;

      var dllFilePath = Path.Combine(librariesFolderPath, dllFileName);
      return File.Exists(dllFilePath) ? dllFilePath : null;
    }

    public static Dictionary<string, string?> GetFiles(string folderPath)
    {
      string[] dllFiles = Directory.GetFiles(folderPath, "*.dll");
      var urlDictionary = new Dictionary<string, string?>();

      foreach (string dllFile in dllFiles)
      {
        try
        {
          using var assembly = AssemblyDefinition.ReadAssembly(dllFile);
          string? foundUrl = null;

          foreach (var typeDef in assembly.MainModule.Types)
          {
            if (typeDef.BaseType?.Name == "ResoniteMod")
            {
              foreach (var propDef in typeDef.Properties)
              {
                if (propDef.Name == "Link" && propDef.GetMethod?.HasBody == true)
                {
                  foreach (var instruction in propDef.GetMethod.Body.Instructions)
                  {
                    if (instruction.OpCode == OpCodes.Ldstr && instruction.Operand is string potentialUrl)
                    {
                      if (Uri.TryCreate(potentialUrl, UriKind.Absolute, out Uri? uri) &&
                          (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps) &&
                          uri.Host.EndsWith("github.com") &&
                          uri.ToString().TrimEnd('/').Split('/').Length > 4)
                      {
                        foundUrl = potentialUrl;
                        break;
                      }
                    }
                  }
                }
                if (foundUrl != null) break;
              }
            }
            if (foundUrl != null) break;
          }

          if (Path.GetFileName(dllFile).StartsWith("_")) foundUrl = "_";
          urlDictionary[dllFile] = foundUrl;
        }
        catch (BadImageFormatException)
        {
          AnsiConsole.MarkupLine($"[yellow]Warning: Could not read assembly information from '{Path.GetFileName(dllFile)}'. It might not be a valid .NET assembly or may be obfuscated.[/]");
          urlDictionary[dllFile] = null;
        }
        catch (Exception ex)
        {
          AnsiConsole.MarkupLine($"[red]Error processing {Path.GetFileName(dllFile)}:[/]");
          AnsiConsole.WriteException(ex, ExceptionFormats.ShortenEverything);
          urlDictionary[dllFile] = null;
        }
      }

      return urlDictionary.OrderBy(pair => pair.Key).ToDictionary(pair => pair.Key, pair => pair.Value);
    }

    private record GitHubAsset(string name, string browser_download_url);
    private record GitHubReleaseInfo(List<GitHubAsset> assets);
    public static async Task<(ModUpdateResultStatus Status, string? Url, Exception? Error)> Download(string dllFile, string url, bool dryMode, string? token)
    {
      string[] urlSegments = url.Split('/');
      if (urlSegments.Length < 5)
      {
        string errMsg = $"Invalid GitHub URL format: {url}";
        return (ModUpdateResultStatus.Error, null, new ArgumentException(errMsg, nameof(url)));
      }
      string owner = urlSegments[3];
      string repo = urlSegments[4];

      var requestMessage = new HttpRequestMessage(HttpMethod.Get, $"https://api.github.com/repos/{owner}/{repo}/releases/latest");
      if (token != null)
      {
        requestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
      }

      int retryCount = 0;
      while (true)
      {
        HttpResponseMessage response;
        try
        {
          response = await _httpClient.SendAsync(requestMessage);
        }
        catch (HttpRequestException ex)
        {
          return (ModUpdateResultStatus.Error, null, ex);
        }

        if (response.IsSuccessStatusCode)
        {
          var responseBody = await response.Content.ReadAsStringAsync();
          GitHubReleaseInfo? releaseInfo;
          try
          {
            releaseInfo = JsonConvert.DeserializeObject<GitHubReleaseInfo>(responseBody);
          }
          catch (JsonException jsonEx)
          {
            return (ModUpdateResultStatus.Error, null, jsonEx);
          }

          if (releaseInfo?.assets == null)
          {
            string errMsg = $"Unexpected JSON structure from GitHub API for {owner}/{repo}. 'assets' field is missing or null.";
            return (ModUpdateResultStatus.Error, null, new JsonException(errMsg));
          }

          foreach (var asset in releaseInfo.assets)
          {
            if (asset.name == Path.GetFileName(dllFile))
            {
              var (valStatus, valUrl, valException) = await DownloadAndValidateDLL(dllFile, asset.browser_download_url, dryMode);
              return (valStatus, valUrl, valException);
            }
          }
          string notFoundMsg = $"Warning: DLL '{Path.GetFileName(dllFile)}' not found in the latest release assets for {owner}/{repo}.";
          return (ModUpdateResultStatus.NoLinkFound, null, new FileNotFoundException(notFoundMsg, dllFile));
        }
        else if (response.StatusCode == HttpStatusCode.Forbidden && response.Headers.RetryAfter?.Delta.HasValue == true)
        {
          var delay = response.Headers.RetryAfter.Delta.Value;
          AnsiConsole.MarkupLine(string.Format(Strings.Errors.ForbiddenRetryWithDelay, retryCount + 1, delay.TotalSeconds));
          if (retryCount >= 3) throw new Exception(Strings.Errors.Forbidden);
          await Task.Delay(delay);
          retryCount++;
          requestMessage = new HttpRequestMessage(HttpMethod.Get, $"https://api.github.com/repos/{owner}/{repo}/releases/latest");
          if (token != null) requestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
          continue;
        }
        else if (response.StatusCode == HttpStatusCode.Forbidden)
        {
          AnsiConsole.MarkupLine(string.Format(Strings.Errors.ForbiddenRetry, retryCount + 1));
          if (retryCount >= 3) throw new Exception(Strings.Errors.Forbidden);
          await Task.Delay(TimeSpan.FromSeconds(60 * (retryCount + 1)));
          retryCount++;
          requestMessage = new HttpRequestMessage(HttpMethod.Get, $"https://api.github.com/repos/{owner}/{repo}/releases/latest");
          if (token != null) requestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
          continue;
        }
        else if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
          throw new Exception(Strings.Errors.InvalidToken);
        }
        else
        {
          AnsiConsole.MarkupLine($"[red]Failed to fetch release information from GitHub API for {owner}/{repo}. Status code: {response.StatusCode}[/]");
          var errorBody = await response.Content.ReadAsStringAsync();
          if (!string.IsNullOrWhiteSpace(errorBody))
          {
            AnsiConsole.MarkupLine($"[red]Response: {errorBody.Substring(0, Math.Min(errorBody.Length, 500))}[/]");
          }
          return (ModUpdateResultStatus.InvalidLink, null, new HttpRequestException($"Failed to fetch release information. Status: {response.StatusCode}, Body: {errorBody}"));
        }
      }
    }

    public static async Task<(ModUpdateResultStatus Status, string? Url, Exception? Error)> DownloadFromRSS(string dllFile, string url, bool dryMode)
    {
      try
      {
        string[] urlParts = url.Split('/');
        if (urlParts.Length < 5)
        {
          string errMsg = $"Invalid GitHub URL format for RSS: {url}";
          return (ModUpdateResultStatus.Error, null, new ArgumentException(errMsg, nameof(url)));
        }
        string owner = urlParts[3];
        string repo = urlParts[4];

        using var xmlClient = new HttpClient();
        xmlClient.DefaultRequestHeaders.UserAgent.ParseAdd(Strings.Application.AppName + "/" + GetVersion());


        using var stream = await xmlClient.GetStreamAsync($"https://github.com/{owner}/{repo}/tags.atom");
        using var reader = XmlReader.Create(stream);
        var tags = SyndicationFeed.Load(reader);

        if (!tags.Items.Any()) return (ModUpdateResultStatus.InvalidLink, null, new InvalidOperationException("No RSS/Atom feed items found."));

        var latest = tags.Items.First();
        if (latest?.Title?.Text == null || !latest.Links.Any()) return (ModUpdateResultStatus.InvalidLink, null, new InvalidOperationException("Latest RSS/Atom item is invalid or has no links."));

        string? tag = latest.Links.FirstOrDefault(l => l.RelationshipType == "alternate")?.Uri.Segments.LastOrDefault()?.TrimEnd('/');
        if (string.IsNullOrEmpty(tag))
        {
          tag = latest.Id?.Split('/').LastOrDefault()?.TrimEnd('/');
        }
        if (string.IsNullOrEmpty(tag)) return (ModUpdateResultStatus.InvalidLink, null, new InvalidOperationException("Could not determine tag from RSS/Atom feed item."));

        string constructedDownloadUrl = $"https://github.com/{owner}/{repo}/releases/download/{tag}/{Path.GetFileName(dllFile)}";

        var (valStatus, valUrl, valException) = await DownloadAndValidateDLL(dllFile, constructedDownloadUrl, dryMode);
        return (valStatus, valUrl, valException);
      }
      catch (Exception ex)
      {
        return (ModUpdateResultStatus.Error, null, ex);
      }
    }

    private static async Task<(ModUpdateResultStatus Status, string? Url, Exception? Error)> DownloadAndValidateDLL(string dllFile, string downloadUrl, bool dryMode)
    {
      var requestMessage = new HttpRequestMessage(HttpMethod.Get, downloadUrl);
      requestMessage.Headers.Accept.Clear();
      requestMessage.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/octet-stream"));

      try
      {
        var response = await _httpClient.SendAsync(requestMessage);
        response.EnsureSuccessStatusCode();

        byte[] downloadedDllBytes = await response.Content.ReadAsByteArrayAsync();
        byte[] existingDllBytes = await File.ReadAllBytesAsync(dllFile);

        string downloadedHashString = ComputeHash(downloadedDllBytes);
        string existingHashString = ComputeHash(existingDllBytes);

        if (downloadedHashString != existingHashString)
        {
          if (!dryMode) await File.WriteAllBytesAsync(dllFile, downloadedDllBytes);
          return (ModUpdateResultStatus.Updated, downloadUrl, null);
        }
        else
        {
          return (ModUpdateResultStatus.UpToDate, null, null);
        }
      }
      catch (HttpRequestException httpEx)
      {
        return (ModUpdateResultStatus.Error, downloadUrl, httpEx);
      }
      catch (IOException ioEx)
      {
        return (ModUpdateResultStatus.Error, downloadUrl, ioEx);
      }
      catch (Exception ex)
      {
        return (ModUpdateResultStatus.Error, downloadUrl, ex);
      }
    }
    private static string ComputeHash(byte[] data)
    {
      using var sha256 = SHA256.Create();
      byte[] hashBytes = sha256.ComputeHash(data);
      return Convert.ToHexString(hashBytes);
    }

    public static async Task<List<SearchResult>> SearchManifest(string searchTerm, string manifestUrl)
    {
      var results = new List<SearchResult>();
      string manifestContent = await DownloadManifest(manifestUrl);
      if (string.IsNullOrEmpty(manifestContent)) return results;
      ManifestData? manifest = null;
      try
      {
        manifest = JsonConvert.DeserializeObject<ManifestData>(manifestContent);
      }
      catch (JsonException jsonEx)
      {
        AnsiConsole.MarkupLine($"[red]Error parsing manifest JSON from {manifestUrl}:[/]");
        AnsiConsole.WriteException(jsonEx, ExceptionFormats.ShortenEverything);
        return results;
      }

      if (manifest?.Objects == null) return results;
      foreach (var authorKey in manifest.Objects.Values)
      {
        foreach (var entryKey in authorKey.Entries)
        {
          if (entryKey.Value.Name.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) ||
              entryKey.Value.Description.Contains(searchTerm, StringComparison.OrdinalIgnoreCase))
          {
            string authorName = authorKey.Author.Keys.First();
            var latestVersionKey = entryKey.Value.Versions.Keys.Max();
            string id = entryKey.Key;
            results.Add(new SearchResult
            {
              Entry = entryKey.Value,
              ID = id,
              AuthorName = authorName,
              LatestVersion = latestVersionKey!,
              AuthorUrl = authorKey.Author.First().Value.Url,
            });
          }
        }
      }
      return results;
    }
    private static async Task<string> DownloadManifest(string url)
    {
      try
      {
        return await _httpClient.GetStringAsync(url);
      }
      catch (HttpRequestException httpEx)
      {
        AnsiConsole.MarkupLine($"[red]Error downloading manifest from {url}:[/]");
        AnsiConsole.WriteException(httpEx, ExceptionFormats.ShortenEverything);
        return string.Empty;
      }
      catch (Exception ex)
      {
        AnsiConsole.MarkupLine($"[red]An unexpected error occurred while downloading manifest from {url}:[/]");
        AnsiConsole.WriteException(ex, ExceptionFormats.ShortenEverything);
        return string.Empty;
      }
    }

    public static void NotifyOverriddenSettings(List<string> overriddenSettings)
    {
      if (overriddenSettings.Any())
      {
        AnsiConsole.MarkupLine($"[yellow]{Strings.Messages.OverriddenSettings}[/]");
        foreach (var setting in overriddenSettings)
        {
          AnsiConsole.Write(new Padder(new Markup($"[yellow]+[/] {setting}")).Padding(1, 0));
        }
      }
    }
    public static void CheckAndSaveOverriddenSettings(SettingsConfig settingsConfig, bool loadedSettings, bool overriddenSettings)
    {
      if (overriddenSettings && loadedSettings)
      {
        bool updateSettings = AnsiConsole.Confirm(Strings.Prompts.SaveOverriddenSettings);
        if (updateSettings)
        {
          SaveSettings(settingsConfig);
          AnsiConsole.MarkupLine($"[green]{Strings.Messages.SettingsUpdated}[/]");
        }
      }
      else if (!loadedSettings)
      {
        bool saveSettings = AnsiConsole.Confirm(Strings.Prompts.SaveSettings);
        if (saveSettings)
        {
          SaveSettings(settingsConfig);
          AnsiConsole.MarkupLine($"[green]{Strings.Messages.SettingsSaved}[/]");
        }
      }
    }
    public static (SettingsConfig, List<string>) OverrideSettings<TCliSettings>(SettingsConfig config, TCliSettings cliSettings)
    {
      var overriddenSettings = new List<string>();
      var configProperties = typeof(SettingsConfig).GetProperties();
      var cliProperties = typeof(TCliSettings).GetProperties();
      foreach (var cliProp in cliProperties)
      {
        var configProp = configProperties.FirstOrDefault(p => p.Name == cliProp.Name);
        if (configProp != null)
        {
          var newValue = cliProp.GetValue(cliSettings);
          if (IsValueProvided(newValue))
          {
            var oldValue = configProp.GetValue(config);
            if (!Equals(oldValue, newValue))
            {
              configProp.SetValue(config, newValue);
              if (cliProp.Name == nameof(SettingsConfig.Token))
              {
                newValue = "********";
              }
              string str1 = $"{cliProp.Name} [red]{oldValue}[/] -> [green]{newValue}[/]";
              string str2 = $"{cliProp.Name} [green]{newValue}[/]";
              overriddenSettings.Add(oldValue != null ? str1 : str2);
            }
          }
        }
      }
      return (config, overriddenSettings);
    }
    private static bool IsValueProvided(object? value)
    {
      return value switch
      {
        null => false,
        string s => !string.IsNullOrEmpty(s),
        _ => true
      };
    }
    public static async Task<(SettingsConfig, bool, bool)> LoadAndOverrideSettingsAsync<TSettings>(TSettings settings)
    {
      var settingsConfig = new SettingsConfig();
      var loadedSettings = LoadSettings();
      if (loadedSettings != null)
      {
        await AnsiConsole.Status().StartAsync(Strings.Status.LoadingSettings, async _ =>
        {
          settingsConfig = loadedSettings;
          await Task.Delay(1000);
        });
      }
      (settingsConfig, var overriddenSettings) = OverrideSettings(settingsConfig, settings);
      if (overriddenSettings.Any() && loadedSettings != null)
      {
        NotifyOverriddenSettings(overriddenSettings);
      }
      await AnsiConsole.Status()
          .StartAsync(Strings.Status.Starting, async ctx =>
          {
            await Task.Delay(1000);
            ctx.Status(Strings.Status.CheckingArguments);
            List<string> propertiesList = new List<string>();
            foreach (PropertyInfo property in typeof(SettingsConfig).GetProperties())
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
      return (settingsConfig, loadedSettings != null ? true : false, overriddenSettings.Any() ? true : false);
    }
    public static string AskPath()
    {
      return AnsiConsole.Prompt(
          new TextPrompt<string>(Strings.Prompts.EnterModsFolderPath)
              .DefaultValue(GetDefaultPath())
              .DefaultValueStyle("gray")
              .PromptStyle("green")
              .AllowEmpty()
              .ValidationErrorMessage($"[red]{Strings.Errors.NotValidDirectory}[/]")
              .Validate(path =>
              {
                return path switch
                {
                  _ when string.IsNullOrWhiteSpace(path) => ValidationResult.Success(),
                  _ when !Directory.Exists(path) => ValidationResult.Error(Strings.Errors.NotValidDirectory),
                  _ => ValidationResult.Success(),
                };
              }));
    }
    public static string GetVersion()
    {
      var assembly = Assembly.GetEntryAssembly();
      if (assembly == null) return "Unknown";

      var version = assembly.GetName().Version;
      return version != null ? $"{version.Major}.{version.Minor}.{version.Build}" : "Unknown Version";
    }

    public static async Task UpdateAdditionalLibraries(Utils.SettingsConfig settingsConfig, bool UpdateStatus = false)
    {
      if (string.IsNullOrEmpty(settingsConfig.ModsFolder) || !Directory.Exists(settingsConfig.ModsFolder))
      {
        AnsiConsole.MarkupLine($"[red]Mods folder path is not valid or not set. Skipping library updates.[/]");
        return;
      }
      await UpdateLibraryAsync("ResoniteModLoader.dll", "Libraries", settingsConfig, UpdateStatus);
      await UpdateLibraryAsync("0Harmony.dll", "rml_libs", settingsConfig, UpdateStatus);
    }

    public static async Task UpdateLibraryAsync(string dllName, string subFolder, Utils.SettingsConfig settingsConfig, bool UpdateStatus = false)
    {
      string? libraryPath = GetLibraryPath(settingsConfig.ModsFolder!, subFolder, dllName);
      if (string.IsNullOrEmpty(libraryPath))
      {
        AnsiConsole.MarkupLine($"[red]{string.Format(Strings.Errors.DLLNotFoundSkipping, dllName)}[/]");
        return;
      }

      var librarySourceUrl = settingsConfig.ResoniteModLoaderSource ?? ResoniteModLoaderSource;

      var (status, _, initialCheckException) = await DownloadFromRSS(libraryPath, librarySourceUrl, dryMode: true);

      if (status == ModUpdateResultStatus.Updated)
      {
        if (AnsiConsole.Confirm(string.Format(Strings.Prompts.UpdateLibraries, dllName)))
        {
          var (applyStatus, _, applyException) = await DownloadFromRSS(libraryPath, librarySourceUrl, dryMode: false);
          if (UpdateStatus)
          {
            if (applyStatus == ModUpdateResultStatus.Updated) AnsiConsole.MarkupLine($"[green]{dllName} updated successfully.[/]");
            else if (applyStatus == ModUpdateResultStatus.Error)
            {
              AnsiConsole.MarkupLine($"[red]{string.Format(Strings.Errors.UpdateFailed, dllName)}: {Markup.Escape(applyException?.Message ?? "Unknown error")}[/]");
            }
          }
        }
      }

      if (UpdateStatus)
      {
        if (status == ModUpdateResultStatus.UpToDate)
        {
          AnsiConsole.MarkupLine($"[slateblue3]{string.Format(Strings.Messages.NoUpdateLibraries, dllName)}[/]");
        }
        else if (status == ModUpdateResultStatus.Error)
        {
          AnsiConsole.MarkupLine($"[red]{string.Format(Strings.Errors.UpdateFailed, dllName)}: {Markup.Escape(initialCheckException?.Message ?? "Unknown error during check")}[/]");
        }
      }
    }
  }
}
