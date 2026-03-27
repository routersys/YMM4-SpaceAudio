using System.Runtime.CompilerServices;

namespace SpaceAudio.Audio;

internal sealed class StereoWidener
{
    private float _width = 1.0f;

    public void SetWidth(float width) => _width = Math.Clamp(width, 0.0f, 2.0f);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Process(ref float left, ref float right)
    {
        float mid = (left + right) * 0.5f;
        float side = (left - right) * 0.5f * _width;
        left = mid + side;
        right = mid - side;
    }
}
