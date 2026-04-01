using System.Runtime.CompilerServices;

namespace SpaceAudio.Models;

public readonly record struct GeometryVertex(float X, float Y, float Z)
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public float DistanceTo(in GeometryVertex other)
    {
        float dx = X - other.X, dy = Y - other.Y, dz = Z - other.Z;
        return MathF.Sqrt(dx * dx + dy * dy + dz * dz);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public float DistanceSquaredTo(in GeometryVertex other)
    {
        float dx = X - other.X, dy = Y - other.Y, dz = Z - other.Z;
        return dx * dx + dy * dy + dz * dz;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public GeometryVertex Subtract(in GeometryVertex other) =>
        new(X - other.X, Y - other.Y, Z - other.Z);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public GeometryVertex Add(in GeometryVertex other) =>
        new(X + other.X, Y + other.Y, Z + other.Z);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public GeometryVertex Scale(float s) => new(X * s, Y * s, Z * s);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static GeometryVertex Cross(in GeometryVertex a, in GeometryVertex b) =>
        new(a.Y * b.Z - a.Z * b.Y, a.Z * b.X - a.X * b.Z, a.X * b.Y - a.Y * b.X);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float Dot(in GeometryVertex a, in GeometryVertex b) =>
        a.X * b.X + a.Y * b.Y + a.Z * b.Z;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public float Length() => MathF.Sqrt(X * X + Y * Y + Z * Z);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public GeometryVertex Normalized()
    {
        float len = Length();
        return len > 1e-8f ? new(X / len, Y / len, Z / len) : default;
    }
}
