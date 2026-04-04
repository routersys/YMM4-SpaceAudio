using System.Runtime.CompilerServices;

namespace SpaceAudio.Audio;

internal sealed class LowPassOnePoleCascade : IDisposable
{
    private const int Stages = 2;
    private readonly float[] _state = new float[Stages];
    private float _coeff;

    public LowPassOnePoleCascade(float cutoffNormalized = 0.25f)
    {
        SetCutoff(cutoffNormalized);
    }

    public void SetCutoff(float normalized)
    {
        normalized = Math.Clamp(normalized, 0.001f, 0.499f);
        _coeff = MathF.Exp(-2.0f * MathF.PI * normalized);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public float Process(float input)
    {
        float x = input;
        for (int i = 0; i < Stages; i++)
        {
            _state[i] = x * (1.0f - _coeff) + _state[i] * _coeff;
            if (MathF.Abs(_state[i]) < 1e-9f) _state[i] = 0.0f;
            x = _state[i];
        }
        return x;
    }

    public void Reset() => Array.Clear(_state);

    public void Dispose() { }
}
