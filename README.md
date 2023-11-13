# Resonite Mod Updater CLI

**ResoniteModUpdater** is a command-line tool that helps you update mods for Resonite. Updates only mods that have that have a github Link variable.
> [!WARNING]
> Does not work with monorepos with multiple mods nor with non github repositorys.

## Prerequisites
- Have .Net 7.0 Installed (SDK or just runtime)
  - Installer: https://dotnet.microsoft.com/en-us/download/dotnet/7.0
  - Winget: `winget install Microsoft.DotNet.Runtime.7`



## Installation

1. Download [ResoniteModUpdater_win_x64.zip](https://github.com/hazre/ResoniteModUpdater/releases/latest/download/ResoniteModUpdater_win_x64.zip) or [ResoniteModUpdater_linux_x64.zip](https://github.com/hazre/ResoniteModUpdater/releases/latest/download/ResoniteModUpdater_linux_x64.zip)
2. Extract it anywhere.
3. Double click to Start or Run from terminal.

## Usage

To use ResoniteModUpdater, you need to specify the mods folder path and, optionally, a GitHub authentication token.

> [!NOTE]
> If you have private mods or mods that you don't want to update. You can ignore mods by adding a `_` prefix to the mod's filename. 

```sh
ResoniteModUpdater [ModsFolder] [OPTIONS]
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

## Arguments

- `[ModsFolder]`: The path to the Resonite mods folder.

## Options

- `-h, --help`: Prints help information, providing usage instructions for the tool.
- `-v, --version`: Prints version information, displaying the version of ResoniteModUpdater.
- `-t, --token`: GitHub authentication token. Use this option only if you plan to run the command multiple times within a short period. The token helps bypass GitHub's request limits (60 Requests per hour).
