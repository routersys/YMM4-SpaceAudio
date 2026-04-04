using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;

namespace SpaceAudio.Audio.Convolution;

internal sealed class OlaConvolver : IDisposable
{
    private const int BlockSize = 1024;
    private static readonly int MaxFftSize = CooleyTukeyFft.NextPowerOf2(BlockSize + 88200);

    private readonly float[] _irSpecRe = GC.AllocateArray<float>(MaxFftSize, pinned: true);
    private readonly float[] _irSpecIm = GC.AllocateArray<float>(MaxFftSize, pinned: true);
    private readonly float[] _irSpecReR = GC.AllocateArray<float>(MaxFftSize, pinned: true);
    private readonly float[] _irSpecImR = GC.AllocateArray<float>(MaxFftSize, pinned: true);
    private readonly float[] _overlapL = GC.AllocateArray<float>(MaxFftSize, pinned: true);
    private readonly float[] _overlapR = GC.AllocateArray<float>(MaxFftSize, pinned: true);
    private readonly float[] _inputRe = GC.AllocateArray<float>(MaxFftSize, pinned: true);
    private readonly float[] _inputIm = GC.AllocateArray<float>(MaxFftSize, pinned: true);
    private readonly float[] _outRe = GC.AllocateArray<float>(MaxFftSize, pinned: true);
    private readonly float[] _outIm = GC.AllocateArray<float>(MaxFftSize, pinned: true);
    private readonly float[] _outReR = GC.AllocateArray<float>(MaxFftSize, pinned: true);
    private readonly float[] _outImR = GC.AllocateArray<float>(MaxFftSize, pinned: true);

    private int _fftSize;
    private bool _hasActiveIr;
    private StereoImpulseResponse? _pendingIr;

    public bool HasActiveIr => _hasActiveIr;

    public void SubmitIr(StereoImpulseResponse ir) => Volatile.Write(ref _pendingIr, ir);

