# Resonite Mod Updater CLI

Updates mods that have that have a github Link variable.

> Does not work with monorepos with multiple mods nor with non github repositorys.

## Prerequisites
- Have .Net 7.0 Installed (SDK or just runtime)
  - Installer: https://dotnet.microsoft.com/en-us/download/dotnet/7.0
  - Winget: `winget install Microsoft.DotNet.Runtime.7`



## Installation

1. Download [ResoniteModUpdater.zip](https://github.com/hazre/Template/releases/latest/download/ResoniteModUpdater.zip)
2. Extract it anywhere.
3. Right-click in Explorer and click on Open in Terminal.
4. Run `.\ResoniteModUpdater.exe`

## Options

If you have private mods or mods that you don't want to update. You can ignore mods by adding a `_` prefix to the mod's filename. 

- `--path`: the path to resonite rml_mods folder, default is `C:\Program Files (x86)\Steam\steamapps\common\Resonite\rml_mods` (Optional) 
- `--bearerToken`: Your github auth token, to bypass 60 requests per hour limit. You shouldn't need this unless you run the it multiple times (Optional) 
