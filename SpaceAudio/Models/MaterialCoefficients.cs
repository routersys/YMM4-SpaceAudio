using SpaceAudio.Enums;
using System.Collections.Frozen;

namespace SpaceAudio.Models;

public static class MaterialCoefficients
{
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

    public static float GetAbsorption(WallMaterial material) =>
        Absorption.GetValueOrDefault(material, 0.10f);
}
