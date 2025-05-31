# Resonite Mod Updater CLI

**ResoniteModUpdater** is a command-line tool that helps you update mods for Resonite. Updates only mods that have a GitHub Link variable.
> [!WARNING]
> Does not work with monorepos with multiple mods nor with non GitHub repositories.

## Prerequisites
- .NET 8.0 or higher is required
  - The Windows installer will automatically install .NET 9.0 if you don't have it

## Installation

### Windows
1. Download the Windows installer from the [latest release](https://github.com/hazre/ResoniteModUpdater/releases/latest)
2. Run the installer
   - The installer will create desktop and start menu shortcuts for ease of use
   - ResoniteModUpdater will be installed under `C:\Users\%UserProfile%\AppData\Local\ResoniteModUpdater`

### Linux
1. Download the AppImage from the [latest release](https://github.com/hazre/ResoniteModUpdater/releases/latest)
2. Make the AppImage executable and run it

## Usage

> [!NOTE]
> If you have private mods or mods that you don't want to update, you can ignore mods by adding a `_` prefix to the mod's filename. 

ResoniteModUpdater offers both an interactive mode and a CLI mode.

To start interactive mode, simply run:

```sh
ResoniteModUpdater
```

For CLI usage:

```sh
ResoniteModUpdater [OPTIONS] [COMMAND]
```

### Commands

- `update`: Updates resonite mods
- `search`: Searches for mods in the mod manifest

### Options

- `-h, --help`: Prints help information
- `-v, --version`: Display version in use

### Examples

1. Update Resonite mods:

```sh
ResoniteModUpdater update
```

2. Update Resonite mods with a specific mods folder:

```sh
ResoniteModUpdater update ~/.steam/steam/steamapps/common/Resonite/rml_mods
```

3. Update Resonite mods with a GitHub authentication token:

```sh
ResoniteModUpdater update ~/.steam/steam/steamapps/common/Resonite/rml_mods -token xxxxxxxxxxxxxx
```

4. Search for mods:

```sh
ResoniteModUpdater search example
```

### Update Command

```sh
ResoniteModUpdater update [ModsFolder] [OPTIONS]
```

#### Arguments

- `[ModsFolder]`: Path to resonite mods folder

#### Options

- `-h, --help`: Prints help information
- `-t, --token`: GitHub authentication token for using GitHub's official API. Optional, alternative to RSS feed method
- `-d, --dry`: Enables dry run mode. Checks for mod updates without installing them

### Search Command

```sh
ResoniteModUpdater search [QUERY] [OPTIONS]
```

#### Arguments

- `[QUERY]`: Query to search for in the mod manifest

#### Options

- `-h, --help`: Prints help information
- `-m, --manifest`: Set alternative manifest json url. It must match the RML manifest schema (Advanced)

## Settings File

The `settings.json` file is used to store the settings for the Resonite Mod Updater. This file is automatically created in the root of the ResoniteModUpdater installation directory when you choose to save your settings.

On Linux, the `settings.json` file is located either in `$XDG_CONFIG_HOME/ResoniteModUpdater` or `$HOME/.config/ResoniteModUpdater`.

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
- `manifest`: It lets you set alternative manifest json url. It must match the ResoniteModLoader manifest schema.

### Usage

If a `settings.json` file is present in the ResoniteModUpdater installation directory, the tool will automatically load the settings from this file. If you want to override these settings, you can do so by providing command line arguments.

For example, if you have a `settings.json` file that specifies a `ModsFolder` and `Token`, but you want to run the tool in dry run mode, you can do so with the following command:

```sh
ResoniteModUpdater update -d
```

This will load the `ModsFolder` and `Token` from the `settings.json` file and enable dry run mode.
