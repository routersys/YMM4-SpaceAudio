using System.Runtime.CompilerServices;

namespace SpaceAudio.Audio;

internal sealed class DampingFilter : IDisposable
{
    private float _state;
    private float _coefficient;

    public DampingFilter(float coefficient = 0.5f)
    {
        _coefficient = Math.Clamp(coefficient, 0.0f, 0.999f);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public float Process(float input)
    {
        _state = input * (1.0f - _coefficient) + _state * _coefficient;
        return _state;
    }

    public void SetCoefficient(float coefficient) =>
        _coefficient = Math.Clamp(coefficient, 0.0f, 0.999f);

    public void Reset() => _state = 0;

    public void Dispose() { }
}
