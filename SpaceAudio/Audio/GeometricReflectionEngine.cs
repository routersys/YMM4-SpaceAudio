using SpaceAudio.Enums;
using SpaceAudio.Models;
using System.Runtime.CompilerServices;

namespace SpaceAudio.Audio;

internal sealed class GeometricReflectionEngine : IDisposable
{
    private const int MaxReflections = 32;
    private const float SpeedOfSound = 343.0f;
    private const float HeadRadius = 0.085f;

    private readonly DelayLine _delayL;
    private readonly DelayLine _delayR;
    private readonly int[] _delaySamplesL = new int[MaxReflections];
    private readonly int[] _delaySamplesR = new int[MaxReflections];
    private readonly float[] _gainsL = new float[MaxReflections];
    private readonly float[] _gainsR = new float[MaxReflections];
    private readonly ReflectionTapFilter[] _tapFiltersL = new ReflectionTapFilter[MaxReflections];
    private readonly ReflectionTapFilter[] _tapFiltersR = new ReflectionTapFilter[MaxReflections];
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
            ConfigureFromGeometry(geometry, in snapshot);
        else
            ConfigureFromRectangular(in snapshot);
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
            TryAddReflection(imageSource, listener, plane.Absorption, plane.SpectralDamping, 1);
        }

        if (_quality != ReverbQuality.Economy)
        {
            for (int i = 0; i < planes.Length && _activeCount < MaxReflections; i++)
            {
                for (int j = i + 1; j < planes.Length && _activeCount < MaxReflections; j++)
                {
                    ref readonly var p1 = ref planes[i];
                    ref readonly var p2 = ref planes[j];
                    var img1 = p1.ReflectPoint(new GeometryVertex(snapshot.SourceX, snapshot.SourceY, snapshot.SourceZ));
                    var img2 = p2.ReflectPoint(new GeometryVertex(img1.X, img1.Y, img1.Z));

                    float combinedAbsorption = 1.0f - (1.0f - p1.Absorption) * (1.0f - p2.Absorption);
                    float combinedDamping = Math.Clamp(p1.SpectralDamping + p2.SpectralDamping * 0.5f, 0.0f, 0.995f);

                    TryAddReflection(img2, new GeometryVertex(snapshot.ListenerX, snapshot.ListenerY, snapshot.ListenerZ),
                        combinedAbsorption, combinedDamping, 2);
                }
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

        AddRectReflection(new(-sx, sy, sz), listener, wallAbs, wallDamp, 1);
        AddRectReflection(new(2 * w - sx, sy, sz), listener, wallAbs, wallDamp, 1);
        AddRectReflection(new(sx, sy, -sz), listener, wallAbs, wallDamp, 1);
        AddRectReflection(new(sx, sy, 2 * d - sz), listener, wallAbs, wallDamp, 1);
        AddRectReflection(new(sx, -sy, sz), listener, floorAbs, floorDamp, 1);
        AddRectReflection(new(sx, 2 * h - sy, sz), listener, ceilAbs, ceilDamp, 1);

        if (_quality != ReverbQuality.Economy)
        {
            float w2abs = 1.0f - (1.0f - wallAbs) * (1.0f - wallAbs);
            float w2damp = Math.Clamp(wallDamp * 1.4f, 0.0f, 0.995f);
            AddRectReflection(new(-sx, sy, -sz), listener, w2abs, w2damp, 2);
            AddRectReflection(new(-sx, sy, 2 * d - sz), listener, w2abs, w2damp, 2);
            AddRectReflection(new(2 * w - sx, sy, -sz), listener, w2abs, w2damp, 2);
            AddRectReflection(new(2 * w - sx, sy, 2 * d - sz), listener, w2abs, w2damp, 2);

            float wfAbs = 1.0f - (1.0f - wallAbs) * (1.0f - floorAbs);
            float wfDamp = Math.Clamp((wallDamp + floorDamp) * 0.6f, 0.0f, 0.995f);
            AddRectReflection(new(-sx, -sy, sz), listener, wfAbs, wfDamp, 2);
            AddRectReflection(new(2 * w - sx, -sy, sz), listener, wfAbs, wfDamp, 2);
        }
    }

    private void AddRectReflection(GeometryVertex imageSource, GeometryVertex listener, float absorption, float spectralDamping, int order)
    {
        TryAddReflection(imageSource, listener, absorption, spectralDamping, order);
    }

    private void TryAddReflection(GeometryVertex imageSource, GeometryVertex listener, float absorption, float spectralDamping, int order)
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
        float normDz = dz / distance;

        float attenuation = (1.0f - absorption) / (1.0f + distance * 0.15f);
        if (order >= 2) attenuation *= 0.7f;
        if (attenuation < 0.001f) return;

        int idx = _activeCount;

        if (_quality != ReverbQuality.Economy)
            ComputeBinauralParameters(imageSource, listener, normDx, distance, attenuation, idx);
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
        float normDx, float distance, float attenuation, int idx)
    {
        float leftEarX = listener.X - HeadRadius;
        float rightEarX = listener.X + HeadRadius;

        float dxL = imageSource.X - leftEarX;
        float dyL = imageSource.Y - listener.Y;
        float dzL = imageSource.Z - listener.Z;
        float distL = MathF.Sqrt(dxL * dxL + dyL * dyL + dzL * dzL);

        float dxR = imageSource.X - rightEarX;
        float distR = MathF.Sqrt(dxR * dxR + dyL * dyL + dzL * dzL);

        float delaySecL = distL / SpeedOfSound;
        float delaySecR = distR / SpeedOfSound;

        _delaySamplesL[idx] = Math.Max(1, (int)(delaySecL * _sampleRate));
        _delaySamplesR[idx] = Math.Max(1, (int)(delaySecR * _sampleRate));

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
        float delaySec = distance / SpeedOfSound;
        int samples = (int)(delaySec * _sampleRate);

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

        outL = 0;
        outR = 0;
        int count = _activeCount;

        if (_quality == ReverbQuality.Economy)
        {
            for (int i = 0; i < count; i++)
            {
                outL += _delayL.ReadAt(_delaySamplesL[i], posL) * _gainsL[i];
                outR += _delayR.ReadAt(_delaySamplesR[i], posR) * _gainsR[i];
            }
        }
        else
        {
            for (int i = 0; i < count; i++)
            {
                float rawL = _delayL.ReadAt(_delaySamplesL[i], posL);
                float rawR = _delayR.ReadAt(_delaySamplesR[i], posR);
                outL += _tapFiltersL[i].Process(rawL) * _gainsL[i];
                outR += _tapFiltersR[i].Process(rawR) * _gainsR[i];
            }
        }
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
