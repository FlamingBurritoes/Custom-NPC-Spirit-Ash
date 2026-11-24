namespace SpiritCreator;

// Offsets are from the slot's FaceData marker, four bytes before the FACE magic.
// Hypothesis checked by the FISTS/SUPPORT in-game proof, not a complete FaceData conversion.
internal static class FaceMapping
{
    internal static IReadOnlyDictionary<string, int> Offsets { get; } = Build();

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
