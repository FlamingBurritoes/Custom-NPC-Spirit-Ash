using System.Buffers.Binary;
using System.Security.Cryptography;
using static SpiritCreator.RegulationBinary;

namespace SpiritCreator;

public static class RegulationExporter
{
    internal const string StockHash = "B8CDAB84C0772F66811FDC5176A381877CA6657B37D420CC2C1562A90D63870E";
    internal const int StockSize = 2045728;
    private const int Nepheli = 25900000, CustomRow = 99000;
    private const int EffectStart = 900000, StockEffectStart = 290050;
    private const int CustomNpc = 100000021, ShadeEffect = 900011, ShadeVfx = 900000;
    private const int StockSpiritDamageEffect = 296000, SpiritDamageEffect = 900012, SpiritDamageSlot = 532;
    private const int MimicThink = 100000010;
    private const int RightCustomWeapon = 900000, LeftCustomWeapon = 900001;
    private static readonly int[] DamageRates = [72, 76, 80, 84, 480];

    public static string Export(SpiritProfile profile, string regulation, string output)
    {
        profile.Validate();
        output = Path.TrimEndingDirectorySeparator(Path.GetFullPath(output));
        var parent = Path.GetDirectoryName(output) ?? throw new ArgumentException("Choose a new output folder, not a drive root.");
        if (Directory.Exists(output) || File.Exists(output))
            throw new IOException($"Output already exists: {output}. Use a new folder.");
        byte[] stock;
        using (var input = File.OpenRead(regulation))
        {
            Require(input.Length == StockSize, "Select the verified stock Steam 1.17.3 regulation.bin (11730034).");
            stock = new byte[StockSize];
            input.ReadExactly(stock);
        }
        Require(Convert.ToHexString(SHA256.HashData(stock)) == StockHash,
            "Unsupported or modified regulation.bin. Select the stock Steam 1.17.3 file (11730034).");
        var original = RegulationBinary.Read(stock);
        var gameFolder = Path.GetDirectoryName(Path.GetFullPath(regulation))!;
        var (target, spawn) = AshTargetCatalog.Resolve(profile.TargetAshId, profile.TargetBuddyRowId);
        var name = string.IsNullOrWhiteSpace(profile.SummonName) ? null : profile.SummonName.Trim();
        var messages = name is null ? null :
            SummonMessageSource.Rename(gameFolder, name, target.GoodsId);

        var buddy = new RegulationParam(original.Table("BuddyParam"), "BUDDY_PARAM_ST", 160);
        var characters = new RegulationParam(original.Table("CharaInitParam"), "CHARACTER_INIT_PARAM", 320);
        var faces = new RegulationParam(original.Table("FaceParam"), "FACE_PARAM_ST", 240);
        var weapons = new RegulationParam(original.Table("EquipParamWeapon"), "EQUIP_PARAM_WEAPON_ST", 664);
        var init = characters.Row(23342).ToArray();
        if (profile.Attributes is { } attributes)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(init.AsSpan(192), (ushort)attributes.Level);
            var values = attributes.Values;
            for (var index = 0; index < values.Length; index++)
                init[194 + index] = (byte)values[index];
        }
        var effective = new NpcAttributes(init[194], init[195], init[196], init[197], init[198], init[199], init[200], init[201]);
        var leftWeapon = profile.LeftWeaponId ?? profile.WeaponId;
        var reinforcement = new RegulationParam(original.Table("ReinforceParamWeapon"), "REINFORCE_PARAM_WEAPON_ST", 128);
        var rightVariant = CheckWeapon(weapons, reinforcement, profile.WeaponId, profile.RightAffinity,
            profile.RightReinforcement, effective, "Right hand");
        var leftVariant = CheckWeapon(weapons, reinforcement, leftWeapon, profile.LeftAffinity,
            profile.LeftReinforcement, effective, "Left hand");
        WriteSpells(original, init, profile, effective);
        WriteExtras(original, init, profile);
        var rightAmmo = AmmunitionCatalog.Resolve(profile.WeaponId, profile.RightAmmoId);
        var leftAmmo = AmmunitionCatalog.Resolve(leftWeapon, profile.LeftAmmoId);
        void EquipAmmo(AmmoChoice? ammo, bool secondary)
        {
            if (ammo is null)
                return;
            Require(BinaryPrimitives.ReadUInt16LittleEndian(weapons.Row(ammo.Id)[422..]) == ammo.Type,
                $"Unexpected stock ammunition type for {ammo.Name}.");
            var bolt = ammo.Type is 85 or 86;
            var index = (secondary ? 2 : 0) + (bolt ? 1 : 0);
            Write32(init, 48 + 4 * index, ammo.Id);
            BinaryPrimitives.WriteUInt16LittleEndian(init.AsSpan(178 + index * 2), 99);
        }
        EquipAmmo(rightAmmo, false);
        EquipAmmo(leftAmmo, rightAmmo is not null && (rightAmmo.Type is 85 or 86) == (leftAmmo?.Type is 85 or 86));
        if (profile.Armor is { } armor)
        {
            var protectors = new RegulationParam(original.Table("EquipParamProtector"), "EQUIP_PARAM_PROTECTOR_ST", 416);
            var ids = armor.Ids;
            for (var slot = 0; slot < ids.Length; slot++)
            {
                var id = ids[slot];
                if (id != EquipmentCatalog.Empty)
                {
                    var protector = protectors.Row(id);
                    Require(protector[214] == slot && (protector[220] & (1 << (slot + 1))) != 0,
                        $"Armor ID {id} does not fit the {(ArmorSlot)slot} slot.");
                }
                Write32(init, 32 + slot * 4, id);
            }
        }
        else
        {
            // Legacy profiles kept Nepheli's body pieces and removed only the headgear.
            Write32(init, 32, EquipmentCatalog.Empty);
        }
        Require(!characters.Contains(CustomRow) && !faces.Contains(CustomRow) &&
                !buddy.Ids.Any(id => I32(buddy.Row(id), 84) == CustomRow) &&
                !characters.Ids.Any(id => I32(characters.Row(id), 164) == CustomRow),
            "A stock row already uses or references the alpha's custom ID.");

