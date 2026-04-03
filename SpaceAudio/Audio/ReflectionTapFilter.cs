using System.Runtime.CompilerServices;

namespace SpaceAudio.Audio;

internal struct ReflectionTapFilter
{
    private float _state;
    private float _coeff;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void SetCoefficient(float spectralDamping, float distance)
    {
        float distanceFactor = Math.Clamp(distance * 0.05f, 0.0f, 0.3f);
        _coeff = Math.Clamp(spectralDamping + distanceFactor, 0.0f, 0.995f);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public float Process(float input)
    {
        _state = input * (1.0f - _coeff) + _state * _coeff;
        return _state;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Reset() => _state = 0;
}
