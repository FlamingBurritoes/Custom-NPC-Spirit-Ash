# Elden Ring Spirit Creator — Prototype Learnings

## Test baseline

- **Elden Ring 1.17.3**, regulation binder version **`11730034`**.
- **Confirmed in-game:** Spirit Jellyfish Ashes summoned Nepheli Loux Puppet through the offline Mod Engine 3 profile. No Smithbox, custom assets, or runtime DLL were needed.
- **Confirmed in-game by the tester:** the Jawbone Axe build works. Upgraded ashes and use of the new weapon skill have not been verified.
- **Verified in the files, pending in-game:** two vanilla appearance builds use different faces and body types while keeping the Jawbone Axe loadout and summon behavior settings.

## Summon and loadout mapping

| Table | Row | What matters |
|---|---:|---|
| `BuddyParam` | `23600000` | Spirit Jellyfish. Keep this row ID and `triggerSpEffectId=236000` so the original item still triggers the summon. |
| `BuddyParam` | `25900000` | Nepheli Loux Puppet. Copy its spawn settings to the Jellyfish row; leave the original Nepheli row alone. Its `npcParamId` and `npcThinkParamId` are both `100000020`. |
| `CharaInitParam` | `23342` | Nepheli's original initializer. Both main-hand slots use `14140004` (Stormhawk Axe +4). |
| `CharaInitParam` | `99000` | New initializer cloned from `23342`. Both `equip_Wep_Right` and `equip_Wep_Left` use `14030000` (Jawbone Axe +0). Only the Jellyfish row points here via `npcPlayerInitParamId`. |
| `EquipParamWeapon` | `14030000` | Vanilla Jawbone Axe. Same weapon type and motion category as the Stormhawk Axe; Nepheli's initializer stats meet its requirements. The weapon table itself is unchanged. |

Start from a single-spawn ash and an already-friendly humanoid. This isolates the loadout from AI and faction changes. Clone the initializer instead of editing a vanilla one shared with another summon. Select weapon IDs from the installed patch, and don't infer that a weapon skill works just because its weapon equips.

## Vanilla appearance check

| me3 profile (under `summon-test/`) | Vanilla reference | `npcPlayerFaceGenId` | `npcPlayerSex` |
|---|---|---:|---:|
| `appearance-roderika.me3` | Roderika, Spirit Tuner (`CharaInitParam` `23201`) | `23200` | `0` |
| `appearance-patches.me3` | Patches (`CharaInitParam` `23090`) | `23090` | `1` |

- Both overrides change only `CharaInitParam` row `99000`: the two appearance references and `equip_Helm=-1`. The face references resolve to existing `FaceParam` rows with different hair, face geometry, and body-scale settings. No vanilla rows were edited. Both repacked binders were reopened and compared against the working Jawbone Axe build.
- Launch one profile at a time and fully quit between tests. Both profiles reuse `SpiritCreatorTest.sl2` and leave `summon-proof.me3` unchanged. Use Jellyfish Ashes +0 in the same area, compare the head and silhouette, and check that both still use the Jawbone Axes. Roderika has visible hair; Patches should be bald. Armor and the spirit shader can obscure small differences.
- This tests vanilla NPC appearance assignment, not conversion from saves.

## Save appearance proof