        var summon = buddy.Row(Nepheli).ToArray();
        var targetRow = buddy.Row(spawn.RowId);
        var goods = new RegulationParam(original.Table("EquipParamGoods"), "EQUIP_PARAM_GOODS_ST", 176);
        var ashItem = goods.Row(target.GoodsId);
        Require(ashItem[62] is 7 or 8 && ashItem[63] == 2 &&
                BinaryPrimitives.ReadUInt16LittleEndian(ashItem[48..]) == target.IconId &&
                I32(targetRow, 4) == spawn.TriggerEffectId &&
                (I32(ashItem, 4) == spawn.TriggerEffectId || target.GoodsId == 233000 && spawn.TriggerEffectId is 233100 or 233200) &&
                I32(summon, 8) == 100000020 &&
                I32(summon, 12) == 100000020 && I32(summon, 84) == 23342,
            "The stock Spirit Ash or humanoid template mapping did not match.");
        Write32(summon, 4, spawn.TriggerEffectId);
        // Keep the selected member's position and facing in its group; leave the original
        // Jellyfish conversion unchanged for existing users.
        if (target.GoodsId != AshTargetCatalog.DefaultGoodsId)
            targetRow.Slice(24, 12).CopyTo(summon.AsSpan(24));
        Write32(summon, 84, CustomRow);
        if (profile.CombatPreset is not null)
        {
            // Use the stock generalist think row, not Mimic's initializer or summon effects.
            var think = new RegulationParam(original.Table("NpcThinkParam"), "NPC_THINK_PARAM_ST", 228);
            var mimic = think.Row(MimicThink);
            Require(I32(mimic, 4) == 10000 && I32(mimic, 8) == 29999, "Unexpected stock Mimic AI.");
            Write32(summon, 12, MimicThink);
        }
        Write32(init, 16, rightVariant < 0 ? rightVariant : rightVariant + profile.RightReinforcement);
        Write32(init, 24, leftVariant < 0 ? leftVariant : leftVariant + profile.LeftReinforcement);
        init[228] = profile.BodyType;
        var importedFace = profile.Face;
        byte[]? face = null;
        if (profile.StockFaceId is { } stockFace)
        {
            Require(faces.Contains(stockFace), $"Stock face {stockFace} is unavailable.");
            Write32(init, 164, stockFace);
        }
        else if (importedFace is null)
        {
            // Stock Wretch character-creation preview initializers. Their faces are
            // byte-identical to the Warrior base templates (1000 / 2000).
            var starterId = profile.BodyType == 1 ? 3118 : 3119;
            var starter = characters.Row(starterId);
            Require(starter[228] == profile.BodyType && I32(starter, 164) == starterId &&
                faces.Row(starterId).SequenceEqual(faces.Row(profile.BodyType == 1 ? 1000 : 2000)),
                "Unexpected stock character-creation appearance.");
            Write32(init, 164, starterId);
        }
        else
        {
            Write32(init, 164, CustomRow);
            face = faces.Row(1).ToArray();
            foreach (var (key, value) in importedFace)
                face[FaceMapping.ParamOffset(FaceMapping.Offsets[key])] = value;
        }

