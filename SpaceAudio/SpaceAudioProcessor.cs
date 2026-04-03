using SpaceAudio.Audio;
using SpaceAudio.Enums;
using SpaceAudio.Models;
using System.Runtime.CompilerServices;
using YukkuriMovieMaker.Player.Audio.Effects;

namespace SpaceAudio;

internal sealed class SpaceAudioProcessor : AudioEffectProcessorBase
{
    private readonly SpaceAudioEffect _item;

    private GeometricReflectionEngine? _geoEngine;
    private FeedbackDelayNetwork? _fdn;
    private DelayLine? _preDelayL;
    private DelayLine? _preDelayR;
    private LowPassOnePoleCascade? _hfFilterL;
    private LowPassOnePoleCascade? _hfFilterR;
    private OutputLimiter? _limiter;
    private StereoWidener? _widener;

    private RoomSnapshot _lastSnapshot;
    private int _lastHz;
    private bool _configured;

    private float _cachedEarlyGain;
    private float _cachedLateGain;
    private float _cachedWet;
    private float _cachedDry;
    private int _cachedPreDelaySamples;

    public override int Hz => Input?.Hz ?? 0;
    public override long Duration => Input?.Duration ?? 0;

    public SpaceAudioProcessor(SpaceAudioEffect item, TimeSpan duration)
    {
        _item = item;
    }

    protected override int read(float[] destBuffer, int offset, int count)
    {
        if (Input is null) return 0;

        int readCount = Input.Read(destBuffer, offset, count);
        if (readCount <= 0) return readCount;

        int frames = readCount / 2;
        long startFrame = Position / 2;
        long totalFrames = Duration / 2;
        int hz = Hz;

        EnsureInitialized(hz);

        if (HasAnimation())
            ProcessAnimated(destBuffer, offset, frames, startFrame, totalFrames, hz);
        else
        {
            var snapshot = _item.CreateSnapshot(startFrame, totalFrames, hz);
            EnsureConfigured(in snapshot, hz);
            ProcessStaticBlock(destBuffer, offset, frames);
        }

        return readCount;
    }

