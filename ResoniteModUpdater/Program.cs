using Mono.Cecil;
using Mono.Cecil.Cil;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using CommandLine;

class Options
{
    [Option('t', "path", Required = false, HelpText = "resonite mods path")]
    public string? path { get; set; }

    [Option('t', "bearerToken", Required = false, HelpText = "The bearer token.")]
    public string? BearerToken { get; set; }
}

internal class Program
{
    private static async Task Main(string[] args)
    {
        string folderPath = @"C:\Program Files (x86)\Steam\steamapps\common\Resonite\rml_mods";
        HttpClient client = new HttpClient();
        Parser.Default.ParseArguments<Options>(args)
        .WithParsed(options =>
        {
            if (options.BearerToken != null)
            {
                string bearerToken = options.BearerToken;
                AuthenticationHeaderValue authHeaderValue = new AuthenticationHeaderValue("Bearer", bearerToken);
                client.DefaultRequestHeaders.Authorization = authHeaderValue;

            }
            if (options.path != null)
            {
                folderPath = options.path;
            }
        });
        client.DefaultRequestHeaders.Add("User-Agent", "Resonite mod updater");
        string[] dllFiles = Directory.GetFiles(folderPath, "*.dll");

        var urlDictionary = new Dictionary<string, string>();

        foreach (string dllFile in dllFiles)
        {
            if (Path.GetFileName(dllFile).StartsWith("_")) continue;
            var assembly = AssemblyDefinition.ReadAssembly(dllFile);
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
                            if (Uri.TryCreate(url, UriKind.Absolute, out Uri uriResult)
                                && (uriResult.Scheme == Uri.UriSchemeHttp || uriResult.Scheme == Uri.UriSchemeHttps)
                                && uriResult.Host.EndsWith("github.com"))
                            {
                                Console.WriteLine($"Found: {Path.GetFileName(dllFile)}: {url}");

                                urlDictionary[dllFile] = url;
                                assembly?.Dispose();
                            }
                        }
                    }
                }
            }
        }

        foreach (var kvp in urlDictionary)
        {
            string dllFile = kvp.Key;
            string url = kvp.Value;

            string dl = $"https://api.github.com/repos/{url.Split('/')[3]}/{url.Split('/')[4]}/releases/latest";
            client.DefaultRequestHeaders.Add("Accept", "application/vnd.github+json");


        download:
            HttpResponseMessage response = await client.GetAsync(dl);
            if (response.IsSuccessStatusCode)
            {
                string responseBody = await response.Content.ReadAsStringAsync();
                dynamic release = JsonConvert.DeserializeObject(responseBody);
                string tagName = release.tag_name;
                JArray assets = release.assets;
                foreach (dynamic asset in assets)
                {
                    string fileName = asset.name;
                    if (fileName.EndsWith(".dll"))
                    {
                        // Download and replace the DLL file
                        string downloadUrl = asset.browser_download_url;
                        Console.WriteLine($"Download: {downloadUrl}");

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
                                File.WriteAllBytes(dllFile, downloadedDllBytes);
                                Console.WriteLine("DLL file replaced.");
                            }
                            else
                            {
                                Console.WriteLine("DLL file is up to date (hashes match).");
                            }
                        }
                    }
                }
            }
            else if (response.StatusCode == HttpStatusCode.Forbidden)
            {
                Console.WriteLine("Access to the resource is forbidden. Retrying in 1 minute...");
                await Task.Delay(TimeSpan.FromMinutes(1));
                goto download;
            }
        }
    }
}