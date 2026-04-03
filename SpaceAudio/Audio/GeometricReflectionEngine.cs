using SpaceAudio.Audio.Bvh;
using SpaceAudio.Enums;
using SpaceAudio.Models;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;

namespace SpaceAudio.Audio;

internal sealed class GeometricReflectionEngine : IDisposable
{
    private const int MaxReflections = 32;
    private const float SpeedOfSound = 343.0f;
    private const float HeadRadius = 0.085f;

    private readonly DelayLine _delayL;
    private readonly DelayLine _delayR;

    private readonly int[] _delaySamplesL = GC.AllocateArray<int>(MaxReflections, pinned: true);
    private readonly int[] _delaySamplesR = GC.AllocateArray<int>(MaxReflections, pinned: true);
    private readonly float[] _gainsL = GC.AllocateArray<float>(MaxReflections, pinned: true);
    private readonly float[] _gainsR = GC.AllocateArray<float>(MaxReflections, pinned: true);
    private readonly float[] _samplesL = GC.AllocateArray<float>(MaxReflections, pinned: true);
    private readonly float[] _samplesR = GC.AllocateArray<float>(MaxReflections, pinned: true);
    private readonly ReflectionTapFilter[] _tapFiltersL = new ReflectionTapFilter[MaxReflections];
    private readonly ReflectionTapFilter[] _tapFiltersR = new ReflectionTapFilter[MaxReflections];

    private readonly FaceBvhTree _bvh = new();
    private int _activeCount;
    private int _sampleRate;
    private ReverbQuality _quality;

    public GeometricReflectionEngine(int maxDelaySamples)
    {
        _delayL = new DelayLine(maxDelaySamples);
        _delayR = new DelayLine(maxDelaySamples);
    }

    public void Configure(RoomGeometry? geometry, in RoomSnapshot snapshot, int sampleRate)
    {
        _sampleRate = sampleRate;
        _activeCount = 0;
        _quality = snapshot.Quality;

        ResetTapFilters();

        if (geometry is not null && geometry.Faces.Length > 0)
        {
            if (_quality != ReverbQuality.Economy)
                _bvh.Build(geometry);
            ConfigureFromGeometry(geometry, in snapshot);
        }
        else
        {
            ConfigureFromRectangular(in snapshot);
        }
    }

    private void ConfigureFromGeometry(RoomGeometry geometry, in RoomSnapshot snapshot)
    {
        var planes = geometry.GetPlanes();
        var source = new GeometryVertex(snapshot.SourceX, snapshot.SourceY, snapshot.SourceZ);
        var listener = new GeometryVertex(snapshot.ListenerX, snapshot.ListenerY, snapshot.ListenerZ);

        for (int i = 0; i < planes.Length && _activeCount < MaxReflections; i++)
        {
            ref readonly var plane = ref planes[i];
            if (MathF.Abs(plane.Nx) < 1e-8f && MathF.Abs(plane.Ny) < 1e-8f && MathF.Abs(plane.Nz) < 1e-8f)
                continue;

            var imageSource = plane.ReflectPoint(in source);
            TryAddReflection(imageSource, listener, plane.Absorption, plane.SpectralDamping, 1, i);
        }

        if (_quality == ReverbQuality.Economy) return;

        for (int i = 0; i < planes.Length && _activeCount < MaxReflections; i++)
        {
            ref readonly var p1 = ref planes[i];
            var img1 = p1.ReflectPoint(in source);

            for (int j = 0; j < planes.Length && _activeCount < MaxReflections; j++)
            {
                if (j == i) continue;
                ref readonly var p2 = ref planes[j];

                var img2 = p2.ReflectPoint(in img1);

                float combinedAbsorption = 1.0f - (1.0f - p1.Absorption) * (1.0f - p2.Absorption);
                float combinedDamping = Math.Clamp(p1.SpectralDamping + p2.SpectralDamping * 0.5f, 0.0f, 0.995f);

                bool validPath = !_bvh.IsOccluded(in img2, in listener, j);
                if (!validPath) continue;

                TryAddReflection(img2, listener, combinedAbsorption, combinedDamping, 2, -1);
            }
        }
    }

