using System.Runtime.CompilerServices;

namespace SpaceAudio.Audio;

internal static class SoftClipper
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float Process(float input)
    {
        if (float.IsNaN(input) || float.IsInfinity(input)) return 0.0f;
        return input;
    }
}