        var replacements = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        var checks = new List<Action<RegulationBinary>>();
        void Replace(string name, string type, int size, RegulationParam table, Dictionary<int, byte[]> rows)
        {
            replacements.Add(name, table.Write(rows));
            checks.Add(binder => table.Verify(new RegulationParam(binder.Table(name), type, size), rows));
        }

        if (profile.RightAshOfWarId is not null || profile.LeftAshOfWarId is not null)
        {
            var custom = new RegulationParam(original.Table("EquipParamCustomWeapon"), "EQUIP_PARAM_CUSTOM_WEAPON_ST", 16);
            var gems = new RegulationParam(original.Table("EquipParamGem"), "EQUIP_PARAM_GEM_ST", 96);
            var skills = new RegulationParam(original.Table("SwordArtsParam"), "SWORD_ARTS_PARAM_ST", 32);
            var rows = new Dictionary<int, byte[]>();
            void Apply(int? gemId, int weaponId, int variantId, int level, WeaponAffinity affinity,
                int customId, int weaponOffset, int typeOffset)
            {
                if (gemId is not { } id)
                    return;
                Require(!custom.Contains(customId) && !characters.Ids.Any(row =>
                            UsesCustomWeapon(characters.Row(row), customId)),
                    "A stock row already uses or references the companion's custom weapon ID.");
                var weapon = weapons.Row(weaponId);
                var ash = AshOfWarCatalog.Resolve(weaponId, id);
                var gem = gems.Row(id);
                var mount = AshOfWarCatalog.Mount(weaponId)!.Value;
                Require(weapon[577] == 2 && BinaryPrimitives.ReadUInt16LittleEndian(weapon[422..]) == mount.WeaponType &&
                        I32(gem, 24) == ash.SkillId && skills.Contains(ash.SkillId) &&
                        (BinaryPrimitives.ReadUInt16LittleEndian(gem[48..]) & (1 << (int)affinity)) != 0 &&
                        gem.Slice(56, 6).SequenceEqual(ash.MountMask) &&
                        AshOfWarCatalog.Allows(gem.Slice(56, 6), mount.Bit),
                    $"The stock game does not allow {ash.Choice.Name} on this weapon with {affinity} affinity.");
                // Stock custom weapons store the affinity row and reinforcement level separately.
                var row = new byte[16];
                Write32(row, 0, variantId);
                Write32(row, 4, id);
                row[8] = (byte)level;
                rows.Add(customId, row);
                Write32(init, weaponOffset, customId);
                init[typeOffset] = 1;
            }

            Apply(profile.RightAshOfWarId, profile.WeaponId, rightVariant, profile.RightReinforcement,
                profile.RightAffinity, RightCustomWeapon, 16, 232);
            Apply(profile.LeftAshOfWarId, leftWeapon, leftVariant, profile.LeftReinforcement,
                profile.LeftAffinity, LeftCustomWeapon, 24, 235);
            Replace("EquipParamCustomWeapon", "EQUIP_PARAM_CUSTOM_WEAPON_ST", 16, custom, rows);
        }

