using SpaceAudio.Models;
using System.Runtime.CompilerServices;

namespace SpaceAudio.Audio.Bvh;

internal readonly struct AabbBox
{
    public readonly float MinX, MinY, MinZ;
    public readonly float MaxX, MaxY, MaxZ;

    public AabbBox(float minX, float minY, float minZ, float maxX, float maxY, float maxZ)
    {
        MinX = minX; MinY = minY; MinZ = minZ;
        MaxX = maxX; MaxY = maxY; MaxZ = maxZ;
    }

    public static readonly AabbBox Empty = new(float.MaxValue, float.MaxValue, float.MaxValue,
        float.MinValue, float.MinValue, float.MinValue);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public AabbBox Expand(in GeometryVertex v) => new(
        MathF.Min(MinX, v.X), MathF.Min(MinY, v.Y), MathF.Min(MinZ, v.Z),
        MathF.Max(MaxX, v.X), MathF.Max(MaxY, v.Y), MathF.Max(MaxZ, v.Z));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public AabbBox Merge(in AabbBox other) => new(
        MathF.Min(MinX, other.MinX), MathF.Min(MinY, other.MinY), MathF.Min(MinZ, other.MinZ),
        MathF.Max(MaxX, other.MaxX), MathF.Max(MaxY, other.MaxY), MathF.Max(MaxZ, other.MaxZ));

    public float CenterX => (MinX + MaxX) * 0.5f;
    public float CenterY => (MinY + MaxY) * 0.5f;
    public float CenterZ => (MinZ + MaxZ) * 0.5f;

    public float SurfaceArea()
    {
        float dx = MaxX - MinX, dy = MaxY - MinY, dz = MaxZ - MinZ;
        return 2.0f * (dx * dy + dy * dz + dz * dx);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool IntersectsRay(float ox, float oy, float oz, float invDx, float invDy, float invDz, float maxT)
    {
        float t1x = (MinX - ox) * invDx, t2x = (MaxX - ox) * invDx;
        float t1y = (MinY - oy) * invDy, t2y = (MaxY - oy) * invDy;
        float t1z = (MinZ - oz) * invDz, t2z = (MaxZ - oz) * invDz;

        float tMin = MathF.Max(MathF.Max(MathF.Min(t1x, t2x), MathF.Min(t1y, t2y)), MathF.Min(t1z, t2z));
        float tMax = MathF.Min(MathF.Min(MathF.Max(t1x, t2x), MathF.Max(t1y, t2y)), MathF.Max(t1z, t2z));

        return tMax >= MathF.Max(0.0f, tMin) && tMin <= maxT;
    }

    public AabbBox Padded()
    {
        const float eps = 1e-4f;
        return new(MinX - eps, MinY - eps, MinZ - eps, MaxX + eps, MaxY + eps, MaxZ + eps);
    }
}