    private void ProcessAnimated(float[] buffer, int offset, int frames, long startFrame, long totalFrames, int hz)
    {
        for (int i = 0; i < frames; i++)
        {
            long currentFrame = startFrame + i;
            var snapshot = _item.CreateSnapshot(currentFrame, totalFrames, hz);
            EnsureConfigured(in snapshot, hz);

            int idx = offset + i * 2;
            ProcessSingleFrame(buffer, idx);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ProcessStaticBlock(float[] buffer, int offset, int frames)
    {
        float earlyGain = _cachedEarlyGain;
        float lateGain = _cachedLateGain;
        float wet = _cachedWet;
        float dry = _cachedDry;
        int preDelaySamples = _cachedPreDelaySamples;

        for (int i = 0; i < frames; i++)
        {
            int idx = offset + i * 2;
            float inL = buffer[idx];
            float inR = buffer[idx + 1];

            float delayedL = _preDelayL!.Process(inL, preDelaySamples);
            float delayedR = _preDelayR!.Process(inR, preDelaySamples);

            _geoEngine!.Process(delayedL, delayedR, out float earlyL, out float earlyR);
            _fdn!.Process(delayedL, delayedR, out float lateL, out float lateR);

            lateL = _hfFilterL!.Process(lateL);
            lateR = _hfFilterR!.Process(lateR);

            float wetL = earlyL * earlyGain + lateL * lateGain;
            float wetR = earlyR * earlyGain + lateR * lateGain;

            buffer[idx] = SoftClipper.Process(inL * dry + wetL * wet);
            buffer[idx + 1] = SoftClipper.Process(inR * dry + wetR * wet);

            _widener!.Process(ref buffer[idx], ref buffer[idx + 1]);
            _limiter!.Process(ref buffer[idx], ref buffer[idx + 1]);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ProcessSingleFrame(float[] buffer, int idx)
    {
        float inL = buffer[idx];
        float inR = buffer[idx + 1];

        float delayedL = _preDelayL!.Process(inL, _cachedPreDelaySamples);
        float delayedR = _preDelayR!.Process(inR, _cachedPreDelaySamples);

        _geoEngine!.Process(delayedL, delayedR, out float earlyL, out float earlyR);
        _fdn!.Process(delayedL, delayedR, out float lateL, out float lateR);

        lateL = _hfFilterL!.Process(lateL);
        lateR = _hfFilterR!.Process(lateR);

        float wetL = earlyL * _cachedEarlyGain + lateL * _cachedLateGain;
        float wetR = earlyR * _cachedEarlyGain + lateR * _cachedLateGain;

        buffer[idx] = SoftClipper.Process(inL * _cachedDry + wetL * _cachedWet);
        buffer[idx + 1] = SoftClipper.Process(inR * _cachedDry + wetR * _cachedWet);

        _widener!.Process(ref buffer[idx], ref buffer[idx + 1]);
        _limiter!.Process(ref buffer[idx], ref buffer[idx + 1]);
    }

    private void EnsureConfigured(in RoomSnapshot snapshot, int hz)
    {
        if (snapshot == _lastSnapshot && _configured) return;

        _geoEngine!.Configure(snapshot.Geometry, in snapshot, hz);
        _fdn!.Configure(in snapshot, hz);

        float hfCutoff = 0.45f * (1.0f - snapshot.HfDamping * 0.8f);
        _hfFilterL!.SetCutoff(hfCutoff);
        _hfFilterR!.SetCutoff(hfCutoff);

        _cachedEarlyGain = MathF.Pow(10.0f, snapshot.EarlyLevel / 20.0f);
        _cachedLateGain = MathF.Pow(10.0f, snapshot.LateLevel / 20.0f);
        _cachedWet = snapshot.DryWetMix;
        _cachedDry = 1.0f - snapshot.DryWetMix;
        _cachedPreDelaySamples = Math.Clamp((int)(snapshot.PreDelayMs * 0.001f * hz), 0, _preDelayL!.MaxDelay - 1);

        float stereoWidth = snapshot.Quality switch
        {
            ReverbQuality.High => 1.2f,
            ReverbQuality.Standard => 1.0f,
            _ => 0.8f
        };
        _widener!.SetWidth(stereoWidth);

        _lastSnapshot = snapshot;
        _configured = true;
    }

    private void EnsureInitialized(int hz)
    {
        if (_lastHz == hz && _geoEngine is not null) return;

        _geoEngine?.Dispose();
        _fdn?.Dispose();
        _preDelayL?.Dispose();
        _preDelayR?.Dispose();
        _hfFilterL?.Dispose();
        _hfFilterR?.Dispose();

        int maxPreDelay = (int)(0.2f * hz) + 256;
        int maxEarlyDelay = (int)(0.1f * hz) + 256;

        _preDelayL = new DelayLine(maxPreDelay);
        _preDelayR = new DelayLine(maxPreDelay);
        _geoEngine = new GeometricReflectionEngine(maxEarlyDelay);
        _fdn = new FeedbackDelayNetwork(hz);
        _hfFilterL = new LowPassOnePoleCascade(0.4f);
        _hfFilterR = new LowPassOnePoleCascade(0.4f);
        _limiter = new OutputLimiter(hz);
        _widener = new StereoWidener();
        _lastHz = hz;
        _configured = false;
    }

    private bool HasAnimation()
    {
        return _item.RoomWidth.Values.Count > 1
            || _item.RoomHeight.Values.Count > 1
            || _item.RoomDepth.Values.Count > 1
            || _item.SourceX.Values.Count > 1
            || _item.SourceY.Values.Count > 1
            || _item.SourceZ.Values.Count > 1
            || _item.ListenerX.Values.Count > 1
            || _item.ListenerY.Values.Count > 1
            || _item.ListenerZ.Values.Count > 1
            || _item.PreDelayMs.Values.Count > 1
            || _item.DecayTime.Values.Count > 1
            || _item.HfDamping.Values.Count > 1
            || _item.Diffusion.Values.Count > 1
            || _item.EarlyLevel.Values.Count > 1
            || _item.LateLevel.Values.Count > 1
            || _item.DryWetMix.Values.Count > 1;
    }

    protected override void seek(long position)
    {
        Input?.Seek(position);
        _preDelayL?.Reset();
        _preDelayR?.Reset();
        _geoEngine?.Reset();
        _fdn?.Reset();
        _hfFilterL?.Reset();
        _hfFilterR?.Reset();
        _limiter?.Reset();
        _configured = false;
    }
}
