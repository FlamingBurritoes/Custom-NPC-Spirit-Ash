# Elden Ring Spirit Creator

## Release goal

A Windows desktop app lets a player select a character from an Elden Ring save, import their appearance, equip the resulting companion with vanilla gear, and choose a combat archetype. The player can see how the equipment looks together and give the companion's Spirit Ash a name and portrait. The app prepares an offline Mod Engine 3 profile. The player obtains and uses the ash through its existing in-game item and acquisition path.

Target the Steam PC version, one verified game patch, one replaceable ash, and one active companion. The app can keep multiple profiles on disk. The first public release targets two tested archetypes, Melee and Caster, with a defined list of supported weapons, skills, spells, and consumables. Expand that list as combinations pass testing. Show stock inventory icons for selected items. Exact character rendering in the editor, custom gear, multiple simultaneous companions, live item grants, and mid-session editing are out of scope.

## Milestones

| Milestone | Outcome | Done when |
|---|---|---|
| 0. Development setup | Private GitHub repo, OpenCode workspace, offline test profile | A clean checkout builds a minimal .NET app, and me3 launches Elden Ring with the separate test save |
| 1. Summon proof | One existing ash calls one friendly humanoid using a fixed vanilla loadout | The ally appears, equips the expected gear, targets enemies, performs at least one player-style action, and despawns correctly |
| 2. Appearance and architecture decision | Two imported appearances are applied and compared in-game | Two visibly different faces and body types reproduce consistently, and the chosen implementation is recorded |
| 3. Export and playable alpha | A profile edits and deploys a companion from the desktop app | A player changes equipment, exports, launches, and sees the change through normal ash use |
| 4. Combat and balancing | Two archetypes and basic stat tuning | Repeated tests show the intended differences and both presets handle their supported loadouts |
| 5. Ash presentation and equipment preview | A named ash with a portrait, plus stock inventory icons for selected items | The ash looks correct in inventory, and selecting different base-game and DLC gear updates the icons |
| 6. Release and maintenance | Versioned Windows package and instructions | A fresh Windows test setup can install, create a summon, play, switch profiles, and uninstall |

## 0. Set up GitHub, OpenCode, and the test game

1. Create a private GitHub repo with a README and a root `AGENTS.md`. Open the local checkout in OpenCode Desktop. Put milestone tasks in GitHub Issues and keep a short record of tested game versions and parameter mappings. OpenCode can work on the local checkout directly.
2. Install the .NET SDK and Avalonia templates. Install me3 and record the game patch and DLC availability. Launch the stock game once if needed to initialise a save. Back up the save and give the me3 test profile a dedicated save filename.
3. Keep saves, extracted vanilla assets, and generated game files outside Git. Commit project code, synthetic fixtures, and configuration examples. Use a dedicated output folder for your me3 package.
4. Treat Smithbox as an optional diagnostic tool. Use it to inspect rows or make a reference edit if that saves time. For a code-first workflow, use a small C# utility with a compatible SoulsFormats implementation and matching parameter definitions. Install Rustup and the x64 Windows MSVC toolchain only if the runtime path is selected.

**First OpenCode task** — “Prepare the repo and document the offline test setup. Keep the first prototype to one game patch, one replacement ash, and a fixed vanilla loadout. Record the source for each game parameter assumption and mark which ones need in-game verification.”

## 1. Prove the summon before building the editor

1. Pick an early-game ash already owned on the test save. Lone Wolf Ashes is a candidate. Verify that the replacement spawns a single ally across the ash levels you plan to support.
2. Use a known player-type humanoid as the starting point. Record the mapping from the ash item and summon trigger to the summon, NPC, AI, and character-initialisation rows. Give the companion its own rows where needed so shared NPC behaviour is easy to isolate.
3. Test with one obvious weapon and armour piece. Check ally targeting, melee attacks, dodge rolls or a weapon skill, and despawn on death or reload. Test on the original ash's normal summoning conditions.
4. Change the weapon to a visibly different one and repeat. Log the game patch, ash and upgrade level, relevant IDs, observed behaviour, and reproduction steps. Use those notes as the baseline for the exporter.

