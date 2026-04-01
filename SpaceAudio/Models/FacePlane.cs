using System.Runtime.CompilerServices;

namespace SpaceAudio.Models;

public readonly record struct FacePlane(float Nx, float Ny, float Nz, float D, float Absorption)
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public GeometryVertex ReflectPoint(in GeometryVertex p)
    {
        float dist = Nx * p.X + Ny * p.Y + Nz * p.Z + D;
        return new(p.X - 2 * dist * Nx, p.Y - 2 * dist * Ny, p.Z - 2 * dist * Nz);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public float DistanceToPoint(in GeometryVertex p) =>
        MathF.Abs(Nx * p.X + Ny * p.Y + Nz * p.Z + D);

    public static FacePlane FromVertices(in GeometryVertex v0, in GeometryVertex v1, in GeometryVertex v2, float absorption)
    {
        var e1 = v1.Subtract(in v0);
        var e2 = v2.Subtract(in v0);
        var normal = GeometryVertex.Cross(in e1, in e2).Normalized();
        float d = -GeometryVertex.Dot(in normal, in v0);
        return new(normal.X, normal.Y, normal.Z, d, absorption);
    }
}