- **Confirmed working in-game by the tester:** `appearance-fists.me3` and `appearance-support.me3` import the base appearances of two characters from a local PC save backup. Both use the same body type, but the body scales, hairstyles, and facial features differ. This pair does not test save conversion across body types. The backup's hash was unchanged.
- The [PC save layout reference](https://elden-ring-save-manager.readthedocs.io/en/stable/technical/save-file-structure/) explains the `ProfileSummary`, slot checksums, and variable-size blocks before `FaceData`. The prototype checks the summary and character checksums, matches slots by name, and verifies that the slot and summary agree on the appearance. It does not import stats, inventory, or progression.
- Each build adds a `FaceParam` row `99000` and points the Jellyfish initializer (`CharaInitParam` row `99000`) at it. Both keep the Jawbone Axes and omit the helmet. The rebuilt binders were reopened and compared against the Jawbone Axe build: only `CharaInitParam` and `FaceParam` changed, and existing vanilla rows were preserved.
- The experimental map copies 157 fields, including model choices, main face geometry, body scales, and colors. Unmapped texture/asymmetry fields retain template values; special eye overrides and nonzero accessory/decal models were not tested. The in-game result supports using a file-only exporter for the alpha, but exact likeness and import across both body types still need verification.

## Historical WitchyBND prototype

1. Work from a copy of the installed `regulation.bin`. With WitchyBND, unpack the binder and convert only the target `.param` tables to XML in place. An omitted row field can use a default from the XML's `<fields>` section; it does not necessarily mean zero.
2. On this setup, WitchyBND **3.0.1.0** shipped an ER version ceiling of `1_16_1_1000L`. Unpacking worked, but repacking the binder failed with `RegulationOutOfBoundsException`. In a **temporary copy of WitchyBND**, set `Assets/Paramdex/ER/Upgrader/version.txt` to `1_17_3_0034L`. This only changes the ceiling; it does not update the schemas. Keep the actual binder `<version>` at `11730034`.
3. Repack the changed XML tables, then the binder folder. Keep `DCX_ZSTD`; compression level `15` built successfully and took less time than the original `21` setting. Reapply compatibility checks rather than assuming this workaround is valid on a different patch.
4. Unpack the result again before deploying. Compared with the in-game-tested baseline, both binders had 194 param tables and only `BuddyParam` and `CharaInitParam` differed. The Jellyfish initializer reference was the only changed `BuddyParam` field. `CharaInitParam` gained exactly one row, and all original rows were unchanged.

## Run or roll back

- Launch with `me3 launch --profile .\summon-test\summon-proof.me3`. It loads `summon-test/playable/regulation.bin` offline and uses `SpiritCreatorTest.sl2`. Fully quit and relaunch the game after swapping the regulation file.
- `summon-test/regulation.stormhawk.bin.bak` is the working pre-swap build. To restore it, copy it over `summon-test/playable/regulation.bin` with the game closed. Keep game data, saves, and generated files out of Git.

## Command-line alpha

- The [C# CLI](README.md) recreates the summon from the exact stock 1.17.3 regulation, not from an earlier mod. Save import creates versioned JSON with only the selected character's name, supported appearance fields, body type, and the default weapon ID. The JSON can be edited and exported again without reading the save.
- Generated **FISTS + Jawbone Axe** and **FISTS + Battle Axe** packages passed file verification and were reported working in-game. This validates the CLI import → edit → export workflow for the alpha. Weapon skill behavior and upgraded ashes are not yet verified.

## In-app writer and combat tuning

- Spirit Battler's [read/edit/write workflow](https://github.com/kingborehaha/Elden-Ring-Spirit-Battler/blob/master/Elden%20Ring%20Spirit%20Battler/FormFuncs.cs#L269) inspired the in-app writer. This app uses its own narrow BND4/PARAM adapter, .NET AES, and ZstdSharp.Port, not SoulsFormatsNEXT or WitchyBND. The writer is pinned to the exact verified stock 1.17.3 file by SHA-256. It preserves the binder's metadata and untouched table bytes, then reopens the result before publishing.
- At 100% HP and damage, the direct FISTS + Jawbone Axe export matched all 194 PARAM files byte-for-byte against the in-game-proven WitchyBND export after independent unpacking.
- When tuning changes, the Jellyfish row points to private copies of Nepheli's +0 through +10 reinforcement effects. The clones keep the existing scaling curve and adjust `maxHpRate` and physical, magic, fire, lightning, and holy attack rates. The original Nepheli Puppet, AI, FP, and player attributes are untouched. The game schema calls the holy rate `darkAttackPowerRate`.
- Tuning has file-level verification; how the rates behave in combat and on upgraded ashes still needs in-game testing.
- The first direct writer produced the correct table bytes but overwrote the DCX compression-header length at byte 44 (the level belongs at byte 48), and Elden Ring crashed loading the resulting file. The fixed writer preserves the header length, uses the known-good 64 KiB Zstandard window without a frame content size, and matches the previous writer's DCX/AES padding. The fixed 100% export decompresses to the entire known-good binder byte-for-byte; game launch still needs a retest.

## Custom loadouts and attributes

- The tester reported the fixed direct writer working. New builds now start from Wretch values (vanilla `CharaInitParam` row `3009`): level 1, eight attributes at 10, right-hand Club, empty left hand, and no armor. We still use Nepheli's puppet initializer as the template and only override selected attributes, level, equipment, and appearance. Level counts points spent; it is not a separate scaling multiplier.
- The 30 weapon choices and 43 armor pieces were checked against the local 1.17.3 item tables. Export reads the selected weapons' actual one-handed requirements, validates armor categories and slot flags, and updates only the custom initializer. Not all items or combinations have been tried in-game, and we have not isolated how each attribute affects summon combat.
- FromSoftware uses reusable logic and battle goals plus separate `NpcThinkParam` settings. Nepheli, Dung Eater, Finger Maiden Therolina, and Jarwight puppets all point to `logicId=10000` and `battleGoalID=29999` in this patch. Matching those IDs is not proof of identical behavior: loadouts and other settings can differ. Meeting weapon requirements does not guarantee useful attacks, blocking, or skill use.

## Weapon skills, affinities, and reinforcement

- The desktop app filters the 14 initial Ashes by the weapon's mount category and then filters the affinities by the chosen gem's `configurableWepAttr00`–`12` mask. For example, Sacred Blade allows Standard, Heavy, Keen, Quality, Lightning, and Sacred. Empty hands, the supported catalysts, and Somber weapons have no non-Standard choices.
- An affinity variant uses the stock weapon ID plus `100 × affinity index`. With no selected Ash, `CharaInitParam` uses that variant ID plus the reinforcement level, matching stock humanoid initializer rows. With an Ash, `wepParamType_Right1` / `wepParamType_Left1` is `1`, and the private `EquipParamCustomWeapon` row stores the affinity variant ID, gem ID, and reinforcement level separately. No stock weapon, gem, skill, or reinforcement curve is rewritten.
- The exporter checks the variant's `originEquipWep` entry at the requested level and its reinforcement curve row. Automated checks cover all displayed weapon/affinity/level combinations against stock data, both hand references with custom Ashes, direct infusion without an Ash, and Somber and catalyst upgrades. These exports have not yet been tested in-game.
- `artifacts/fists-storm-stomp/spirit.me3` pairs a Club with Storm Stomp and a Heater Shield with No Skill. The export passed file checks and ME3 recognized the offline profile. In-game skill activation still needs testing: the equipped skill is not an AI command.

## Companion shade

- The blue puppet tint comes from `NpcParam` effect `295200` → VFX `57100` → phantom `201`. Puppet particles come from a separate effect (`295201`). Puppet mode keeps both, and the other modes omit those particles.
- White uses the vanilla Spirit Ash effect `295000` → phantom `200`. Red and gold use private copies of the shade effect and VFX with phantom `60` or `44`. No shade keeps the base spirit effect's non-visual fields and removes its shade VFX reference.
- Alternate shades use a private `NpcParam` row, while the stock Nepheli Puppet, friendly team, and AI reference remain unchanged. All five exports were independently unpacked and checked with stat tuning and an Ash of War enabled. In-game color and appearance still need a visual check.

## Earlier humanoid AI research — stock 1.17.3

Compared the decoded `NpcThinkParam` row bytes, excluding the row headers/IDs. These are our proposed preset names, not FromSoftware's. Matching a full think row establishes a shared AI configuration, not identical behavior with different equipment, effects, or encounter scripts.

| Candidate | Stock think rows | Evidence |
|---|---|---|
| **Spirit companion (default)** | Nepheli `100000020`, Dung Eater `100000030`, Jarwight `100000060` | Byte-identical. `logicId=10000`, `battleGoalID=29999`, `isBuddyAI=1`. The Nepheli version already works in our summon. |
| **Early-engaging spirit** | Fire Knight Queelign `150000002` | Same logic/goal and buddy flag, but `BattleStartDist=21` versus Nepheli's `2`, and `eye_dist=30` versus `2`. Other DLC buddy variants share the non-extended settings. Prototype the detection changes on a private copy of Nepheli's row first. |
| **Cooperator-style** | Nepheli `533340000`, Rogier `533250000`, Tragoth `533440000` | Byte-identical. `30000/29999`, `isBuddyAI=0`. Hornsent `534140000` and Ansbach `534160000` are a separate identical cooperator pair with longer pursuit/target memory. Igon is an exception that uses `10000`. |
| **Invader-style** | Nerijus `543430000`, Bernahl `543260000`, Festering Vyke `523040000` | Byte-identical. `31000/29999`, `isBuddyAI=0`. Alberich `543850000` is a close variant, not an exact match. Some hostile encounters use `10000` instead, so this is a common invader configuration, not a universal one. |

- **Caster, Archer, and Thrower are separate loadout experiments.** Mimic `100000010`, Therolina `100000040`, and Dolores `100000050` have byte-identical think rows despite different equipment. That group differs from Nepheli's by one field, `idAttackCannotMove`. Therolina's vanilla seal/incantation setup and Dolores's bow/ammunition are better starting references than promising that a think-row swap alone makes the companion cast or shoot. Jarwight shares Nepheli's exact think row and has a throwable in its initializer.
- These think-row families are not the shipped combat presets. Engagement distances are separate from attack choice, and co-op and invasion logic should not be assumed to preserve spirit follow behavior.

## Combat presets — attack selection

- The community-decompiled [shared battle goal `29999`](https://raw.githubusercontent.com/zoloypzuo/EldenRingAI/master/aicommon/029999_battle.dec.lua) selects base weights using weapon and spell type and adds stock weights when specific effects are present. The script is a reference, not a byte-for-byte verification against the installed game. The effect IDs and referenced probability rows were checked against the stock 1.17.3 regulation.
- All explicit presets reference Mimic's buddy think row `100000010`. Default adds no role effect. Warrior uses Tragoth's `18657` → `15043` (adds weight to the first three right-hand melee actions). Caster uses Wilhelm's `18663` → `15049` (including weight for a ranged cast); Sorcerer uses Sellen's `18667` → `15053` (including weights for spell actions). These are additive weights, not commands or casting percentages.
- The role effect goes into `NpcParam.spEffectID23` (offset 516) of the private friendly NPC. The export keeps the puppet's team, spirit effects, and follow configuration. Stock think, probability, magic, and weapon tables are untouched. Old profiles that omit the preset still use the Nepheli think row.
- The Finger Seal and Glintstone Staff equip selected stock spells in the private initializer at offsets 96–120. Export checks spell requirements, cast family and AI usage IDs along with catalyst flags. The stock NPC infinite-FP effect is preserved; this feature doesn't tune FP.
- **In-game report:** The tester confirmed Caster casts spells, but also saw a brief bout of apparent left-hand punches. The shared goal still has melee actions (including left-hand attacks), and the catalyst's melee action can also look like a punch. The exact action hasn't been identified; no AI weights were changed in response. If it recurs, record the spell loadout and whether it happens only in melee range.
- **Controlled comparison:** Use the same summon-enabled encounter, armor, attributes, shade, and HP/damage for both Warrior and Caster. To test the weighting independently of equipment, compare Caster against Default using the same Finger Seal, spells, and empty offhand. Casting frequency is not yet verified.

## Custom spell loadouts

- The expanded catalogs supersede the initial curated lists below. `tools/generate_catalog.py` rebuilds the embedded TSV data from decoded stock 1.17.3 PARAMs and their WitchyBND XML names. The running app does not need either the generator or the unpacked game data. The catalog excludes test/NPC-only rows and duplicate variants. It includes player spells even when the shared AI has no known action for them; equipping a spell does not guarantee it is cast.

- The shared goal scans spell indexes `0–6`, matching `CharaInitParam.equip_Spell_01` through `_07`. Vanilla initializers can store multi-memory-slot player spells in consecutive entries, so the limit here is seven spell entries, not a player memory-slot budget.
- `CheckMagicType` counts offensive AI usage groups `10`, `11`, and `12` when selecting the main-hand base weights. Context and preset effects modify action weights. When an action requests a spell group, `GetRandomSpellId_ByType` collects eligible matching slots and rolls 1–100 across equal-sized intervals; any slot-order difference is only rounding, not a deliberate priority. `CanUseSpell` checks the catalyst and conditional rules for buffs. The UI labels these groups by typical range or purpose, not a guaranteed range or cast rate.
- The picker exposes 41 sorceries and 53 incantations from stock, including DLC selections. Classification uses the game row's cast type rather than community name tags or ID ranges (e.g. Night Maiden's Mist is a sorcery, Death Lightning an incantation). The tests verify each catalog entry and read back custom, starter, and empty loadouts without changing the shared magic or AI tables.

## Talismans and quick items

- New exports default to four empty talisman slots even when the source initializer had equipment. Explicit talismans are still supported.

- Stock humanoid NPC initializers use `equip_Accessory01` through `_04` at offsets 64–76. Nepheli Puppet starts with Bull-Goat’s `1210` and Axe `2130`; Dung Eater Puppet equips three talismans. The export uses normal equipment slots and validates each stock accessory’s passive-effect reference. It does not force a separate NPC passive effect or assume all conditional bonuses behave the same on a summon.
- Quick items use `item_01` through `_10` at offsets 124–160 with counts at 204–213. The editor supports up to three types, while the export clears unused quick slots and preserves the template's secondary items (including three Talisman Pouches).
- Reference goal `29999` scans quick and secondary item slots, filters by `EquipParamGoods.aiUseJudgeId`, and picks eligible items for a throw, heal, or buff action. Ordinary player Crimson/Cerulean flask rows lack this usage ID. The NPC Crimson Flask `50201` is used by vanilla Patches and Nepheli's co-op initializer; it points to `19391`, a stock 30%-of-max-HP heal. The reference action weights suppress that healing action at or above half HP, but a lower HP value does not guarantee a use. Cerulean is also unnecessary with the template's infinite-FP effect.
- Item and talisman edits are private to the custom initializer. Automated readback checked IDs, stack quantities, empty slots, and that the shared AI, accessory, and goods tables remain unchanged. In-game item use, actual consumption, and passive effects are still unverified for our summon.

## Summon name and portrait

- For a custom name, the exporter reads the three English item message bundles directly from the stock Game directory's `Data0` archive. Each bundle has a base `GoodsName` table containing Jellyfish IDs `236000` through `236010`. The export replaces those entries, preserving the upgrade suffix, and adds a private entry in `NpcName`. Only the cloned `NpcParam` row uses that name ID. It does not touch the vanilla Nepheli Puppet, installed assets, or the Ash item ID, icon, or description.
- All eleven names and the private NPC name were checked in each rebuilt bundle. In-game text display still needs a check, particularly the name that appears with the summoned NPC's health bar.
- Stock `EquipParamGoods` uses icon ID `111` at all Jellyfish upgrade levels. Optional PNG/JPEG import center-crops to 160 × 160 and replaces just the BC7 blocks for that icon in `SB_Icon_00` in the stock high-detail common texture archive. The separate `MENU_Knowledge_00111` item-detail image remains stock. Disabled portrait exports contain no texture override.
- The [stock spirit HP display](https://docs.rs/eldenring/latest/src/eldenring/cs/fe_man.rs.html) uses a different HUD data structure from friendly overhead tags and does not have a name field. The private `NpcParam.nameId` link is not enough to add a HUD label. Neither HUD assets nor team behavior are changed for this feature.

## Selecting a replacement target

- `AshTargets.tsv` indexes 83 base-game and DLC ashes with 151 selectable `BuddyParam` rows. The stock goods row's effect reference, not its item ID or a guessed row number, links it to the spawn rows. This matters for DLC ashes and for Jolán and Anna, whose rows have different leading numbers. Mimic Tear's special copy flow is excluded.
- Export retains the selected row ID and trigger, uses the existing humanoid puppet template for spawning and reinforcement, and retains the stock position and facing for non-Jellyfish targets. It leaves all other Buddy rows and the item costs and upgrade paths untouched.
- Giant Rat Ashes use separate trigger families at +0–+4, +5–+9, and +10. Rows are labeled with their level range in the editor. Rat icons also vary with the upgrade tier. Portrait export patches each stock icon used by the selected item; text export renames the selected item in the base or DLC name table as appropriate.

## Inventory images

- The editor looks up stock icon IDs in each item's PARAM table, then crops the corresponding inventory tile from the verified Data0 UI atlas. Spell art comes from the matching `EquipParamGoods` row, not the `Magic` row. Armor uses the first available of the two stock icon variants.
- The app retains only the compressed tiles for catalog entries and decodes selected images on demand. Entries without stock artwork stay blank. No icons are written to disk or added to exports. `--icons` exercises the stock lookup and BC7 readback.

## Optional appearance

- The stock Wretch character-creation preview rows (`CharaInitParam` 3118 and 3119) point to `FaceParam` rows of the same IDs. Those faces are byte-identical to the Warrior base templates 1000 and 2000, respectively. Character creation uses these same faces across the class previews. In this PARAM, Type A is `1` and Type B is `0`.
- With no imported face, export links to the verified stock row and leaves `FaceParam` untouched. Selecting a save slot captures the supported appearance immediately. Later exports use that snapshot without reading the save again; the default and imported appearance both fit in the existing JSON profile schema.

## Build files and overhead nameplates

- Desktop build JSON stores a versioned editable profile, quick-item slot positions, and alternate catalyst spell lists. It is not an ME3 launch profile. Loading does not read the original save, and missing spawn selection or unmet combat prerequisites do not stop draft saves. Export still validates the actual loadout.

## Preset library

- The preset generator enumerates stock map-linked player-model characters plus named NPC and Spirit Ash initializers. The index reports each row, role, substitution, and omission. Separate-model NPCs and humanoid ashes use a curated equipment manifest, not their original rigs or movesets.
- Presets link to stock `FaceParam` rows directly. This preserves the original stock data without embedding faces in the JSON or restricting the game’s stock faces to the save importer’s field subset.
- Portrait import and export were removed. The inventory atlas is only read for stock thumbnails. Version 1 build files with old portrait data still load, but that field is dropped on the next save.

## Spirit outgoing damage and health display

- Nepheli Puppet's `NpcParam` uses special effect `296000` in slot 27. It sets ten outgoing damage multipliers (physical, magic, fire, lightning, and holy, each against player and enemy targets) to 0.25. Its incoming damage modifiers (0.5) and poise damage modifier (0.05) are separate. The editor copies the effect for the custom summon and only replaces the outgoing multipliers. The existing HP and attack-power adjustments still act on the Ash reinforcement effects.
- The stock Spirit Ash HUD has HP values and a character handle, but is separate from the cooperator tag that tracks an on-screen position. No file-only, per-spirit option to force a persistent overhead bar was verified. No HUD or team changes are exported.
- Rogier’s Margit summon is a map entity activated by a gold NPC summon sign. That flow supplies a name ID to the cooperator system, which populates the friendly overhead tag and handles line of sight. The Ashes workflow uses `BuddyParam` and the separate HP-only spirit display. The current export supplies `NpcParam.nameId` and English text but does not register a cooperator; no file-only method was verified to add a friendly tag while retaining the Ash workflow. Team changes were not shipped as a cosmetic workaround.
