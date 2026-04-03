using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace SpaceAudio.Audio.Convolution;

internal sealed class OlaConvolver : IDisposable
{
    private const int BlockSize = 1024;

    private float[]? _irSpecRe;
    private float[]? _irSpecIm;
    private float[]? _overlapL;
    private float[]? _overlapR;
    private float[]? _irSpecReR;
    private float[]? _irSpecImR;
    private int _fftSize;

    private StereoImpulseResponse? _pendingIr;
    private StereoImpulseResponse? _activeIr;

    private readonly float[] _inputRe;
    private readonly float[] _inputIm;
    private readonly float[] _outRe;
    private readonly float[] _outIm;

    private static readonly int MaxFftSize = CooleyTukeyFft.NextPowerOf2(BlockSize + 88200);

    public OlaConvolver()
    {
        _inputRe = new float[MaxFftSize];
        _inputIm = new float[MaxFftSize];
        _outRe = new float[MaxFftSize];
        _outIm = new float[MaxFftSize];
    }

    public void SubmitIr(StereoImpulseResponse ir)
    {
        Volatile.Write(ref _pendingIr, ir);
    }

    public bool HasActiveIr => _activeIr is not null;

    private void ApplyPendingIr()
    {
        var pending = Interlocked.Exchange(ref _pendingIr, null);
        if (pending is null) return;

        int irLen = pending.Length;
        int fftSize = CooleyTukeyFft.NextPowerOf2(BlockSize + irLen - 1);
        fftSize = Math.Min(fftSize, MaxFftSize);

        float[] specReL = new float[fftSize];
        float[] specImL = new float[fftSize];
        float[] specReR = new float[fftSize];
        float[] specImR = new float[fftSize];

        int copyLen = Math.Min(irLen, fftSize);
        Buffer.BlockCopy(pending.Left, 0, specReL, 0, copyLen * sizeof(float));
        Buffer.BlockCopy(pending.Right, 0, specReR, 0, copyLen * sizeof(float));

        CooleyTukeyFft.Forward(specReL, specImL);
        CooleyTukeyFft.Forward(specReR, specImR);

        _irSpecRe = specReL;
        _irSpecIm = specImL;
        _irSpecReR = specReR;
        _irSpecImR = specImR;
        _fftSize = fftSize;

        _overlapL = new float[fftSize];
        _overlapR = new float[fftSize];
        _activeIr = pending;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void ProcessBlock(float[] buffer, int offset, int frames)
    {
        if (Volatile.Read(ref _pendingIr) is not null)
            ApplyPendingIr();

        if (_activeIr is null || _irSpecRe is null) return;

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
        {
            float l = Unsafe.Add(ref bufRef, offset + i * 2);
            float r = Unsafe.Add(ref bufRef, offset + i * 2 + 1);
            Unsafe.Add(ref inReRef, i) = (l + r) * 0.5f;
        }

        CooleyTukeyFft.Forward(_inputRe, _inputIm);

        CooleyTukeyFft.MultiplyAccumulate(_outRe, _outIm, _inputRe, _inputIm, _irSpecRe!, _irSpecIm!);
        CooleyTukeyFft.Inverse(_outRe, _outIm);

        float[] outReR = new float[fftSize];
        float[] outImR = new float[fftSize];
        CooleyTukeyFft.MultiplyAccumulate(outReR, outImR, _inputRe, _inputIm, _irSpecReR!, _irSpecImR!);
        CooleyTukeyFft.Inverse(outReR, outImR);

        ref float overlapLRef = ref MemoryMarshal.GetArrayDataReference(_overlapL!);
        ref float overlapRRef = ref MemoryMarshal.GetArrayDataReference(_overlapR!);
        ref float outReRef2 = ref MemoryMarshal.GetArrayDataReference(_outRe);
        ref float outReRRef = ref MemoryMarshal.GetArrayDataReference(outReR);

        for (int i = 0; i < frames; i++)
        {
            float outL = Unsafe.Add(ref outReRef2, i) + Unsafe.Add(ref overlapLRef, i);
            float outR = Unsafe.Add(ref outReRRef, i) + Unsafe.Add(ref overlapRRef, i);
            Unsafe.Add(ref bufRef, offset + i * 2) = outL;
            Unsafe.Add(ref bufRef, offset + i * 2 + 1) = outR;
        }

        int tailLen = fftSize - frames;
        if (tailLen > 0)
        {
            for (int i = 0; i < tailLen; i++)
            {
                Unsafe.Add(ref overlapLRef, i) =
                    (i + frames < fftSize ? Unsafe.Add(ref outReRef2, i + frames) : 0)
                    + (i < fftSize - frames ? Unsafe.Add(ref overlapLRef, i + frames) : 0);
                Unsafe.Add(ref overlapRRef, i) =
                    (i + frames < fftSize ? Unsafe.Add(ref outReRRef, i + frames) : 0)
                    + (i < fftSize - frames ? Unsafe.Add(ref overlapRRef, i + frames) : 0);
            }
        }
    }

    public void Reset()
    {
        _pendingIr = null;
        _activeIr = null;
        _irSpecRe = null;
        _irSpecIm = null;
        _irSpecReR = null;
        _irSpecImR = null;
        _overlapL = null;
        _overlapR = null;
        Array.Clear(_inputRe);
        Array.Clear(_inputIm);
        Array.Clear(_outRe);
        Array.Clear(_outIm);
    }

    public void Dispose() => Reset();
}
