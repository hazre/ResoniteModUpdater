using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.ServiceModel.Syndication;
using System.Xml;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Spectre.Console;

namespace ResoniteModUpdater
{
  public static class Utils
  {
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

    public static void SaveSettings(SettingsConfig settings)
    {
      var settingsJson = JsonConvert.SerializeObject(settings, Newtonsoft.Json.Formatting.Indented);
      var settingsFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, SettingsFileName);
      File.WriteAllText(settingsFilePath, settingsJson);
    }

    public static SettingsConfig? LoadSettings()
    {
      var settingsFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, SettingsFileName);
      if (File.Exists(settingsFilePath))
      {
        var settingsJson = File.ReadAllText(settingsFilePath);
        return JsonConvert.DeserializeObject<SettingsConfig>(settingsJson);
      }

      return null;
    }

    public static string? GetLibraryPath(string folderPath, string libraryFolderName, string dllFileName)
    {
      var parentFolderPath = Path.GetDirectoryName(folderPath);
      if (parentFolderPath == null) return null;

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
          var linkProperty = assembly.MainModule.Types
              .Where(t => t.BaseType?.Name == "ResoniteMod")
              .SelectMany(t => t.Properties)
              .FirstOrDefault(p => p.Name == "Link");

          if (linkProperty?.GetMethod?.HasBody == true)
          {
            var url = linkProperty.GetMethod.Body.Instructions
                .Where(i => i.OpCode == OpCodes.Ldstr)
                .Select(i => i.Operand as string)
                .FirstOrDefault(u => Uri.TryCreate(u, UriKind.Absolute, out Uri? uri) &&
                                     (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps) &&
                                     uri.Host.EndsWith("github.com") &&
                                     uri.ToString().TrimEnd('/').Split('/').Length > 4);


            if (Path.GetFileName(dllFile).StartsWith("_")) url = "_";
            urlDictionary[dllFile] = url;
            assembly?.Dispose();
          }
        }
        catch (Exception ex)
        {
          AnsiConsole.MarkupLine($"{Path.GetFileName(dllFile)}: [red]{ex.Message}[/]");
        }
      }

      var sortedUrlDictionary = urlDictionary.OrderBy(pair => pair.Key).ToDictionary(pair => pair.Key, pair => pair.Value);

      return sortedUrlDictionary;
    }

    public static async Task<(int, string?)> Download(string dllFile, string url, bool dryMode, string? token)
    {
      HttpClient client = new HttpClient();
      client.DefaultRequestHeaders.Add("User-Agent", "Resonite mod updater");
      if (token != null) client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
      client.DefaultRequestHeaders.Add("Accept", "application/vnd.github+json");

      int retryCount = 0;
      while (true)
      {
        string apiUrl = $"https://api.github.com/repos/{url.Split('/')[3]}/{url.Split('/')[4]}/releases/latest";
        var response = await client.GetAsync(apiUrl);

        if (response.IsSuccessStatusCode)
        {
          var responseBody = await response.Content.ReadAsStringAsync();
          dynamic release = JsonConvert.DeserializeObject(responseBody)!;
          JArray assets = release.assets;

          foreach (dynamic asset in assets)
          {
            if (asset.name == Path.GetFileName(dllFile))
            {
              string downloadUrl = asset.browser_download_url;

              return await DownloadAndValidateDLL(dllFile, downloadUrl, dryMode);
            }
          }
        }
        else if (response.StatusCode == HttpStatusCode.Forbidden)
        {
          AnsiConsole.MarkupLine($"Attempt {retryCount + 1}: Access to the resource is forbidden. Retrying in 1 minute...");
          retryCount++;
          if (retryCount > 3)
          {
            throw new Exception("Access to the resource is forbidden after multiple attempts.");
          }
          await Task.Delay(TimeSpan.FromMinutes(1));
          continue;
        }
        else if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
          throw new Exception("Invalid token provided");
        }

        return (3, null);
      }
    }

    public static async Task<(int, string?)> DownloadFromRSS(string dllFile, string url, bool dryMode)
    {
      try
      {
        string[] urlParts = url.Split('/');
        string owner = urlParts[3];
        string repo = urlParts[4];

        using var reader = XmlReader.Create($"https://github.com/{owner}/{repo}/tags.atom");
        var tags = SyndicationFeed.Load(reader);

        if (!tags.Items.Any()) return (3, null);

        var latest = tags.Items.First();
        if (latest?.Title == null) return (3, null);

        string tag = latest.Links[0].Uri.ToString().Split('/')[7];
        string downloadUrl = $"https://github.com/{owner}/{repo}/releases/download/{tag}/{Path.GetFileName(dllFile)}";

        return await DownloadAndValidateDLL(dllFile, downloadUrl, dryMode);
      }
      catch
      {
        return (-1, null);
      }
    }

    private static async Task<(int, string?)> DownloadAndValidateDLL(string dllFile, string downloadUrl, bool dryMode)
    {
      using var client = new HttpClient();
      client.DefaultRequestHeaders.Add("Accept", "application/octet-stream");

      try
      {
        byte[] downloadedDllBytes = await client.GetByteArrayAsync(downloadUrl);
        byte[] existingDllBytes = File.ReadAllBytes(dllFile);

        string downloadedHashString = ComputeHash(downloadedDllBytes);
        string existingHashString = ComputeHash(existingDllBytes);

        if (downloadedHashString != existingHashString)
        {
          if (!dryMode) File.WriteAllBytes(dllFile, downloadedDllBytes);
          return (0, downloadUrl);
        }
        else
        {
          return (1, null);
        }
      }
      catch
      {
        return (-1, null);
      }
    }

    private static string ComputeHash(byte[] data)
    {
      using var md5 = MD5.Create();
      return BitConverter.ToString(md5.ComputeHash(data)).Replace("-", string.Empty);
    }

    public static async Task<List<SearchResult>> SearchManifest(string searchTerm, string manifestUrl)
    {
      var results = new List<SearchResult>();
      string manifestContent = await DownloadManifest(manifestUrl);

      if (string.IsNullOrEmpty(manifestContent)) return results;

      var manifest = JsonConvert.DeserializeObject<ManifestData>(manifestContent);

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
      using HttpClient client = new HttpClient();
      return await client.GetStringAsync(url);
    }

    public static void NotifyOverriddenSettings(List<string> overriddenSettings)
    {
      if (overriddenSettings.Any())
      {
        AnsiConsole.MarkupLine("[yellow]The following settings were overridden by command-line arguments:[/]");
        foreach (var setting in overriddenSettings)
        {
          AnsiConsole.Write(new Padder(new Markup($"[yellow]+[/] {setting}")).Padding(1, 0));
        }
      }
    }

    public static void CheckAndSaveOverriddenSettings(SettingsConfig settingsConfig, bool loadedSettings, bool overriddenSettings)
    {

      if (overriddenSettings)
      {
        bool updateSettings = AnsiConsole.Confirm("Do you want to update your saved settings with the overridden values?");
        if (updateSettings)
        {
          SaveSettings(settingsConfig);
          AnsiConsole.MarkupLine("[green]Settings updated and saved successfully.[/]");
        }
      }
      else if (loadedSettings)
      {
        bool saveSettings = AnsiConsole.Confirm("No settings file found. Do you want to save the current settings?");
        if (saveSettings)
        {
          SaveSettings(settingsConfig);
          AnsiConsole.MarkupLine("[green]Settings saved successfully.[/]");
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
              // Special handling for the Token property
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
  }
}