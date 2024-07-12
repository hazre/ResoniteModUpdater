# Resonite Mod Updater CLI

**ResoniteModUpdater** is a command-line tool that helps you update mods for Resonite. Updates only mods that have a GitHub Link variable.
> [!WARNING]
> Does not work with monorepos with multiple mods nor with non GitHub repositories.

## Prerequisites
- Have .NET 7.0 or **higher** Installed (SDK or just runtime)
  - Installer: https://dotnet.microsoft.com/en-us/download/dotnet/7.0
  - Winget: `winget install Microsoft.DotNet.Runtime.7`



## Installation

1. Download [ResoniteModUpdater_win_x64.zip](https://github.com/hazre/ResoniteModUpdater/releases/latest/download/ResoniteModUpdater_win_x64.zip) or [ResoniteModUpdater_linux_x64.zip](https://github.com/hazre/ResoniteModUpdater/releases/latest/download/ResoniteModUpdater_linux_x64.zip)
2. Extract it anywhere.
3. Double click to start or run from terminal.

## Usage

To use ResoniteModUpdater, you need to specify the mods folder path and, optionally, a GitHub authentication token.

> [!NOTE]
> If you have private mods or mods that you don't want to update. You can ignore mods by adding a `_` prefix to the mod's filename. 

```sh
ResoniteModUpdater [ModsFolder] [OPTIONS]
```

```sh
ResoniteModUpdater search [QUERY] [OPTIONS]
```

### Examples

1. Update Resonite mods without using an authentication token:

```sh
ResoniteModUpdater "C:\Program Files (x86)\Steam\steamapps\common\Resonite\rml_mods"
```

2. Update Resonite mods with a GitHub authentication token:

```sh
ResoniteModUpdater "C:\Program Files (x86)\Steam\steamapps\common\Resonite\rml_mods" -token xxxxxxxxxxxxxx
```

### Arguments

- `[ModsFolder]`: The path to the Resonite mods folder.

### Commands

-  `Search`: Searches the manifest for mods (Alias: find)

### Options

- `-h, --help`: Prints help information, providing usage instructions for the tool.
- `-v, --version`: Prints version information, displaying the version of ResoniteModUpdater.
- `-t, --token`: GitHub authentication token. Use this option only if you plan to run the command multiple times within a short period. The token helps bypass GitHub's request limits (60 Requests per hour).
- `-d, --dry`: Enables dry run mode. Checks for mod updates without installing them.
- `search -m, --manifest`: Set alternative manifest json url. It must match the RML manifest schema (Advanced)

## Settings File

The `settings.json` file is used to store the settings for the Resonite Mod Updater. This file is automatically created in the same directory as the ResoniteModUpdater executable when you choose to save your settings.

Here is an example of what the `settings.json` file might look like:

```json
{
  "ModsFolder": "C:\\Program Files (x86)\\Steam\\steamapps\\common\\Resonite\\rml_mods",
  "Token": null,
  "DryMode": false,
  "ResoniteModLoaderSource": "https://github.com/resonite-modding-group/ResoniteModLoader",
  "manifest": "https://raw.githubusercontent.com/resonite-modding-group/resonite-mod-manifest/main/manifest.json"
}
```

### Fields

- `ModsFolder`: The path to the Resonite mods folder.
- `Token`: GitHub authentication token to allow downloading from GitHub's official API as an alternative to using GitHub's RSS feed. This option is optional and can be used if preferred over the RSS feed method.
- `DryMode`: A boolean value that enables or disables dry run mode. When enabled, the tool checks for mod updates without installing them.
- `ResoniteModLoaderSource`: Allows you to change where `ResoniteModLoader.dll` and `0Harmony.dll` are updated from.
-  `manifest`: It let's you aet alternative manifest json url. It must match the ResoniteModLoader manifest schema.

### Usage

If a `settings.json` file is present in the same directory as the ResoniteModUpdater executable, the tool will automatically load the settings from this file. If you want to override these settings, you can do so by providing command line arguments.

For example, if you have a `settings.json` file that specifies a `ModsFolder` and `Token`, but you want to run the tool in dry run mode, you can do so with the following command:

```sh
ResoniteModUpdater -d
```

This will load the `ModsFolder` and `Token` from the `settings.json` file and enable dry run mode.
