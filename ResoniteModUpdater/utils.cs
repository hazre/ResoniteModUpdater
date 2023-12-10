using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Spectre.Console;
using static ResoniteModUpdater.Program.DefaultCommand;

namespace ResoniteModUpdater
{
  public static class Utils
  {
    internal static string SettingsFileName = "settings.json";
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

    internal static void SaveSettings(Settings settings)
    {
      var settingsJson = JsonConvert.SerializeObject(settings, Formatting.Indented);
      var settingsFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, SettingsFileName);
      File.WriteAllText(settingsFilePath, settingsJson);
    }

    internal static Settings? LoadSettings()
    {
      var settingsFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, SettingsFileName);
      if (File.Exists(settingsFilePath))
      {
        var settingsJson = File.ReadAllText(settingsFilePath);
        return JsonConvert.DeserializeObject<Settings>(settingsJson);
      }

      return null;
    }

    public static Task<Dictionary<string, string>> GetFiles(string folderPath)
    {
      string[] dllFiles = Directory.GetFiles(folderPath, "*.dll");

      var urlDictionary = new Dictionary<string, string>();

      foreach (string dllFile in dllFiles)
      {
        if (Path.GetFileName(dllFile).StartsWith("_")) continue;
        AssemblyDefinition assembly;
        try
        {
          assembly = AssemblyDefinition.ReadAssembly(dllFile);
        }
        catch (Exception ex)
        {
          AnsiConsole.MarkupLine($"{Path.GetFileName(dllFile)}: [red]{ex.Message}[/]");
          continue;
        }
        var types = assembly.MainModule.Types;

        PropertyDefinition? linkProperty = null;
        foreach (var type in types)
        {
          if (type.BaseType != null && type.BaseType.Name == "ResoniteMod")
          {
            linkProperty = type.Properties.FirstOrDefault(p => p.Name == "Link");
            if (linkProperty != null)
            {
              break;
            }
          }
        }

        if (linkProperty != null)
        {
          var getterMethod = linkProperty.GetMethod;
          if (getterMethod.HasBody)
          {
            var instructions = getterMethod.Body.Instructions;
            foreach (var instruction in instructions)
            {
              if (instruction.OpCode == OpCodes.Ldstr)
              {
                string? url = instruction.Operand as string;
                if (Uri.TryCreate(url, UriKind.Absolute, out Uri? uriResult)
                    && (uriResult.Scheme == Uri.UriSchemeHttp || uriResult.Scheme == Uri.UriSchemeHttps)
                    && uriResult.Host.EndsWith("github.com") && uriResult.ToString().TrimEnd('/').Split('/').Length > 4)
                {
                  urlDictionary[dllFile] = url;
                  assembly?.Dispose();
                }
              }
            }
          }
        }
      }
      return Task.FromResult(urlDictionary);
    }
    public static async Task<int> Download(string dllFile, string url, bool dryMode, string? token)
    {
      HttpClient client = new HttpClient();
      client.DefaultRequestHeaders.Add("User-Agent", "Resonite mod updater");
      if (token != null) client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
      download:
      string dl = $"https://api.github.com/repos/{url.Split('/')[3]}/{url.Split('/')[4]}/releases/latest";
      client.DefaultRequestHeaders.Add("Accept", "application/vnd.github+json");
      HttpResponseMessage response = await client.GetAsync(dl);
      if (response.IsSuccessStatusCode)
      {
        string responseBody = await response.Content.ReadAsStringAsync();
        dynamic release = JsonConvert.DeserializeObject(responseBody)!;
        string tagName = release.tag_name;
        JArray assets = release.assets;
        foreach (dynamic asset in assets)
        {
          string fileName = asset.name;
          if (fileName == Path.GetFileName(dllFile))
          {
            // Download and replace the DLL file
            string downloadUrl = asset.browser_download_url;

            // Use the downloadUrl to download the DLL file and replace the existing one
            client.DefaultRequestHeaders.Add("Accept", "application/octet-stream");
            byte[] downloadedDllBytes = await client.GetByteArrayAsync(downloadUrl);
            // Compute the hash of the downloaded DLL
            using (var md5 = MD5.Create())
            {
              byte[] downloadedHash = md5.ComputeHash(downloadedDllBytes);
              string downloadedHashString = BitConverter.ToString(downloadedHash).Replace("-", string.Empty);

              // Compute the hash of the existing DLL
              byte[] existingDllBytes = File.ReadAllBytes(dllFile);
              byte[] existingHash = md5.ComputeHash(existingDllBytes);
              string existingHashString = BitConverter.ToString(existingHash).Replace("-", string.Empty);

              if (downloadedHashString != existingHashString)
              {
                // Hashes are different, replace the DLL
                if (!dryMode) File.WriteAllBytes(dllFile, downloadedDllBytes);
                return 0;
              }
              else
              {
                return 1;
              }
            }
          }
        }
      }
      else if (response.StatusCode == HttpStatusCode.Forbidden)
      {
        AnsiConsole.MarkupLine("Access to the resource is forbidden. Retrying in 1 minute...");
        await Task.Delay(TimeSpan.FromMinutes(1));
        goto download;
        // throw new Exception("Access to the resource is forbidden.");
      }
      return -1;
    }

  }
}