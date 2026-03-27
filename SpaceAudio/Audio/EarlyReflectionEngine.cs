using SpaceAudio.Enums;
using SpaceAudio.Models;
using System.Runtime.CompilerServices;

namespace SpaceAudio.Audio;

internal sealed class EarlyReflectionEngine : IDisposable
{
    private const int MaxReflections = 24;
    private const float SpeedOfSound = 343.0f;

    private readonly DelayLine _delayL;
    private readonly DelayLine _delayR;
    private readonly int[] _delaySamplesL = new int[MaxReflections];
    private readonly int[] _delaySamplesR = new int[MaxReflections];
    private readonly float[] _gainsL = new float[MaxReflections];
    private readonly float[] _gainsR = new float[MaxReflections];
    private int _activeCount;
    private int _sampleRate;

    public EarlyReflectionEngine(int maxDelaySamples)
    {
        _delayL = new DelayLine(maxDelaySamples);
        _delayR = new DelayLine(maxDelaySamples);
    }

    public void Configure(in RoomSnapshot snapshot, int sampleRate)
    {
        _sampleRate = sampleRate;
        _activeCount = 0;
        GenerateFirstOrderReflections(in snapshot);
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

    private void GenerateFirstOrderReflections(in RoomSnapshot snap)
    {
        float sx = snap.SourceX, sy = snap.SourceY, sz = snap.SourceZ;
        float lx = snap.ListenerX, ly = snap.ListenerY, lz = snap.ListenerZ;
        float w = snap.Width, h = snap.Height, d = snap.Depth;
        float wallAbs = snap.WallAbsorption;
        float floorAbs = snap.FloorAbsorption;
        float ceilAbs = snap.CeilingAbsorption;

        if (snap.Shape == RoomShape.Rectangular)
        {
            AddReflection(-sx, sy, sz, lx, ly, lz, wallAbs);
            AddReflection(2 * w - sx, sy, sz, lx, ly, lz, wallAbs);
            AddReflection(sx, sy, -sz, lx, ly, lz, wallAbs);
            AddReflection(sx, sy, 2 * d - sz, lx, ly, lz, wallAbs);
            AddReflection(sx, -sy, sz, lx, ly, lz, floorAbs);
            AddReflection(sx, 2 * h - sy, sz, lx, ly, lz, ceilAbs);
            AddReflection(-sx, sy, -sz, lx, ly, lz, wallAbs * 0.8f);
            AddReflection(-sx, sy, 2 * d - sz, lx, ly, lz, wallAbs * 0.8f);
            AddReflection(2 * w - sx, sy, -sz, lx, ly, lz, wallAbs * 0.8f);
            AddReflection(2 * w - sx, sy, 2 * d - sz, lx, ly, lz, wallAbs * 0.8f);
            AddReflection(-sx, -sy, sz, lx, ly, lz, (wallAbs + floorAbs) * 0.4f);
            AddReflection(2 * w - sx, -sy, sz, lx, ly, lz, (wallAbs + floorAbs) * 0.4f);
        }
        else if (snap.Shape == RoomShape.Cathedral)
        {
            float hc = h * 1.5f;
            AddReflection(-sx, sy, sz, lx, ly, lz, wallAbs);
            AddReflection(2 * w - sx, sy, sz, lx, ly, lz, wallAbs);
            AddReflection(sx, sy, -sz, lx, ly, lz, wallAbs);
            AddReflection(sx, sy, 2 * d - sz, lx, ly, lz, wallAbs);
            AddReflection(sx, -sy, sz, lx, ly, lz, floorAbs);
            AddReflection(sx, 2 * hc - sy, sz, lx, ly, lz, ceilAbs * 0.5f);
            AddReflection(-sx, sy, -sz, lx, ly, lz, wallAbs * 0.7f);
            AddReflection(-sx, sy, 2 * d - sz, lx, ly, lz, wallAbs * 0.7f);
            AddReflection(2 * w - sx, sy, -sz, lx, ly, lz, wallAbs * 0.7f);
            AddReflection(2 * w - sx, sy, 2 * d - sz, lx, ly, lz, wallAbs * 0.7f);
            AddReflection(w * 0.5f, hc, d * 0.5f, lx, ly, lz, ceilAbs * 0.3f);
            AddReflection(w * 0.5f, hc, d * 1.5f, lx, ly, lz, ceilAbs * 0.3f);
        }
        else if (snap.Shape == RoomShape.LShaped)
        {
            AddReflection(-sx, sy, sz, lx, ly, lz, wallAbs);
            AddReflection(2 * w - sx, sy, sz, lx, ly, lz, wallAbs);
            AddReflection(sx, sy, -sz, lx, ly, lz, wallAbs);
            AddReflection(sx, sy, 2 * d - sz, lx, ly, lz, wallAbs);
            AddReflection(sx, -sy, sz, lx, ly, lz, floorAbs);
            AddReflection(sx, 2 * h - sy, sz, lx, ly, lz, ceilAbs);
            AddReflection(w * 0.5f, sy, d * 0.5f, lx, ly, lz, wallAbs * 0.6f);
            AddReflection(-sx, sy, -sz, lx, ly, lz, wallAbs * 0.7f);
            AddReflection(2 * w - sx, sy, 2 * d - sz, lx, ly, lz, wallAbs * 0.7f);
        }
        else if (snap.Shape == RoomShape.Studio)
        {
            AddReflection(-sx, sy, sz, lx, ly, lz, wallAbs * 1.5f);
            AddReflection(2 * w - sx, sy, sz, lx, ly, lz, wallAbs * 1.5f);
            AddReflection(sx, sy, -sz, lx, ly, lz, wallAbs * 1.5f);
            AddReflection(sx, sy, 2 * d - sz, lx, ly, lz, wallAbs * 1.5f);
            AddReflection(sx, -sy, sz, lx, ly, lz, floorAbs * 1.2f);
            AddReflection(sx, 2 * h - sy, sz, lx, ly, lz, ceilAbs * 1.5f);
        }
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

    public void Dispose()
    {
        _delayL.Dispose();
        _delayR.Dispose();
    }
}
