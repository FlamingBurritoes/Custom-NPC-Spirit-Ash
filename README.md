# Elden Ring Spirit Creator

An early Windows desktop app and command-line tool for turning a character's appearance into a friendly Spirit Jellyfish replacement. It exports a file-only, offline Mod Engine 3 profile. It does not edit the original save, replace Steam game files, or import the character's equipment or stats.

## Use the desktop app

The stock 1.17.3 catalog includes base-game and DLC equipment. Search weapons or Ashes of War across both hands, armor across all four armor slots, and talismans or consumables across their respective slots. Sorceries and incantations have separate searches, shown for compatible right-hand weapons. Ranged weapons show their compatible ammunition and share one ammunition search.

On this checkout, double-click `artifacts\elden-ring-spirit-creator\SpiritCreator.Desktop.exe`. Close the app before rebuilding. Publish each update to the same folder:

```powershell
dotnet publish .\src\SpiritCreator.Desktop -c Release -o .\artifacts\elden-ring-spirit-creator
```

Re-export any profiles from the original `desktop-direct` build; those files have an invalid compression header.

1. Click **Import save…**. The Windows file picker starts at `%APPDATA%\EldenRing`, one level above the account folder, if that directory exists. Open your folder and select a PC `.sl2` or `.sl2.bak` file. To use a backup elsewhere, browse to it instead.
2. Choose an active character by name and slot number. The app shows only the filename, not the full save path, and does not display or store the Steam ID.
3. Choose a **Combat preset**. Switching presets does not change your gear. Click **Apply starter gear** to replace the two hand slots and reset their skills, affinities, and reinforcement without changing your attributes, armor, shade, or tuning.
4. Set your **Attributes** and **Equipment**. New builds start at level 1 with 10 in every attribute, a Club, and no armor. Each attribute point above 10 adds one level. Clear an attribute and press Enter or leave the field to reset it to 10. The two hand slots and the head, chest, hands, and legs slots are independent.
5. For each hand, choose an **Ash of War**, **Affinity (infusion)**, and **Reinforcement** separately. Affinity choices depend on the weapon and selected Ash. **Default weapon skill** keeps the weapon’s built-in skill. Regular weapons go from **+0 to +25**, and Somber weapons like Stormhawk Axe and Rivers of Blood go from **+0 to +10** without infusion options. Weapon reinforcement is independent of the Spirit Ash’s upgrade level. Export checks one-handed stat requirements, but equipping a skill does not guarantee the AI will use it.
6. Under **Equipment**, expand **Talismans and consumables** to set up to four passive talismans and three quick-item types. Quick items are empty by default. Talismans start with the two already inherited from Nepheli: Bull-Goat’s and Axe Talisman. Set a slot to **None** to clear it.
7. Pick a **Companion shade**: No shade (default), Spirit Ash (white), Invader (red), Puppet (blue), or Cooperator (gold). This is cosmetic only. Optionally set **HP (%)** and **Damage (%)** from 50 to 200. At 100, no extra reinforcement multiplier is applied. Click **Export offline profile**, then **Launch offline**. Use the Jellyfish Ashes on the separate test save. Fully quit the game before switching exports.

| Combat preset | What changes |
|---|---|
| **Default (Mimic-style)** | Uses your chosen loadout without a role-specific attack bias. Doesn't copy the player. |
| **Warrior** | Adds a melee attack bias and equips no spells. Starter gear is Club + Heater Shield. You can use another right-hand melee weapon and a non-catalyst offhand. |
| **Caster (Faith)** | Adds a spellcasting bias. Starter spells are Flame Sling and Catch Flame. Requires a right-hand incantation catalyst and an empty left hand. |
| **Sorcerer (Intelligence)** | Adds a spellcasting bias. Starter spell is Glintstone Pebble. Requires a right-hand sorcery catalyst and an empty left hand. |

Equip a right-hand **Glintstone Staff** for sorceries or **Finger Seal** for incantations, including in Default. Under **Equipment**, search the spell catalog, select a spell, and click **Add spell**. Select an equipped spell to remove it. The spirit has **seven spell slots**. Each catalyst keeps its own selection while you edit the build, and **Restore starter spells** resets just that list. Export checks spell requirements and catalyst compatibility.

