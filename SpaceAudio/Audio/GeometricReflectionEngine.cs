using SpaceAudio.Models;
using System.Runtime.CompilerServices;

namespace SpaceAudio.Audio;

internal sealed class GeometricReflectionEngine : IDisposable
{
    private const int MaxReflections = 32;
    private const float SpeedOfSound = 343.0f;

    private readonly DelayLine _delayL;
    private readonly DelayLine _delayR;
    private readonly int[] _delaySamplesL = new int[MaxReflections];
    private readonly int[] _delaySamplesR = new int[MaxReflections];
    private readonly float[] _gainsL = new float[MaxReflections];
    private readonly float[] _gainsR = new float[MaxReflections];
    private int _activeCount;
    private int _sampleRate;

    public GeometricReflectionEngine(int maxDelaySamples)
    {
        _delayL = new DelayLine(maxDelaySamples);
        _delayR = new DelayLine(maxDelaySamples);
    }

    public void Configure(RoomGeometry? geometry, in RoomSnapshot snapshot, int sampleRate)
    {
        _sampleRate = sampleRate;
        _activeCount = 0;

        if (geometry is null || geometry.Faces.Length == 0)
        {
            ConfigureFallback(in snapshot);
            return;
        }

        var planes = geometry.GetPlanes();
        var source = new GeometryVertex(snapshot.SourceX, snapshot.SourceY, snapshot.SourceZ);
        var listener = new GeometryVertex(snapshot.ListenerX, snapshot.ListenerY, snapshot.ListenerZ);

        for (int i = 0; i < planes.Length && _activeCount < MaxReflections; i++)
        {
            ref readonly var plane = ref planes[i];
            if (MathF.Abs(plane.Nx) < 1e-8f && MathF.Abs(plane.Ny) < 1e-8f && MathF.Abs(plane.Nz) < 1e-8f)
                continue;

            var imageSource = plane.ReflectPoint(in source);
            float dx = imageSource.X - listener.X;
            float dy = imageSource.Y - listener.Y;
            float dz = imageSource.Z - listener.Z;
            float distSq = dx * dx + dy * dy + dz * dz;
            if (distSq < 0.0001f) continue;

            float distance = MathF.Sqrt(distSq);
            float delaySec = distance / SpeedOfSound;
            int samples = (int)(delaySec * _sampleRate);
            if (samples < 1 || samples >= _delayL.MaxDelay - 1) continue;

            float attenuation = (1.0f - plane.Absorption) / (1.0f + distance * 0.15f);
            float normDx = dx / distance;
            float pan = Math.Clamp(normDx, -1.0f, 1.0f);

            int idx = _activeCount++;
            _delaySamplesL[idx] = samples;
            _delaySamplesR[idx] = samples;
            _gainsL[idx] = attenuation * (0.5f - pan * 0.3f);
            _gainsR[idx] = attenuation * (0.5f + pan * 0.3f);
        }

        for (int i = 0; i < planes.Length && _activeCount < MaxReflections; i++)
        {
            for (int j = i + 1; j < planes.Length && _activeCount < MaxReflections; j++)
            {
                ref readonly var p1 = ref planes[i];
                ref readonly var p2 = ref planes[j];
                var img1 = p1.ReflectPoint(in source);
                var img2 = p2.ReflectPoint(new GeometryVertex(img1.X, img1.Y, img1.Z));

                float dx = img2.X - listener.X;
                float dy = img2.Y - listener.Y;
                float dz = img2.Z - listener.Z;
                float distSq = dx * dx + dy * dy + dz * dz;
                if (distSq < 0.0001f) continue;

                float distance = MathF.Sqrt(distSq);
                float delaySec = distance / SpeedOfSound;
                int samples = (int)(delaySec * _sampleRate);
                if (samples < 1 || samples >= _delayL.MaxDelay - 1) continue;

                float attenuation = (1.0f - p1.Absorption) * (1.0f - p2.Absorption) / (1.0f + distance * 0.2f);
                if (attenuation < 0.001f) continue;

                float normDx = dx / distance;
                float pan = Math.Clamp(normDx, -1.0f, 1.0f);

                int idx = _activeCount++;
                _delaySamplesL[idx] = samples;
                _delaySamplesR[idx] = samples;
                _gainsL[idx] = attenuation * (0.5f - pan * 0.3f) * 0.7f;
                _gainsR[idx] = attenuation * (0.5f + pan * 0.3f) * 0.7f;
            }
        }
    }

    private void ConfigureFallback(in RoomSnapshot snap)
    {
        float sx = snap.SourceX, sy = snap.SourceY, sz = snap.SourceZ;
        float lx = snap.ListenerX, ly = snap.ListenerY, lz = snap.ListenerZ;
        float w = snap.Width, h = snap.Height, d = snap.Depth;
        float wallAbs = snap.WallAbsorption;
        float floorAbs = snap.FloorAbsorption;
        float ceilAbs = snap.CeilingAbsorption;

        AddReflection(-sx, sy, sz, lx, ly, lz, wallAbs);
        AddReflection(2 * w - sx, sy, sz, lx, ly, lz, wallAbs);
        AddReflection(sx, sy, -sz, lx, ly, lz, wallAbs);
        AddReflection(sx, sy, 2 * d - sz, lx, ly, lz, wallAbs);
        AddReflection(sx, -sy, sz, lx, ly, lz, floorAbs);
        AddReflection(sx, 2 * h - sy, sz, lx, ly, lz, ceilAbs);
    }

    private void AddReflection(float ix, float iy, float iz, float lx, float ly, float lz, float absorption)
    {
        if (_activeCount >= MaxReflections) return;
        float dx = ix - lx, dy = iy - ly, dz = iz - lz;
        float distSq = dx * dx + dy * dy + dz * dz;
        if (distSq < 0.0001f) return;
        float distance = MathF.Sqrt(distSq);
        float delaySec = distance / SpeedOfSound;
        int samples = (int)(delaySec * _sampleRate);
        if (samples < 1 || samples >= _delayL.MaxDelay - 1) return;
        float attenuation = (1.0f - absorption) / (1.0f + distance * 0.15f);
        float normDx = dx / distance;
        float pan = Math.Clamp(normDx, -1.0f, 1.0f);
        int idx = _activeCount++;
        _delaySamplesL[idx] = samples;
        _delaySamplesR[idx] = samples;
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
        for (int i = 0; i < _activeCount; i++)
        {
            outL += _delayL.ReadAt(_delaySamplesL[i], posL) * _gainsL[i];
            outR += _delayR.ReadAt(_delaySamplesR[i], posR) * _gainsR[i];
        }
    }

    public void Reset()
    {
        _delayL.Reset();
        _delayR.Reset();
    }

    public void Dispose()
    {
        _delayL.Dispose();
        _delayR.Dispose();
    }
}