    private void ConfigureFromRectangular(in RoomSnapshot snap)
    {
        float sx = snap.SourceX, sy = snap.SourceY, sz = snap.SourceZ;
        float lx = snap.ListenerX, ly = snap.ListenerY, lz = snap.ListenerZ;
        float w = snap.Width, h = snap.Height, d = snap.Depth;
        float wallAbs = snap.WallAbsorption;
        float floorAbs = snap.FloorAbsorption;
        float ceilAbs = snap.CeilingAbsorption;
        float wallDamp = snap.WallSpectralDamping;
        float floorDamp = snap.FloorSpectralDamping;
        float ceilDamp = snap.CeilingSpectralDamping;

        var listener = new GeometryVertex(lx, ly, lz);

        TryAddReflection(new(-sx, sy, sz), listener, wallAbs, wallDamp, 1, -1);
        TryAddReflection(new(2 * w - sx, sy, sz), listener, wallAbs, wallDamp, 1, -1);
        TryAddReflection(new(sx, sy, -sz), listener, wallAbs, wallDamp, 1, -1);
        TryAddReflection(new(sx, sy, 2 * d - sz), listener, wallAbs, wallDamp, 1, -1);
        TryAddReflection(new(sx, -sy, sz), listener, floorAbs, floorDamp, 1, -1);
        TryAddReflection(new(sx, 2 * h - sy, sz), listener, ceilAbs, ceilDamp, 1, -1);

        if (_quality == ReverbQuality.Economy) return;

        float w2abs = 1.0f - (1.0f - wallAbs) * (1.0f - wallAbs);
        float w2damp = Math.Clamp(wallDamp * 1.4f, 0.0f, 0.995f);
        TryAddReflection(new(-sx, sy, -sz), listener, w2abs, w2damp, 2, -1);
        TryAddReflection(new(-sx, sy, 2 * d - sz), listener, w2abs, w2damp, 2, -1);
        TryAddReflection(new(2 * w - sx, sy, -sz), listener, w2abs, w2damp, 2, -1);
        TryAddReflection(new(2 * w - sx, sy, 2 * d - sz), listener, w2abs, w2damp, 2, -1);

        float wfAbs = 1.0f - (1.0f - wallAbs) * (1.0f - floorAbs);
        float wfDamp = Math.Clamp((wallDamp + floorDamp) * 0.6f, 0.0f, 0.995f);
        TryAddReflection(new(-sx, -sy, sz), listener, wfAbs, wfDamp, 2, -1);
        TryAddReflection(new(2 * w - sx, -sy, sz), listener, wfAbs, wfDamp, 2, -1);
    }

