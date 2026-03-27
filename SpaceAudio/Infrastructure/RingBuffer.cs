using System.Runtime.CompilerServices;

namespace SpaceAudio.Infrastructure;

internal sealed class RingBuffer
{
    private readonly float[] _buffer;
    private readonly int _mask;
    private int _writePos;

    public int Length => _buffer.Length;

    public RingBuffer(int sizeInPowerOfTwo)
    {
        int size = 1;
        while (size < sizeInPowerOfTwo) size <<= 1;
        _buffer = new float[size];
        _mask = size - 1;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Write(float value)
    {
        _buffer[_writePos & _mask] = value;
        _writePos++;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public float Read(int delaySamples)
    {
        int readPos = (_writePos - delaySamples) & _mask;
        return _buffer[readPos];
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public float ReadInterpolated(float delaySamples)
    {
        int intDelay = (int)delaySamples;
        float frac = delaySamples - intDelay;
        float a = Read(intDelay);
        float b = Read(intDelay + 1);
        return a + frac * (b - a);
    }

    public void Clear()
    {
        Array.Clear(_buffer);
        _writePos = 0;
    }
}
