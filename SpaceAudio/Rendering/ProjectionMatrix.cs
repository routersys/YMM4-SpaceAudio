using System.Numerics;
using System.Runtime.CompilerServices;
using System.Windows;

namespace SpaceAudio.Rendering;

internal static class ProjectionMatrix
{
    public const float NearPlane = 0.1f;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Matrix4x4 CreateViewMatrix(Camera3D camera)
    {
        var (ex, ey, ez) = camera.GetEyePosition();
        var eye = new Vector3(ex, ey, ez);
        var target = new Vector3(camera.TargetX, camera.TargetY, camera.TargetZ);
        var up = new Vector3(0, 1, 0);

        if (MathF.Abs(camera.Pitch) > 89.5f)
        {
            up = new Vector3(0, 0, camera.Pitch > 0 ? -1 : 1);
        }

        return Matrix4x4.CreateLookAt(eye, target, up);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Point ProjectToScreen(Vector3 camPos, double viewWidth, double viewHeight, float fov = 50.0f)
    {
        float aspect = (float)(viewWidth / Math.Max(viewHeight, 1.0));
        float tanHalfFov = MathF.Tan(fov * MathF.PI / 360.0f);

        float invZ = 1.0f / Math.Max(0.001f, -camPos.Z);

        double screenX = viewWidth * 0.5 + camPos.X * invZ / (tanHalfFov * aspect) * viewWidth * 0.5;
        double screenY = viewHeight * 0.5 - camPos.Y * invZ / tanHalfFov * viewHeight * 0.5;

        return new Point(screenX, screenY);
    }

    public static int ClipPolygonZ(ReadOnlySpan<Vector3> input, Span<Vector3> output)
    {
        if (input.Length < 3) return 0;

        int outCount = 0;
        Vector3 s = input[^1];
        bool sInside = s.Z <= -NearPlane;

        for (int i = 0; i < input.Length; i++)
        {
            Vector3 p = input[i];
            bool pInside = p.Z <= -NearPlane;

            if (pInside)
            {
                if (!sInside)
                {
                    output[outCount++] = IntersectZ(s, p, -NearPlane);
                }
                output[outCount++] = p;
            }
            else if (sInside)
            {
                output[outCount++] = IntersectZ(s, p, -NearPlane);
            }

            s = p;
            sInside = pInside;
        }

        return outCount;
    }

    public static bool ClipLineZ(ref Vector3 p1, ref Vector3 p2)
    {
        bool inside1 = p1.Z <= -NearPlane;
        bool inside2 = p2.Z <= -NearPlane;

        if (!inside1 && !inside2) return false;

        if (inside1 && !inside2)
            p2 = IntersectZ(p1, p2, -NearPlane);
        else if (!inside1 && inside2)
            p1 = IntersectZ(p1, p2, -NearPlane);

        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector3 IntersectZ(Vector3 a, Vector3 b, float z)
    {
        float t = (z - a.Z) / (b.Z - a.Z);
        return new Vector3(
            a.X + (b.X - a.X) * t,
            a.Y + (b.Y - a.Y) * t,
            z);
    }
}
