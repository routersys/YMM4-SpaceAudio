using SpaceAudio.Models;
using System.Runtime.CompilerServices;

namespace SpaceAudio.Audio;

internal static class RoomAcousticsCalculator
{
    private const float SpeedOfSound = 343.0f;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float CalculateDirectDistance(in RoomSnapshot snap)
    {
        float dx = snap.SourceX - snap.ListenerX;
        float dy = snap.SourceY - snap.ListenerY;
        float dz = snap.SourceZ - snap.ListenerZ;
        return MathF.Sqrt(dx * dx + dy * dy + dz * dz);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float CalculateRT60(float volume, float totalAbsorption) =>
        totalAbsorption > 0 ? 0.161f * volume / totalAbsorption : 5.0f;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float CalculateRoomVolume(in RoomSnapshot snap) =>
        snap.Width * snap.Height * snap.Depth;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float CalculateTotalSurfaceArea(in RoomSnapshot snap)
    {
        float w = snap.Width, h = snap.Height, d = snap.Depth;
        return 2.0f * (w * d + w * h + d * h);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float CalculatePreDelay(in RoomSnapshot snap)
    {
        float dist = CalculateDirectDistance(in snap);
        return dist / SpeedOfSound;
    }
}