        var effects = new RegulationParam(original.Table("SpEffectParam"), "SP_EFFECT_PARAM_ST", 912);
        var effectRows = new Dictionary<int, byte[]>();
        var bias = CombatPresetChoice.BiasEffect(profile.CombatPreset);
        var outgoing = profile.OutgoingDamagePercent ?? SpiritProfile.StockOutgoingDamagePercent;
        var adjustSpiritDamage = outgoing != SpiritProfile.StockOutgoingDamagePercent;
        if (profile.Shade != CompanionShade.Puppet || bias is not null || name is not null || adjustSpiritDamage)
        {
            const int puppetColor = 295200, puppetParticles = 295201, whiteColor = 295000;
            const int colorSlot = 528, particlesSlot = 520, biasSlot = 516, primaryVfx = 368;
            var npcs = new RegulationParam(original.Table("NpcParam"), "NPC_PARAM_ST", 736);
            var npc = npcs.Row(I32(summon, 8)).ToArray();
            Require(npc[307] == 47 && I32(npc, 468) == -1 && I32(npc, colorSlot) == puppetColor &&
                    I32(npc, particlesSlot) == puppetParticles && !npcs.Contains(CustomNpc) &&
                    !buddy.Ids.Any(id => I32(buddy.Row(id), 8) == CustomNpc ||
                                         I32(buddy.Row(id), 16) == CustomNpc),
                "Unexpected stock puppet NPC or custom NPC ID collision.");
            if (name is not null)
            {
                Require(!npcs.Ids.Any(id => I32(npcs.Row(id), 12) == SummonMessages.NpcNameId),
                    "The private name ID is already used by a stock NPC.");
                Write32(npc, 12, SummonMessages.NpcNameId);
            }
            if (bias is { } effectId)
            {
                Require(I32(npc, biasSlot) == -1 && effects.Contains(effectId),
                    "Unexpected stock AI bias effect or occupied NPC effect slot.");
                Write32(npc, biasSlot, effectId);
            }
            if (adjustSpiritDamage)
            {
                Require(I32(npc, SpiritDamageSlot) == StockSpiritDamageEffect && !effects.Contains(SpiritDamageEffect) &&
                        !npcs.Ids.Any(id => I32(npcs.Row(id), SpiritDamageSlot) == SpiritDamageEffect),
                    "Unexpected stock spirit damage effect or private row collision.");
                var adjustment = effects.Row(StockSpiritDamageEffect).ToArray();
                // Five outgoing damage channels each for player and enemy targets.
                // Keep the incoming damage reduction and poise penalty on the same row.
                for (var offset = 652; offset <= 688; offset += sizeof(float))
                {
                    var field = adjustment.AsSpan(offset, sizeof(float));
                    Require(BinaryPrimitives.ReadSingleLittleEndian(field) == 0.25f, "Unexpected stock outgoing damage rate.");
                    BinaryPrimitives.WriteSingleLittleEndian(field, outgoing / 100f);
                }
                effectRows.Add(SpiritDamageEffect, adjustment);
                Write32(npc, SpiritDamageSlot, SpiritDamageEffect);
            }
            // Puppet particles do not provide the shade, but should not carry over to non-puppet looks.
            if (profile.Shade != CompanionShade.Puppet)
                Write32(npc, particlesSlot, -1);
            if (profile.Shade == CompanionShade.SpiritAsh)
            {
                Require(I32(effects.Row(whiteColor), primaryVfx) == 57000, "Unexpected stock spirit ash color.");
                Write32(npc, colorSlot, whiteColor);
            }
            else if (profile.Shade != CompanionShade.Puppet)
            {
                Require(!effects.Contains(ShadeEffect), "The private shade effect ID is already in use.");
                var effect = effects.Row(puppetColor).ToArray();
                Require(I32(effect, primaryVfx) == 57100 &&
                        Enumerable.Range(0, 7).All(slot => I32(effect, 396 + 4 * slot) == -1),
                    "Unexpected stock puppet color effect.");
                if (profile.Shade == CompanionShade.None)
                {
                    // Keep the stock spirit state and remove only its rendering reference.
                    Write32(effect, primaryVfx, -1);
                }
                else
                {
                    var vfx = new RegulationParam(original.Table("SpEffectVfxParam"), "SP_EFFECT_VFX_PARAM_ST", 164);
                    var render = vfx.Row(57000).ToArray();
                    Require(render[125] == 2 && I32(render, 128) == 200 && !vfx.Contains(ShadeVfx),
                        "Unexpected stock shade VFX or private row collision.");
                    Write32(render, 128, profile.Shade == CompanionShade.Invader ? 60 : 44);
                    Replace("SpEffectVfxParam", "SP_EFFECT_VFX_PARAM_ST", 164, vfx, new() { [ShadeVfx] = render });
                    Write32(effect, primaryVfx, ShadeVfx);
                }
                effectRows.Add(ShadeEffect, effect);
                Write32(npc, colorSlot, ShadeEffect);
            }
            Write32(summon, 8, CustomNpc);
            Replace("NpcParam", "NPC_PARAM_ST", 736, npcs, new() { [CustomNpc] = npc });
        }

