using System.Runtime.CompilerServices;

namespace SpaceAudio.Audio;

internal sealed class CombFilter : IDisposable
{
    private readonly float[] _buffer;
    private readonly int _mask;
    private readonly int _delay;
    private int _writePos;
    private float _feedback;
    private float _dampState;
    private float _damp1;
    private float _damp2;

    public CombFilter(int delaySamples, float feedback = 0.84f, float damping = 0.2f)
    {
        _delay = delaySamples;
        _feedback = feedback;
        int size = 1;
        while (size < delaySamples + 1) size <<= 1;
        _buffer = new float[size];
        _mask = size - 1;
        SetDamping(damping);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public float Process(float input)
    {
        float delayed = _buffer[(_writePos - _delay) & _mask];
        _dampState = delayed * _damp2 + _dampState * _damp1;
        _buffer[_writePos & _mask] = input + _dampState * _feedback;
        _writePos++;
        return delayed;
    }

    public void SetFeedback(float feedback) => _feedback = feedback;

    public void SetDamping(float damping)
    {
        _damp1 = damping;
        _damp2 = 1.0f - damping;
    }

    public void Reset()
    {
        Array.Clear(_buffer);
        _writePos = 0;
        _dampState = 0;
    }

    public void Dispose() { }
}
