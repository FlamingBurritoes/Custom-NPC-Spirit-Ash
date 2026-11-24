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

## Repacking without Smithbox

1. Work from a copy of the installed `regulation.bin`. With WitchyBND, unpack the binder and convert only the target `.param` tables to XML in place. An omitted row field can use a default from the XML's `<fields>` section; it does not necessarily mean zero.
2. On this setup, WitchyBND **3.0.1.0** shipped an ER version ceiling of `1_16_1_1000L`. Unpacking worked, but repacking the binder failed with `RegulationOutOfBoundsException`. In a **temporary copy of WitchyBND**, set `Assets/Paramdex/ER/Upgrader/version.txt` to `1_17_3_0034L`. This only changes the ceiling; it does not update the schemas. Keep the actual binder `<version>` at `11730034`.
3. Repack the changed XML tables, then the binder folder. Keep `DCX_ZSTD`; compression level `15` built successfully and took less time than the original `21` setting. Reapply compatibility checks rather than assuming this workaround is valid on a different patch.
4. Unpack the result again before deploying. Compared with the in-game-tested baseline, both binders had 194 param tables and only `BuddyParam` and `CharaInitParam` differed. The Jellyfish initializer reference was the only changed `BuddyParam` field. `CharaInitParam` gained exactly one row, and all original rows were unchanged.

## Run or roll back

- Launch with `me3 launch --profile .\summon-test\summon-proof.me3`. It loads `summon-test/playable/regulation.bin` offline and uses `SpiritCreatorTest.sl2`. Fully quit and relaunch the game after swapping the regulation file.
- `summon-test/regulation.stormhawk.bin.bak` is the working pre-swap build. To restore it, copy it over `summon-test/playable/regulation.bin` with the game closed. Keep game data, saves, and generated files out of Git.

## Command-line alpha

- The [C# CLI](README.md) recreates the summon from the exact stock 1.17.3 regulation, not from an earlier mod. Save import creates versioned JSON with only the selected character's name, supported appearance fields, body type, and the default weapon ID. The JSON can be edited and exported again without reading the save.
- Export works in scratch space, adjusts the version ceiling only on its private WitchyBND copy, and checks the rebuilt regulation's table set and the values of all rows in the three changed tables before publishing. It refuses to overwrite an existing output folder.
- Generated **FISTS + Jawbone Axe** and **FISTS + Battle Axe** packages passed file verification and were reported working in-game. This validates the CLI import → edit → export workflow for the alpha. Weapon skill behavior and upgraded ashes are not yet verified.
