using System.Runtime.CompilerServices;

namespace SpaceAudio.Audio;

internal static class SoftClipper
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float Process(float input)
    {
        if (input >= 1.0f) return 1.0f;
        if (input <= -1.0f) return -1.0f;
        return input - input * input * input * 0.333333f;
    }
}
