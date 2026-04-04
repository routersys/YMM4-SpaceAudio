using System.Runtime.CompilerServices;

namespace SpaceAudio.Audio;

internal sealed class OutputLimiter
{
    public OutputLimiter(int sampleRate)
    {
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Process(ref float left, ref float right)
    {
        left = Limit(left);
        right = Limit(right);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static float Limit(float input)
    {
        if (float.IsNaN(input) || float.IsInfinity(input)) return 0.0f;
        if (input > 24.0f) return 24.0f;
        if (input < -24.0f) return -24.0f;
        return input;
    }

    public void Reset() { }
}
