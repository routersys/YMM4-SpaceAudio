using SpaceAudio.Enums;
using System.Collections.Frozen;
using System.Runtime.CompilerServices;

namespace SpaceAudio.Models;

public static class MaterialCoefficients
{
    public const int BandCount = 6;

    public static readonly int[] BandFrequencies = [125, 250, 500, 1000, 2000, 4000];

    private static readonly FrozenDictionary<WallMaterial, float[]> BandData = new Dictionary<WallMaterial, float[]>
    {
        [WallMaterial.Concrete] = [0.01f, 0.01f, 0.015f, 0.02f, 0.02f, 0.02f],
        [WallMaterial.Wood] = [0.15f, 0.11f, 0.10f, 0.07f, 0.06f, 0.07f],
        [WallMaterial.Glass] = [0.35f, 0.25f, 0.18f, 0.12f, 0.07f, 0.04f],
        [WallMaterial.Carpet] = [0.02f, 0.06f, 0.14f, 0.37f, 0.60f, 0.65f],
        [WallMaterial.AcousticPanel] = [0.28f, 0.55f, 0.80f, 0.90f, 0.85f, 0.80f],
        [WallMaterial.Brick] = [0.03f, 0.03f, 0.03f, 0.04f, 0.05f, 0.07f],
        [WallMaterial.Drywall] = [0.29f, 0.10f, 0.05f, 0.04f, 0.07f, 0.09f],
        [WallMaterial.Tile] = [0.01f, 0.01f, 0.015f, 0.02f, 0.02f, 0.02f]
    }.ToFrozenDictionary();

    private static readonly FrozenDictionary<WallMaterial, float> Absorption = new Dictionary<WallMaterial, float>
    {
        [WallMaterial.Concrete] = 0.02f,
        [WallMaterial.Wood] = 0.10f,
        [WallMaterial.Glass] = 0.03f,
        [WallMaterial.Carpet] = 0.50f,
        [WallMaterial.AcousticPanel] = 0.80f,
        [WallMaterial.Brick] = 0.05f,
        [WallMaterial.Drywall] = 0.12f,
        [WallMaterial.Tile] = 0.02f
    }.ToFrozenDictionary();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float GetAbsorption(WallMaterial material) =>
        Absorption.GetValueOrDefault(material, 0.10f);

    public static float[] GetBandAbsorption(WallMaterial material) =>
        BandData.TryGetValue(material, out var bands) ? bands : [0.10f, 0.10f, 0.10f, 0.10f, 0.10f, 0.10f];

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float ComputeBroadband(ReadOnlySpan<float> bands)
    {
        if (bands.Length < BandCount) return 0.10f;
        float sum = 0;
        for (int i = 0; i < BandCount; i++) sum += bands[i];
        return sum / BandCount;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float ComputeSpectralDamping(ReadOnlySpan<float> bands)
    {
        if (bands.Length < BandCount) return 0.3f;
        float lowRefl = MathF.Sqrt((1.0f - bands[0]) * (1.0f - bands[1]));
        float highRefl = MathF.Sqrt((1.0f - bands[4]) * (1.0f - bands[5]));
        lowRefl = Math.Max(lowRefl, 1e-6f);
        float ratio = Math.Clamp(highRefl / lowRefl, 0.01f, 1.0f);
        return (1.0f - ratio) / (1.0f + ratio);
    }

    public static float[] ResolveBandAbsorption(string materialId, WallMaterial fallback)
    {
        var mat = Services.ServiceLocator.MaterialService.GetById(materialId);
        if (mat is not null && mat.BandAbsorption is { Length: >= BandCount })
            return mat.BandAbsorption;
        if (mat is not null)
            return [mat.Absorption, mat.Absorption, mat.Absorption, mat.Absorption, mat.Absorption, mat.Absorption];
        return GetBandAbsorption(fallback);
    }
}