        // Keep the original reinforcement curve and isolate the adjustments from the real Nepheli Puppet.
        if (profile.HpPercent != 100 || profile.DamagePercent != 100)
        {
            for (var level = 0; level <= 10; level++)
            {
                var reference = 40 + level * 4;
                var originalId = I32(summon, reference);
                var newId = EffectStart + level;
                Require(originalId == StockEffectStart + level && !effects.Contains(newId),
                    "Unexpected reinforcement effect or custom ID collision.");
                var effect = effects.Row(originalId).ToArray();
                Scale(effect, 16, profile.HpPercent);
                foreach (var field in DamageRates)
                    Scale(effect, field, profile.DamagePercent);
                effectRows.Add(newId, effect);
                Write32(summon, reference, newId);
            }
        }

        if (effectRows.Count > 0)
            Replace("SpEffectParam", "SP_EFFECT_PARAM_ST", 912, effects, effectRows);

        Replace("BuddyParam", "BUDDY_PARAM_ST", 160, buddy, new() { [spawn.RowId] = summon });
        Replace("CharaInitParam", "CHARACTER_INIT_PARAM", 320, characters, new() { [CustomRow] = init });
        if (face is not null)
            Replace("FaceParam", "FACE_PARAM_ST", 240, faces, new() { [CustomRow] = face });
        var package = original.Pack(replacements);
        var reopened = RegulationBinary.Read(package);
        original.Verify(reopened, replacements);
        foreach (var check in checks)
            check(reopened);

        Directory.CreateDirectory(parent);
        var staging = Path.Combine(parent, ".spirit-staging-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(staging);
        try
        {
            Directory.CreateDirectory(Path.Combine(staging, "mod"));
            File.WriteAllBytes(Path.Combine(staging, "mod", "regulation.bin"), package);
            if (messages is not null)
            {
                var messageDirectory = Path.Combine(staging, "mod", "msg", "engus");
                Directory.CreateDirectory(messageDirectory);
                foreach (var (file, bytes) in messages)
                    File.WriteAllBytes(Path.Combine(messageDirectory, file), bytes);
            }
            File.WriteAllText(Path.Combine(staging, "spirit.me3"), """
                profileVersion = "v1"
                savefile = "SpiritCreatorTest.sl2"
                start_online = false

                [[supports]]
                game = "eldenring"

                [[packages]]
                path = "mod"
                """ + "\n");
            Directory.Move(staging, output);
            return Path.Combine(output, "spirit.me3");
        }
        finally
        {
            if (Directory.Exists(staging))
                Directory.Delete(staging, recursive: true);
        }
    }

    private static bool UsesCustomWeapon(ReadOnlySpan<byte> row, int id) =>
        (row[232] == 1 && I32(row, 16) == id) || (row[233] == 1 && I32(row, 20) == id) ||
        (row[234] == 1 && I32(row, 264) == id) || (row[235] == 1 && I32(row, 24) == id) ||
        (row[236] == 1 && I32(row, 28) == id) || (row[237] == 1 && I32(row, 268) == id);

    internal static int CheckWeapon(RegulationParam table, RegulationParam reinforcement, int id,
        WeaponAffinity affinity, int level, NpcAttributes attributes, string hand)
    {
        if (id == EquipmentCatalog.Empty)
            return id;
        var item = EquipmentCatalog.Weapon(id);
        var variantId = WeaponUpgradeCatalog.VariantId(id, affinity);
        var row = table.Row(variantId);
        var baseRow = table.Row(id);
        var originOffset = level < 16 ? 96 + 4 * level : 592 + 4 * (level - 16);
        Require((affinity == WeaponAffinity.Standard || (baseRow[265] & 0x80) == 0 && baseRow[577] == 2) &&
                I32(row, originOffset) == id &&
                reinforcement.Contains(BinaryPrimitives.ReadUInt16LittleEndian(row[218..]) + level),
            $"{hand}: {item.Name} does not support {affinity} +{level} in the stock game.");
        var actual = new WeaponRequirements(row[242], row[243], row[244], row[245], row[405]);
        Require(actual == item.Requirements, $"The catalog requirements for {item.Name} do not match the stock game.");
        Require(((row[261] & 0x80) != 0) == item.Sorcery && ((row[262] & 2) != 0) == item.Incantation,
            $"Unexpected stock casting capabilities for {item.Name}.");
        var missing = actual.Describe(attributes);
        Require(missing.Length == 0, $"{hand}: {item.Name} requires {missing}.");
        return variantId;
    }