Spirit Battler is a precedent for replacing an ash and configuring humanoids. Its old output should be treated as reference material, with current mappings verified against your game files. [Spirit Battler source](https://github.com/kingborehaha/Elden-Ring-Spirit-Battler)

## 2. Import appearance and pick one game integration

1. Start with the stock character-creation appearance. Optionally read a selected save slot without writing to it. Capture body type and appearance in the profile so exporting or loading a build does not depend on the original save. Test two deliberately different characters.
2. Try mapping the imported values into the NPC appearance and character-initialisation data for the file-only summon. Compare both faces and bodies visually in-game, including without a helmet. Check which imported values round-trip reliably. Treat exact conversion as unverified until this passes.
3. If the file-only result meets the fidelity target, use the C# exporter for game data. If exact appearance is essential and direct conversion is unreliable, prototype a small Rust DLL loaded by me3. It reads the same profile and applies it to the intended summoned character. Evaluate the supported game patch and dependency terms before adopting `fromsoftware-rs`. ER Echo demonstrates a runtime path for appearance import.
4. Record the decision in the repo. Keep one shipped game integration. If the runtime path needs more investigation, use a named appearance preset for the internal alpha and keep exact import as a release gate.

[ER Echo](https://www.nexusmods.com/eldenring/mods/11724) · [Elden Ring Save Manager appearance documentation](https://elden-ring-save-manager.readthedocs.io/en/main/user-guide/appearance/) · [fromsoftware-rs](https://github.com/vswarte/fromsoftware-rs)

## 3. Build the app and export workflow

1. Define the versioned JSON profile. Include appearance, game content IDs for the loadout, archetype, supported stat overrides, and display data. Store IDs as the source of truth and names as UI labels. Keep game paths and save paths in local app settings.
2. Build the C# export command before connecting the Avalonia UI. Validate game version and selected item IDs. In the file-only path, generate the edited game data from the selected base. In the runtime path, export the profile for the DLL and any required static mod files. Test a second export and an interrupted or failed export. Write to a staging folder and preserve the previous usable output until validation passes.
3. Build the editor around one workflow. Import a character, select equipment, save a profile, export, and launch the offline profile. Start with weapon and armour. Add compatible Ashes of War, catalysts, spells, and items as the catalogue and AI support are verified.
4. Test with a missing game path, an invalid or unsupported save, an unavailable DLC item, and an unknown game patch. Include a useful error message for each. The player should be able to edit an existing profile without reimporting a save.

## 4. Tune and verify combat presets

Use working humanoid or Mimic-style behaviour as a baseline. Start with Melee and Caster. Define each by observable behaviour. Melee closes distance and favours weapon actions. Caster uses a compatible catalyst and attempts ranged spells when there is space, with a tested close-range fallback. Add HP and damage tuning first, then expose individual attributes only once their summon effects are verified.

Run repeated fights against a close-range enemy and an enemy that starts at range. Compare the presets on the same compatible build where possible. Log approaches, blocks or rolls, weapon skills, casts, idle behaviour, and failed attempts to use an action. Test at least a sword-and-shield build, a two-handed build, a staff build, and a seal build. Test a consumable before adding it to the supported list. Keep a short compatibility matrix and use it to drive the editor's warnings and defaults.

## 5. Add the ash name, portrait, and visual preview

1. Keep the donor item's ID and acquisition path. Export the new name and description through the game's text resources. Verify inventory, quick slots, and spirit tuning for the tested upgrade levels and language. Check whether any separate display text needs its own edit.
2. Offer a default icon and let the player import and crop a screenshot or PNG. Verify the small icon and detail view where applicable. Test one hand-made portrait end to end before automating texture packaging. Use the player's local installation as the source for required game assets.
3. Resolve item icon IDs from stock PARAMs and display the matching inventory tiles beside the item selectors. Leave entries without stock art blank.
4. Treat the portrait and inventory previews as independent features. Importing a portrait does not change the stock target thumbnail.

[Item icon replacement guide](https://www.nexusmods.com/eldenring/articles/747)

## 6. Package and release

- Tag an internal alpha after the editor can export a playable companion. Tag a beta after appearance import, combat presets, and ash presentation pass their tests. Use the first public release for the verified preview and supported catalogue.
- Build and test on Windows from a clean checkout. Automate profile parsing, validation, and export checks with synthetic fixtures. Keep a manual in-game checklist for visual matching, combat, ash upgrades, respawn, and uninstall.
- Package the app, launch instructions, supported game patch and DLC scope, and a troubleshooting guide. Explain how to switch back to an unmodded launch and remove the generated profile. Verify a fresh installation and confirm the normal save and original game files are intact.
- Document conflicts with mods that replace the same regulation, AI, text, or icon assets. Verify the same supported patch before each release. On a game update, rerun the summon and appearance checks before declaring compatibility.
- Optional reference tools and development notes belong in the repo alongside their source links. [me3](https://github.com/garyttierney/me3) · [OpenCode project instructions](https://opencode.ai/docs/instructions/) · [Avalonia setup](https://docs.avaloniaui.net/docs/get-started/) · [SoulsFormatsNEXT](https://github.com/soulsmods/SoulsFormatsNEXT)

## First three issues to open

1. **Verify offline launch and a separate test save.** Record the game version and verify that a mod file override loads.
2. **Replace one existing ash with a fixed-loadout humanoid.** Capture the row mappings and demonstrate a visible gear change and usable combat behaviour.
3. **Import two distinct appearances and compare them in-game.** Use the results to decide between a file-only exporter and the runtime integration before scaffolding the full editor.