    private void TryAddReflection(GeometryVertex imageSource, GeometryVertex listener,
        float absorption, float spectralDamping, int order, int excludeFace)
    {
        if (_activeCount >= MaxReflections) return;

        float dx = imageSource.X - listener.X;
        float dy = imageSource.Y - listener.Y;
        float dz = imageSource.Z - listener.Z;
        float distSq = dx * dx + dy * dy + dz * dz;
        if (distSq < 0.0001f) return;

        float distance = MathF.Sqrt(distSq);
        float normDx = dx / distance;
        float normDy = dy / distance;

        float attenuation = (1.0f - absorption) / (1.0f + distance * 0.15f);
        if (order >= 2) attenuation *= 0.7f;
        if (attenuation < 0.001f) return;

        int idx = _activeCount;

        if (_quality != ReverbQuality.Economy)
            ComputeBinauralParameters(imageSource, listener, normDx, normDy, distance, attenuation, idx);
        else
            ComputeSimplePanning(distance, normDx, attenuation, idx);

        if (_delaySamplesL[idx] < 1 || _delaySamplesL[idx] >= _delayL.MaxDelay - 1) return;
        if (_delaySamplesR[idx] < 1 || _delaySamplesR[idx] >= _delayR.MaxDelay - 1) return;

        _tapFiltersL[idx].SetCoefficient(spectralDamping, distance);
        _tapFiltersR[idx].SetCoefficient(spectralDamping, distance);

        _activeCount++;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ComputeBinauralParameters(GeometryVertex imageSource, GeometryVertex listener,
        float normDx, float normDy, float distance, float attenuation, int idx)
    {
        float leftEarX = listener.X - HeadRadius;
        float rightEarX = listener.X + HeadRadius;

        float dxL = imageSource.X - leftEarX;
        float dyL = imageSource.Y - listener.Y;
        float dzL = imageSource.Z - listener.Z;
        float distL = MathF.Sqrt(dxL * dxL + dyL * dyL + dzL * dzL);

        float dxR = imageSource.X - rightEarX;
        float distR = MathF.Sqrt(dxR * dxR + dyL * dyL + dzL * dzL);

        _delaySamplesL[idx] = Math.Max(1, (int)(distL / SpeedOfSound * _sampleRate));
        _delaySamplesR[idx] = Math.Max(1, (int)(distR / SpeedOfSound * _sampleRate));

        float angle = MathF.Asin(Math.Clamp(normDx, -1.0f, 1.0f));
        float ildFactor = 1.0f + 0.4f * MathF.Abs(angle) / (MathF.PI * 0.5f);

        float baseGain = attenuation / (1.0f + distance * 0.05f);

        if (normDx < 0)
        {
            _gainsL[idx] = baseGain;
            _gainsR[idx] = baseGain / ildFactor;
        }
        else
        {
            _gainsL[idx] = baseGain / ildFactor;
            _gainsR[idx] = baseGain;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ComputeSimplePanning(float distance, float normDx, float attenuation, int idx)
    {
        int samples = (int)(distance / SpeedOfSound * _sampleRate);
        _delaySamplesL[idx] = samples;
        _delaySamplesR[idx] = samples;

        float pan = Math.Clamp(normDx, -1.0f, 1.0f);
        _gainsL[idx] = attenuation * (0.5f - pan * 0.3f);
        _gainsR[idx] = attenuation * (0.5f + pan * 0.3f);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Process(float inputL, float inputR, out float outL, out float outR)
    {
        _delayL.Write(inputL);
        _delayR.Write(inputR);

        int posL = _delayL.CurrentWritePosition;
        int posR = _delayR.CurrentWritePosition;

        int count = _activeCount;

        ref int dsL = ref MemoryMarshal.GetArrayDataReference(_delaySamplesL);
        ref int dsR = ref MemoryMarshal.GetArrayDataReference(_delaySamplesR);
        ref float sL = ref MemoryMarshal.GetArrayDataReference(_samplesL);
        ref float sR = ref MemoryMarshal.GetArrayDataReference(_samplesR);

        for (int i = 0; i < count; i++)
        {
            Unsafe.Add(ref sL, i) = _delayL.ReadAt(Unsafe.Add(ref dsL, i), posL);
            Unsafe.Add(ref sR, i) = _delayR.ReadAt(Unsafe.Add(ref dsR, i), posR);
        }

        if (_quality != ReverbQuality.Economy)
        {
            for (int i = 0; i < count; i++)
            {
                Unsafe.Add(ref sL, i) = _tapFiltersL[i].Process(Unsafe.Add(ref sL, i));
                Unsafe.Add(ref sR, i) = _tapFiltersR[i].Process(Unsafe.Add(ref sR, i));
            }
        }

        outL = SimdDotProduct(_samplesL, _gainsL, count);
        outR = SimdDotProduct(_samplesR, _gainsR, count);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static float SimdDotProduct(float[] samples, float[] gains, int count)
    {
        if (count == 0) return 0.0f;

        ref float sampRef = ref MemoryMarshal.GetArrayDataReference(samples);
        ref float gainRef = ref MemoryMarshal.GetArrayDataReference(gains);

        float sum = 0.0f;

        if (Avx.IsSupported && count >= 8)
        {
            var accumL = Vector256<float>.Zero;
            int simdEnd = count & ~7;

            for (int i = 0; i < simdEnd; i += 8)
            {
                var s = Vector256.LoadUnsafe(ref Unsafe.Add(ref sampRef, i));
                var g = Vector256.LoadUnsafe(ref Unsafe.Add(ref gainRef, i));
                accumL = Vector256.Add(accumL, Vector256.Multiply(s, g));
            }

            var lower = accumL.GetLower();
            var upper = accumL.GetUpper();
            var combined = Vector128.Add(lower, upper);
            combined = Vector128.Add(combined, Vector128.Shuffle(combined, Vector128.Create(2, 3, 0, 1)));
            sum = combined[0] + combined[1];

            for (int i = simdEnd; i < count; i++)
                sum += Unsafe.Add(ref sampRef, i) * Unsafe.Add(ref gainRef, i);
        }
        else if (Sse.IsSupported && count >= 4)
        {
            var accumL = Vector128<float>.Zero;
            int simdEnd = count & ~3;

            for (int i = 0; i < simdEnd; i += 4)
            {
                var s = Vector128.LoadUnsafe(ref Unsafe.Add(ref sampRef, i));
                var g = Vector128.LoadUnsafe(ref Unsafe.Add(ref gainRef, i));
                accumL = Vector128.Add(accumL, Vector128.Multiply(s, g));
            }

            accumL = Vector128.Add(accumL, Vector128.Shuffle(accumL, Vector128.Create(2, 3, 0, 1)));
            sum = accumL[0] + accumL[1];

            for (int i = simdEnd; i < count; i++)
                sum += Unsafe.Add(ref sampRef, i) * Unsafe.Add(ref gainRef, i);
        }
        else
        {
            for (int i = 0; i < count; i++)
                sum += Unsafe.Add(ref sampRef, i) * Unsafe.Add(ref gainRef, i);
        }

        return sum;
    }

    private void ResetTapFilters()
    {
        for (int i = 0; i < MaxReflections; i++)
        {
            _tapFiltersL[i].Reset();
            _tapFiltersR[i].Reset();
        }
    }

    public void Reset()
    {
        _delayL.Reset();
        _delayR.Reset();
        ResetTapFilters();
    }

    public void Dispose()
    {
        _delayL.Dispose();
        _delayR.Dispose();
    }
}
