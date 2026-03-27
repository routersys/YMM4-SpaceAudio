using System.Runtime.CompilerServices;

namespace SpaceAudio.Audio;

internal sealed class AllPassFilter : IDisposable
{
    private readonly float[] _buffer;
    private readonly int _mask;
    private readonly int _delay;
    private int _writePos;
    private float _gain;

    public AllPassFilter(int delaySamples, float gain = 0.5f)
    {
        _delay = delaySamples;
        _gain = gain;
        int size = 1;
        while (size < delaySamples + 1) size <<= 1;
        _buffer = new float[size];
        _mask = size - 1;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public float Process(float input)
    {
        float delayed = _buffer[(_writePos - _delay) & _mask];
        float output = -_gain * input + delayed;
        _buffer[_writePos & _mask] = input + _gain * delayed;
        _writePos++;
        return output;
    }

    public void SetGain(float gain) => _gain = gain;

    public void Reset()
    {
        Array.Clear(_buffer);
        _writePos = 0;
    }

    public void Dispose() { }
}
