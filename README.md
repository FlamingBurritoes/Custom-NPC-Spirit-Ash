# Elden Ring Spirit Creator

An early Windows desktop app and command-line tool for turning a character's appearance into a friendly Spirit Jellyfish replacement. It exports a file-only, offline Mod Engine 3 profile. It does not edit the original save, replace Steam game files, or import the character's equipment or stats.

## Use the desktop app

On this checkout, double-click `artifacts\desktop-direct-fixed\SpiritCreator.Desktop.exe`. To build it from source, use `dotnet publish .\src\SpiritCreator.Desktop -c Release -o .\artifacts\desktop-direct-fixed`. Re-export any profiles from the earlier `desktop-direct` build; those files have an invalid compression header.

1. Click **Import save…**. The Windows file picker starts at `%APPDATA%\EldenRing`, one level above the account folder, if that directory exists. Open your folder and select a PC `.sl2` or `.sl2.bak` file. To use a backup elsewhere, browse to it instead.
2. Choose an active character by name and slot number. The app shows only the filename, not the full save path, and does not display or store the Steam ID.
3. Choose **Jawbone Axe** or **Battle Axe** from the weapon dropdown. This equips two of the selected axe. Set **HP (%)** and **Damage (%)** from 50 to 200; leave both at 100 for the default. These controls don't change AI, FP, or player attributes.
4. Click **Export offline profile**, then **Launch offline** after it finishes. Use the Jellyfish Ashes on the separate test save. Fully quit the game before switching exports.

The app writes the mod directly; no WitchyBND or Smithbox is needed. If auto-detection misses your game or Mod Engine 3 installation, expand **Tool paths** and select the stock `regulation.bin` or `me3.exe`. Exports go under `%LOCALAPPDATA%\SpiritCreator\Exports`.

## Play an exported CLI profile

From the project folder, launch a generated profile:

```powershell
me3 launch --profile .\artifacts\fists-jawbone\spirit.me3
```

The profile uses `SpiritCreatorTest.sl2`, not the normal save. Use the +0 Spirit Jellyfish Ashes where they normally work, then fully quit the game before switching to another profile. Inside `artifacts/`, profiles, executables, and generated game data are local-only and excluded from Git.

## Build and export

Building from source requires the [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0). Launching requires [me3](https://me3.help/). No external regulation editor or game archive extraction is needed.

```powershell
dotnet publish .\src\SpiritCreator.Cli -c Release -o .\artifacts\cli
New-Item -ItemType Directory -Force .\artifacts\profiles
$cli = '.\artifacts\cli\SpiritCreator.Cli.exe'
$stock = 'C:\Program Files (x86)\Steam\steamapps\common\ELDEN RING\Game\regulation.bin'

& $cli import --save '..\ER0000.sl2.bak' --character SUPPORT --profile .\artifacts\profiles\MySupport.json
& $cli export --profile .\artifacts\profiles\MySupport.json --regulation $stock --out .\artifacts\my-support
me3 launch --profile .\artifacts\my-support\spirit.me3
```

Adjust the paths for your installation, and use new names for the JSON file and output directory on repeat runs. Edit `weaponId` to switch both weapon slots between `14030000` (Jawbone Axe) and `14000000` (Battle Axe). Optional `hpPercent` and `damagePercent` fields both default to 100. No reimport is needed. The exporter reopens and checks the generated regulation before publishing it and never overwrites an existing export.

This alpha accepts only the verified stock Steam **1.17.3** regulation (`11730034`) and PC saves with character slot versions `210` or `211`. It uses the base appearance only; decals, face accessories, and quest-related appearance overrides are not supported. See [learnings.md](learnings.md) for the mapping and what still needs in-game verification.

## Run the automated checks

```powershell
dotnet run --project .\tests\SpiritCreator.Tests -c Release
```

The checks generate a synthetic save in memory; no real saves or assets are needed.
