using System.Runtime.CompilerServices;

namespace SpaceAudio.Audio;

internal sealed class OutputLimiter
{
    private float _envelope;
    private float _attackCoeff;
    private float _releaseCoeff;
    private float _threshold = 0.95f;

    public OutputLimiter(int sampleRate)
    {
        float attackMs = 0.1f;
        float releaseMs = 50.0f;
        _attackCoeff = MathF.Exp(-1.0f / (attackMs * 0.001f * sampleRate));
        _releaseCoeff = MathF.Exp(-1.0f / (releaseMs * 0.001f * sampleRate));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Process(ref float left, ref float right)
    {
        float peak = MathF.Max(MathF.Abs(left), MathF.Abs(right));
        float coeff = peak > _envelope ? _attackCoeff : _releaseCoeff;
        _envelope = coeff * _envelope + (1.0f - coeff) * peak;

        if (_envelope > _threshold)
        {
            float gain = _threshold / _envelope;
            left *= gain;
            right *= gain;
        }
    }

    public void Reset() => _envelope = 0;
}