    private void ApplyPendingIr()
    {
        var pending = Interlocked.Exchange(ref _pendingIr, null);
        if (pending is null) return;

        int irLen = pending.Length;
        int fftSize = CooleyTukeyFft.NextPowerOf2(BlockSize + irLen - 1);
        fftSize = Math.Min(fftSize, MaxFftSize);
        int copyLen = Math.Min(irLen, fftSize);

        Array.Clear(_irSpecRe, 0, fftSize);
        Array.Clear(_irSpecIm, 0, fftSize);
        Array.Clear(_irSpecReR, 0, fftSize);
        Array.Clear(_irSpecImR, 0, fftSize);
        Array.Clear(_overlapL, 0, fftSize);
        Array.Clear(_overlapR, 0, fftSize);

        Buffer.BlockCopy(pending.Left, 0, _irSpecRe, 0, copyLen * sizeof(float));
        Buffer.BlockCopy(pending.Right, 0, _irSpecReR, 0, copyLen * sizeof(float));

        CooleyTukeyFft.Forward(_irSpecRe, _irSpecIm, fftSize);
        CooleyTukeyFft.Forward(_irSpecReR, _irSpecImR, fftSize);

        _fftSize = fftSize;
        _hasActiveIr = true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void ProcessBlock(float[] buffer, int offset, int frames)
    {
        if (Volatile.Read(ref _pendingIr) is not null)
            ApplyPendingIr();

        if (!_hasActiveIr) return;

        int fftSize = _fftSize;
        int processed = 0;

        while (processed < frames)
        {
            int chunk = Math.Min(frames - processed, BlockSize);
            ProcessChunk(buffer, offset + processed * 2, chunk, fftSize);
            processed += chunk;
        }
    }

    private void ProcessChunk(float[] buffer, int offset, int frames, int fftSize)
    {
        Array.Clear(_inputRe, 0, fftSize);
        Array.Clear(_inputIm, 0, fftSize);

        ref float bufRef = ref MemoryMarshal.GetArrayDataReference(buffer);
        ref float inReRef = ref MemoryMarshal.GetArrayDataReference(_inputRe);

        for (int i = 0; i < frames; i++)
            Unsafe.Add(ref inReRef, i) =
                (Unsafe.Add(ref bufRef, offset + i * 2) + Unsafe.Add(ref bufRef, offset + i * 2 + 1)) * 0.5f;

        CooleyTukeyFft.Forward(_inputRe, _inputIm, fftSize);

        CooleyTukeyFft.MultiplyAccumulate(_outRe, _outIm, _inputRe, _inputIm, _irSpecRe, _irSpecIm, fftSize);
        CooleyTukeyFft.Inverse(_outRe, _outIm, fftSize);

        CooleyTukeyFft.MultiplyAccumulate(_outReR, _outImR, _inputRe, _inputIm, _irSpecReR, _irSpecImR, fftSize);
        CooleyTukeyFft.Inverse(_outReR, _outImR, fftSize);

        ref float overlapLRef = ref MemoryMarshal.GetArrayDataReference(_overlapL);
        ref float overlapRRef = ref MemoryMarshal.GetArrayDataReference(_overlapR);
        ref float outReRef = ref MemoryMarshal.GetArrayDataReference(_outRe);
        ref float outReRRef = ref MemoryMarshal.GetArrayDataReference(_outReR);

        for (int i = 0; i < frames; i++)
        {
            Unsafe.Add(ref bufRef, offset + i * 2) =
                Unsafe.Add(ref outReRef, i) + Unsafe.Add(ref overlapLRef, i);
            Unsafe.Add(ref bufRef, offset + i * 2 + 1) =
                Unsafe.Add(ref outReRRef, i) + Unsafe.Add(ref overlapRRef, i);
        }

        int tailLen = fftSize - frames;
        if (tailLen <= 0) return;

        ref float outReTail = ref Unsafe.Add(ref outReRef, frames);
        ref float outReRTail = ref Unsafe.Add(ref outReRRef, frames);
        ref float ovLTail = ref Unsafe.Add(ref overlapLRef, frames);
        ref float ovRTail = ref Unsafe.Add(ref overlapRRef, frames);

        int j = 0;

        if (Avx.IsSupported && tailLen >= 8)
        {
            int simdEnd = tailLen & ~7;
            for (; j < simdEnd; j += 8)
            {
                Vector256.StoreUnsafe(
                    Vector256.Add(
                        Vector256.LoadUnsafe(ref Unsafe.Add(ref outReTail, j)),
                        Vector256.LoadUnsafe(ref Unsafe.Add(ref ovLTail, j))),
                    ref Unsafe.Add(ref overlapLRef, j));
                Vector256.StoreUnsafe(
                    Vector256.Add(
                        Vector256.LoadUnsafe(ref Unsafe.Add(ref outReRTail, j)),
                        Vector256.LoadUnsafe(ref Unsafe.Add(ref ovRTail, j))),
                    ref Unsafe.Add(ref overlapRRef, j));
            }
        }
        else if (Sse.IsSupported && tailLen >= 4)
        {
            int simdEnd = tailLen & ~3;
            for (; j < simdEnd; j += 4)
            {
                Vector128.StoreUnsafe(
                    Vector128.Add(
                        Vector128.LoadUnsafe(ref Unsafe.Add(ref outReTail, j)),
                        Vector128.LoadUnsafe(ref Unsafe.Add(ref ovLTail, j))),
                    ref Unsafe.Add(ref overlapLRef, j));
                Vector128.StoreUnsafe(
                    Vector128.Add(
                        Vector128.LoadUnsafe(ref Unsafe.Add(ref outReRTail, j)),
                        Vector128.LoadUnsafe(ref Unsafe.Add(ref ovRTail, j))),
                    ref Unsafe.Add(ref overlapRRef, j));
            }
        }

        for (; j < tailLen; j++)
        {
            Unsafe.Add(ref overlapLRef, j) =
                Unsafe.Add(ref outReTail, j) + Unsafe.Add(ref ovLTail, j);
            Unsafe.Add(ref overlapRRef, j) =
                Unsafe.Add(ref outReRTail, j) + Unsafe.Add(ref ovRTail, j);
        }
    }

    public void Reset()
    {
        _pendingIr = null;
        _hasActiveIr = false;
        _fftSize = 0;
        Array.Clear(_irSpecRe);
        Array.Clear(_irSpecIm);
        Array.Clear(_irSpecReR);
        Array.Clear(_irSpecImR);
        Array.Clear(_overlapL);
        Array.Clear(_overlapR);
        Array.Clear(_inputRe);
        Array.Clear(_inputIm);
        Array.Clear(_outRe);
        Array.Clear(_outIm);
        Array.Clear(_outReR);
        Array.Clear(_outImR);
    }

    public void Dispose() => Reset();
}
