using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace SpaceAudio.Audio;

internal sealed class DelayLine : IDisposable
{
    private readonly float[] _buffer;
    private readonly int _mask;
    private int _writePos;

    public int MaxDelay => _buffer.Length;

    public DelayLine(int maxDelaySamples)
    {
        int size = 1;
        while (size < maxDelaySamples + 1) size <<= 1;
        _buffer = GC.AllocateArray<float>(size, pinned: true);
        _mask = size - 1;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public float Process(float input, int delaySamples)
    {
        ref float baseRef = ref MemoryMarshal.GetArrayDataReference(_buffer);
        float output = Unsafe.Add(ref baseRef, (_writePos - delaySamples) & _mask);
        Unsafe.Add(ref baseRef, _writePos & _mask) = input;
        _writePos++;
        return output;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public float Read(int delaySamples)
        => Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(_buffer), (_writePos - delaySamples) & _mask);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public float ReadAt(int delaySamples, int basePos)
        => Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(_buffer), (basePos - delaySamples) & _mask);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Write(float value)
    {
        Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(_buffer), _writePos & _mask) = value;
        _writePos++;
    }

    public int CurrentWritePosition
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _writePos;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public float ProcessInterpolated(float input, float delaySamples)
    {
        ref float baseRef = ref MemoryMarshal.GetArrayDataReference(_buffer);
        int intDelay = (int)delaySamples;
        float frac = delaySamples - intDelay;
        float a = Unsafe.Add(ref baseRef, (_writePos - intDelay) & _mask);
        float b = Unsafe.Add(ref baseRef, (_writePos - intDelay - 1) & _mask);
        float output = a + frac * (b - a);
        Unsafe.Add(ref baseRef, _writePos & _mask) = input;
        _writePos++;
        return output;
    }

    public void Reset()
    {
        Array.Clear(_buffer);
        _writePos = 0;
    }

    public void Dispose() { }
}
