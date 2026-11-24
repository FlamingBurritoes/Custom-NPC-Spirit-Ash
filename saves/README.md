# Stock character builds

Use **Load build…** in Spirit Creator and choose a JSON file. The desktop release includes this folder. These are editable builds, not Elden Ring save files or ME3 launch profiles. Use **Save build…** to save your own copy.

| Folder | Builds | Contents |
|---|---:|---|
| `Spirit Ashes` | 75 | Player-model ashes, fixed Mimic Tear references, and inspired builds for ashes with separate models |
| `Cooperators` | 35 | Player-model NPCs with cooperator roles, including Rogier and Igon |
| `Invaders` | 42 | Player-model invaders and hostile encounter variants |
| `NPCs` | 156 | Other player-model NPCs, bosses, and outfit/loadout variants |
| `Model references` | 9 | Inspired builds for characters such as Melina, Blaidd, and Ranni who use separate models |

[index.tsv](index.tsv) lists every file, its source IDs, role(s), and any adaptations. Several encounters can share one build; different initializer rows are kept separate.

- **Stock-derived builds** use the original face row and supported equipment. The generator maps NPC-only weapons to player equivalents when possible, raises stats where the editor requires it, and notes omitted items.
- **Inspired builds** use ordinary equipment to suggest the character’s look and loadout. They do not recreate unique bodies, animations, multi-arm rigs, or special attacks. This is not a complete catalog of separate-model NPCs or enemies.
- All builds export as **friendly Spirit Ash replacements**. “Cooperator” and “Invader” identify the source character, not how the exported companion spawns or behaves. Supported Spirit Ash presets preselect their own Ash and spawn row; other presets start on Spirit Jellyfish Ashes. Mimic Tear references do not copy the player or replace the actual Mimic Tear Ash.

## Regenerate or check

From the repository root with .NET 10:

```powershell
dotnet run --project tools/SpiritCreator.Presets -c Release -- `
  "C:\path\to\ELDEN RING\Game\regulation.bin" `
  "C:\path\to\WitchyBND-exported-PARAM-XML" `
  "C:\path\to\entities.html" `
  tools/SpiritCreator.Presets/model-approximations.tsv `
  saves --check
```

Use stock Steam 1.17.3 and the matching `CharaInitParam.param.xml` and `Magic.param.xml`. The [Souls Modding entity list](https://soulsmods.github.io/data/er/entities.html) supplies map links; save it locally as `entities.html`. Remove `--check` to write the generated files instead of validating them. The editor does not require these generator inputs to load the shipped presets.
