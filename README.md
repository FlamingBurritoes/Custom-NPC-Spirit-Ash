# Elden Ring Spirit Creator

An early Windows desktop app and command-line tool for turning a character's appearance into a friendly Spirit Jellyfish replacement. It exports a file-only, offline Mod Engine 3 profile. It does not edit the original save, replace Steam game files, or import the character's equipment or stats.

## Use the desktop app

On this checkout, double-click `artifacts\desktop\SpiritCreator.Desktop.exe`. To build it from source, use `dotnet publish .\src\SpiritCreator.Desktop -c Release -o .\artifacts\desktop`.

1. Click **Import save…**. The Windows file picker starts at `%APPDATA%\EldenRing`, one level above the account folder, if that directory exists. Open your folder and select a PC `.sl2` or `.sl2.bak` file. To use a backup elsewhere, browse to it instead.
2. Choose an active character by name and slot number. The app shows only the filename, not the full save path, and does not display or store the Steam ID.
3. Choose **Jawbone Axe** or **Battle Axe** from the weapon dropdown. This equips two of the selected axe.
4. Click **Export offline profile**, then **Launch offline** after it finishes. Use the Jellyfish Ashes on the separate test save. Fully quit the game before switching exports.

If a dependency is missing, expand **Tool paths** and select the stock `regulation.bin`, WitchyBND 3.0.1.0, or `me3.exe`. The app detects common locations and stores the generated profiles under `%LOCALAPPDATA%\SpiritCreator\Exports`.

## Play an exported CLI profile

From the project folder, launch a generated profile:

```powershell
me3 launch --profile .\artifacts\fists-jawbone\spirit.me3
```

The profile uses `SpiritCreatorTest.sl2`, not the normal save. Use the +0 Spirit Jellyfish Ashes where they normally work, then fully quit the game before switching to another profile. Inside `artifacts/`, profiles, executables, and generated game data are local-only and excluded from Git.

## Build and export

Building from source requires the [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0). Exporting requires the extracted Windows x64 release of [WitchyBND 3.0.1.0](https://github.com/ividyon/WitchyBND/releases/tag/3.0.1.0), including its assets. Launching requires [me3](https://me3.help/). No Smithbox or game archive extraction is needed.

```powershell
dotnet publish .\src\SpiritCreator.Cli -c Release -o .\artifacts\cli
New-Item -ItemType Directory -Force .\artifacts\profiles
$cli = '.\artifacts\cli\SpiritCreator.Cli.exe'
$stock = 'C:\Program Files (x86)\Steam\steamapps\common\ELDEN RING\Game\regulation.bin'
$witchy = '..\WitchyBND-3.0.1.0-win-x64\WitchyBND.exe'

& $cli import --save '..\ER0000.sl2.bak' --character SUPPORT --profile .\artifacts\profiles\MySupport.json
& $cli export --profile .\artifacts\profiles\MySupport.json --regulation $stock --witchy $witchy --out .\artifacts\my-support
me3 launch --profile .\artifacts\my-support\spirit.me3
```

Adjust the paths for your installation, and use new names for the JSON file and output directory on repeat runs. You can edit `weaponId` in the JSON to switch both weapon slots between `14030000` (Jawbone Axe, the starter loadout) and `14000000` (Battle Axe). No reimport is needed. Outputs are published only after the generated regulation is unpacked and checked; existing exports are not overwritten.

This alpha accepts only the verified stock Steam **1.17.3** regulation (`11730034`) and PC saves with character slot versions `210` or `211`. It uses the base appearance only; decals, face accessories, and quest-related appearance overrides are not supported. See [learnings.md](learnings.md) for the mapping and what still needs in-game verification.

## Run the automated checks

```powershell
dotnet run --project .\tests\SpiritCreator.Tests -c Release
```

The checks generate a synthetic save in memory; no real saves or assets are needed.