    private static void WriteExtras(RegulationBinary original, byte[] init, SpiritProfile profile)
    {
        var ids = profile.TalismanIds ?? [];
        var accessories = ids.Any(id => id >= 0)
            ? new RegulationParam(original.Table("EquipParamAccessory"), "EQUIP_PARAM_ACCESSORY_ST", 96) : null;
        for (var slot = 0; slot < ExtrasCatalog.TalismanSlots; slot++)
        {
            var id = slot < ids.Length ? ids[slot] : EquipmentCatalog.Empty;
            if (id >= 0)
            {
                var choice = ExtrasCatalog.Talismans.First(entry => entry.Id == id);
                var row = accessories!.Row(id);
                Require(row[49] == 2 && I32(row, 4) == choice.EffectId,
                    $"Unexpected stock passive effect for {choice.Name}.");
            }
            Write32(init, 64 + 4 * slot, id);
        }
        if (profile.Consumables is { } stacks)
        {
            var goods = new RegulationParam(original.Table("EquipParamGoods"), "EQUIP_PARAM_GOODS_ST", 176);
            for (var slot = 0; slot < 10; slot++)
            {
                var stack = slot < stacks.Length ? stacks[slot] : null;
                if (stack is not null)
                {
                    var choice = ExtrasCatalog.Consumables.First(entry => entry.Id == stack.Id);
                    var row = goods.Row(stack.Id);
                    Require(I32(row, 124) == choice.AiUsage && I32(row, 4) == choice.ReferenceId &&
                            row[63] == choice.ReferenceCategory && (row[72] & 0x80) != 0 &&
                            BinaryPrimitives.ReadUInt16LittleEndian(row[58..]) >= choice.MaxQuantity,
                        $"Unexpected stock AI usage, effect, or stack limit for {choice.Name}.");
                }
                Write32(init, 124 + slot * 4, stack?.Id ?? EquipmentCatalog.Empty);
                init[204 + slot] = (byte)(stack?.Quantity ?? 0);
            }
            // Leave the source NPC's secondary item slots alone: these include its Talisman Pouches.
        }
    }

    private static void WriteSpells(RegulationBinary original, byte[] init, SpiritProfile profile, NpcAttributes attributes)
    {
        var ids = profile.SpellIds ?? SpellCatalog.StarterIds(profile.WeaponId);
        if (SpellCatalog.LoadoutProblem(profile.CombatPreset, profile.WeaponId, ids, attributes) is { } problem)
            throw new InvalidDataException(problem);
        var choices = SpellCatalog.ForWeapon(profile.WeaponId);
        var magic = ids.Length == 0 ? null : new RegulationParam(original.Table("Magic"), "MAGIC_PARAM_ST", 168);
        for (var slot = 0; slot < SpellCatalog.SlotCount; slot++)
        {
            var id = slot < ids.Length ? ids[slot] : -1;
            if (id >= 0)
                VerifySpell(magic!.Row(id), choices.First(spell => spell.Id == id), profile.WeaponId);
            Write32(init, 96 + 4 * slot, id);
        }
    }

    internal static void VerifySpell(ReadOnlySpan<byte> row, SpellChoice spell, int weaponId)
    {
        var actual = new WeaponRequirements(Intelligence: row[34], Faith: row[35], Arcane: row[14]);
        var weapon = EquipmentCatalog.Weapon(weaponId);
        Require(actual == spell.Requirements && row[38] == spell.CastType &&
            (spell.CastType == 0 ? weapon.Sorcery : weapon.Incantation) && I32(row, 116) == spell.AiUsage,
            $"Unexpected stock requirements, catalyst type or AI usage for {spell.Name}.");
    }

    private static void Scale(byte[] row, int offset, int percent)
    {
        if (percent == 100)
            return;
        var original = BinaryPrimitives.ReadSingleLittleEndian(row.AsSpan(offset));
        Require(float.IsFinite(original) && original > 0, "Unexpected reinforcement multiplier.");
        BinaryPrimitives.WriteSingleLittleEndian(row.AsSpan(offset), original * (percent / 100f));
    }
}
