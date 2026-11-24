namespace SpiritCreator;

// Offsets are from the slot's FaceData marker, four bytes before the FACE magic.
// Hypothesis checked by the FISTS/SUPPORT in-game proof, not a complete FaceData conversion.
internal static class FaceMapping
{
    internal static IReadOnlyDictionary<string, int> Offsets { get; } = Build();

    // Convert the validated save offsets to byte offsets in the 240-byte FaceParam row.
    internal static int ParamOffset(int source) => source switch
    {
        0x10 => 0, 0x14 => 60, 0x18 => 35, 0x1C => 74, 0x20 => 67, 0x24 => 85, 0x28 => 89, 0x2C => 81,
        >= 0x30 and <= 0x32 => 113 + source - 0x30,
        >= 0x34 and <= 0x6F => 117 + source - 0x34,
        >= 0xB0 and <= 0xB6 => 99 + source - 0xB0,
        >= 0xB7 and <= 0xD4 => 1 + source - 0xB7,
        >= 0xDE and <= 0xE1 => 31 + source - 0xDE,
        0xE2 => 39, >= 0xE3 and <= 0xE5 => 36 + source - 0xE3,
        >= 0xE6 and <= 0xED => 40 + source - 0xE6,
        0xEE => 51, >= 0xEF and <= 0xF1 => 48 + source - 0xEF,
        >= 0xF2 and <= 0xF9 => 52 + source - 0xF2,
        >= 0xFA and <= 0xFF => 61 + source - 0xFA,
        >= 0x100 and <= 0x105 => 68 + source - 0x100,
        >= 0x106 and <= 0x10B => 75 + source - 0x106,
        >= 0x10C and <= 0x10E => 82 + source - 0x10C,
        _ => throw new InvalidDataException("Appearance field lacks a regulation offset.")
    };

    private static Dictionary<string, int> Build()
    {
        var fields = new Dictionary<string, int>(StringComparer.Ordinal);
        void Group(int offset, IEnumerable<string> keys)
        {
            foreach (var key in keys)
                fields.Add(key, offset++);
        }

        string[] models = ["face_partsId", "hair_partsId", "eye_partsId", "eyebrow_partsId",
            "beard_partsId", "accessories_partsId", "decal_partsId", "eyelash_partsId"];
        for (var i = 0; i < models.Length; i++)
            Group(0x10 + i * 4, [models[i]]);
        Group(0x30, ["age", "gender", "caricatureGeometry"]);
        Group(0x34, Enumerable.Range(0, 60).Select(i => $"faceGeoData{i:D2}"));
        Group(0xB0, ["chrBodyScaleHead", "chrBodyScaleBreast", "chrBodyScaleAbdomen",
            "chrBodyScaleRArm", "chrBodyScaleRLeg", "chrBodyScaleLArm", "chrBodyScaleLLeg"]);
        var skin = new List<string>(Rgb("skin_color")) { "skin_gloss", "skin_pores", "face_beard" };
        foreach (var prefix in new[] { "face_aroundEye", "face_cheek", "face_eyeLine",
                     "face_eyeShadowDown", "face_eyeShadowUp", "face_lip" })
        {
            skin.Add(prefix);
            skin.AddRange(Rgb(prefix + "Color"));
        }
        Group(0xB7, skin);
        Group(0xDE, ["body_hair", .. Rgb("body_hairColor")]);
        foreach (var (offset, eye) in new[] { (0xE2, "eyeR_"), (0xEE, "eyeL_") })
            Group(offset, [eye + "irisScale", .. Rgb(eye + "irisColor"), eye + "cataract",
                .. Rgb(eye + "cataractColor"), .. Rgb(eye + "scleraColor"), eye + "irisDistance"]);
        foreach (var (offset, part) in new[] { (0xFA, "hair"), (0x100, "beard"), (0x106, "eyebrow") })
            Group(offset, [.. Rgb(part + "_color"), part + "_shininess", part + "_rootBlack", part + "_whiteDensity"]);
        Group(0x10C, Rgb("eyelash_color"));
        return fields;
    }

    private static string[] Rgb(string prefix) => [prefix + "_R", prefix + "_G", prefix + "_B"];
}