**What gets cast?** The shared humanoid AI chooses an action group first, such as long-range casting, close-range casting, healing, or buffing. It then picks roughly evenly among eligible spells in that group. For example, Flame Sling and Lightning Spear compete in the long-range group; Catch Flame is picked on a different action. Slot order is not a priority setting. To make Lightning Spear the go-to ranged spell, make it the only one in that group. Actual casting frequency still needs in-game testing.

Caster favors spells but can still use melee actions. If you run a ranged-only loadout, adding a close-range spell such as Catch Flame gives the AI another option up close; it doesn't disable punches.

Quick items include the **NPC Crimson Flask**, Fire Pot, Kukri, Throwing Dagger, Boiled Crab, and Exalted Flesh. The AI decides when to use them; equipping one is not a command. The project uses the NPC healing flask, not the player one. Cerulean and Wondrous Physick are not supported. For a first test, equip only one Crimson Flask and watch for the drinking animation and an HP increase after the summon takes damage. For a passive effect, compare Crimson Amber Medallion against no talismans with the same HP tuning. The exported slots and quantities are verified; passive effects and item use still need in-game checks.

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
New-Item -ItemType Directory -Force .\artifacts\profiles
$cli = '.\src\SpiritCreator.Cli'
$stock = 'C:\Program Files (x86)\Steam\steamapps\common\ELDEN RING\Game\regulation.bin'

dotnet run --project $cli -c Release -- import --save '..\ER0000.sl2.bak' --character SUPPORT --profile .\artifacts\profiles\MySupport.json
dotnet run --project $cli -c Release -- export --profile .\artifacts\profiles\MySupport.json --regulation $stock --out .\artifacts\my-support
me3 launch --profile .\artifacts\my-support\spirit.me3
```

Adjust the paths for your installation, and use new names for the JSON file and output directory on repeat runs. Edit `weaponId` (right hand), `leftWeaponId`, `attributes`, and `armor` to customize a build. A gear ID of `-1` means empty. Set optional `rightAshOfWarId` and `leftAshOfWarId` to compatible gem IDs (for example, `50200` for Storm Stomp); omit them to keep the default skills. `hpPercent` and `damagePercent` default to 100. Older JSON files with no `attributes` or `armor` retain the legacy Nepheli setup, and a missing `leftWeaponId` mirrors the right hand. No reimport is needed. The exporter reopens and checks the generated regulation before publishing it and never overwrites an existing export.

Set `combatPreset` to `"Default"`, `"Warrior"`, `"Caster"`, or `"Sorcerer"`. Set gear explicitly in JSON: for Caster use `weaponId: 34000000` and `leftWeaponId: -1`; for Sorcerer use `weaponId: 33000000` and `leftWeaponId: -1`. Both work with all attributes at 10. If `combatPreset` is omitted, the exporter retains the original Nepheli think row for compatibility.

Set `spellIds` to a list of up to seven compatible IDs, e.g. `"spellIds": [6900, 6000]` for Lightning Spear and Catch Flame (requires 17 Faith). Omit it to use starter spells, or use `[]` for no spells on Default. Caster and Sorcerer require at least one spell.

Use base weapon IDs for `weaponId` and `leftWeaponId`. Set `rightAffinity` / `leftAffinity` to an affinity name such as `"Cold"` or `"FlameArt"` (default `"Standard"`), and `rightReinforcement` / `leftReinforcement` to a level (default `0`). These settings are independent for each hand.

Set optional `talismanIds` to up to four catalog IDs (e.g. `[1000, 4003]`), and `consumables` to up to three item stacks (e.g. `[{"id": 50201, "quantity": 1}]` for one NPC Crimson Flask). Omit those fields to retain the original Nepheli slots, or use `[]` to clear them. Consumables replace only the quick-item slots, not the source summon’s secondary slots.

This alpha accepts only the verified stock Steam **1.17.3** regulation (`11730034`) and PC saves with character slot versions `210` or `211`. It uses the base appearance only; decals, face accessories, and quest-related appearance overrides are not supported. See [learnings.md](learnings.md) for the mapping and what still needs in-game verification.

## Run the automated checks

```powershell
dotnet run --project .\tests\SpiritCreator.Tests -c Release
```

The checks generate a synthetic save in memory; no real saves or assets are needed. To also verify the preset exports, append `-- PATH_TO_STOCK_REGULATION_BIN` to the command.
