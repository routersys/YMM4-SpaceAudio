using SpaceAudio.Audio;
using SpaceAudio.Audio.Convolution;
using SpaceAudio.Enums;
using SpaceAudio.Models;
using System.Runtime.CompilerServices;
using YukkuriMovieMaker.Player.Audio.Effects;

namespace SpaceAudio;

internal sealed class SpaceAudioProcessor : AudioEffectProcessorBase
{
    private const float PreDelaySmoothTimeSeconds = 0.05f;
    private const float SpeedOfSound = 343.0f;
    private const int MaxBlockFrames = 8192;
    private const int TimelineFps = 30;

    private readonly SpaceAudioEffect _item;
    private readonly TimeSpan _itemDuration;
    private readonly float[] _dryBuffer = GC.AllocateArray<float>(MaxBlockFrames * 2, pinned: true);

    private GeometricReflectionEngine? _geoEngine;
    private FeedbackDelayNetwork? _fdn;
    private DelayLine? _preDelayL;
    private DelayLine? _preDelayR;
    private LowPassOnePoleCascade? _hfFilterL;
    private LowPassOnePoleCascade? _hfFilterR;
    private OutputLimiter? _limiter;
    private StereoWidener? _widener;
    private OlaConvolver? _convolver;
    private AsyncIrPipeline? _irPipeline;

    private RoomSnapshot _lastSnapshot;
    private int _lastHz;
    private bool _configured;

    private float _cachedEarlyGain;
    private float _cachedLateGain;
    private float _cachedWet;
    private float _cachedDry;
    private float _targetPreDelaySamples;
    private float _currentPreDelaySamples;
    private float _preDelaySmooth;
    private ReverbQuality _cachedQuality;

    public override int Hz => Input?.Hz ?? 0;
    public override long Duration => Input?.Duration ?? 0;

    public SpaceAudioProcessor(SpaceAudioEffect item, TimeSpan duration)
    {
        _item = item;
        _itemDuration = duration;
    }

    protected override int read(float[] destBuffer, int offset, int count)
    {
        if (Input is null) return 0;

        int readCount = Input.Read(destBuffer, offset, count);
        if (readCount <= 0) return readCount;

        int frames = readCount / 2;
        long startFrame = Position / 2;
        int hz = Hz;
        long itemTotalSamples = (long)(_itemDuration.TotalSeconds * hz);
        long blockMs = hz > 0 ? (long)((double)readCount / 2 / hz * 1000.0) : 0L;

        long currentTimelineFrame = hz > 0 ? (long)((double)Position / 2 / hz * TimelineFps) : 0L;
        long totalTimelineFrames = (long)(_itemDuration.TotalSeconds * TimelineFps);
        Services.ServiceLocator.TimelineService.UpdateFromProcessor(currentTimelineFrame, totalTimelineFrames, TimelineFps, blockMs);

        EnsureInitialized(hz);

        var snapshot = _item.CreateSnapshot(startFrame, Math.Max(itemTotalSamples, 1L), hz);
        EnsureConfigured(in snapshot, hz);

        if (_cachedQuality == ReverbQuality.High && _convolver is not null && _convolver.HasActiveIr)
            ProcessConvolution(destBuffer, offset, frames);
        else
            ProcessStaticBlock(destBuffer, offset, frames);

        return readCount;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ProcessConvolution(float[] buffer, int offset, int frames)
    {
        if (frames > MaxBlockFrames) return;

        float dry = _cachedDry;
        float wet = _cachedWet;
        float targetPreDelay = _targetPreDelaySamples;
        float smooth = _preDelaySmooth;

        Buffer.BlockCopy(buffer, offset * sizeof(float), _dryBuffer, 0, frames * 2 * sizeof(float));

        for (int i = 0; i < frames; i++)
        {
            int idx = offset + i * 2;
            _currentPreDelaySamples += smooth * (targetPreDelay - _currentPreDelaySamples);
            if (MathF.Abs(targetPreDelay - _currentPreDelaySamples) < 1e-4f)
                _currentPreDelaySamples = targetPreDelay;

            buffer[idx] = _preDelayL!.ProcessInterpolated(buffer[idx], _currentPreDelaySamples);
            buffer[idx + 1] = _preDelayR!.ProcessInterpolated(buffer[idx + 1], _currentPreDelaySamples);
        }

        _convolver!.ProcessBlock(buffer, offset, frames);

        for (int i = 0; i < frames; i++)
        {
            int idx = offset + i * 2;
            float dryL = _dryBuffer[i * 2];
            float dryR = _dryBuffer[i * 2 + 1];
            buffer[idx] = SoftClipper.Process(dryL * dry + buffer[idx] * wet);
            buffer[idx + 1] = SoftClipper.Process(dryR * dry + buffer[idx + 1] * wet);
            _widener!.Process(ref buffer[idx], ref buffer[idx + 1]);
            _limiter!.Process(ref buffer[idx], ref buffer[idx + 1]);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ProcessStaticBlock(float[] buffer, int offset, int frames)
    {
        float earlyGain = _cachedEarlyGain;
        float lateGain = _cachedLateGain;
        float wet = _cachedWet;
        float dry = _cachedDry;
        float targetPreDelay = _targetPreDelaySamples;
        float smooth = _preDelaySmooth;

        for (int i = 0; i < frames; i++)
        {
            int idx = offset + i * 2;
            float inL = buffer[idx];
            float inR = buffer[idx + 1];

            _currentPreDelaySamples += smooth * (targetPreDelay - _currentPreDelaySamples);
            if (MathF.Abs(targetPreDelay - _currentPreDelaySamples) < 1e-4f)
                _currentPreDelaySamples = targetPreDelay;

            float delayedL = _preDelayL!.ProcessInterpolated(inL, _currentPreDelaySamples);
            float delayedR = _preDelayR!.ProcessInterpolated(inR, _currentPreDelaySamples);

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
    private void EnsureConfigured(in RoomSnapshot snapshot, int hz)
    {
        if (snapshot == _lastSnapshot && _configured) return;

        bool firstConfigure = !_configured;

        _geoEngine!.Configure(snapshot.Geometry, in snapshot, hz);
        _fdn!.Configure(in snapshot, hz);

        float hfCutoff = 0.45f * (1.0f - snapshot.HfDamping * 0.8f);
        _hfFilterL!.SetCutoff(hfCutoff);
        _hfFilterR!.SetCutoff(hfCutoff);

        _cachedEarlyGain = MathF.Pow(10.0f, snapshot.EarlyLevel / 20.0f);
        _cachedLateGain = MathF.Pow(10.0f, snapshot.LateLevel / 20.0f);
        _cachedWet = snapshot.DryWetMix;
        _cachedDry = 1.0f - snapshot.DryWetMix;
        _cachedQuality = snapshot.Quality;

        float directDist = RoomAcousticsCalculator.CalculateDirectDistance(in snapshot);
        float physicsDelaySamples = directDist / SpeedOfSound * hz;
        float userDelaySamples = snapshot.PreDelayMs * 0.001f * hz;
        _targetPreDelaySamples = Math.Clamp(
            physicsDelaySamples + userDelaySamples,
            2.0f,
            _preDelayL!.MaxDelay - 2);

        if (firstConfigure)
            _currentPreDelaySamples = _targetPreDelaySamples;

        float stereoWidth = snapshot.Quality switch
        {
            ReverbQuality.High => 1.2f,
            ReverbQuality.Standard => 1.0f,
            _ => 0.8f
        };
        _widener!.SetWidth(stereoWidth);

        if (snapshot.Quality == ReverbQuality.High && _irPipeline is not null)
            _irPipeline.Submit(in snapshot, hz);

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
        _convolver?.Dispose();
        _irPipeline?.Dispose();

        int maxPreDelay = (int)(0.5f * hz) + 256;
        int maxEarlyDelay = (int)(0.1f * hz) + 256;

        _preDelayL = new DelayLine(maxPreDelay);
        _preDelayR = new DelayLine(maxPreDelay);
        _geoEngine = new GeometricReflectionEngine(maxEarlyDelay);
        _fdn = new FeedbackDelayNetwork(hz);
        _hfFilterL = new LowPassOnePoleCascade(0.4f);
        _hfFilterR = new LowPassOnePoleCascade(0.4f);
        _limiter = new OutputLimiter(hz);
        _widener = new StereoWidener();
        _convolver = new OlaConvolver();
        _irPipeline = new AsyncIrPipeline(_convolver);
        _preDelaySmooth = 1.0f - MathF.Exp(-1.0f / (PreDelaySmoothTimeSeconds * hz));
        _currentPreDelaySamples = 1.0f;

        _lastHz = hz;
        _configured = false;
    }

    protected override void seek(long position)
    {
        Input?.Seek(position);
        long frame = _lastHz > 0 ? (long)((double)position / 2 / _lastHz * TimelineFps) : 0L;
        Services.ServiceLocator.TimelineService.NotifySeek(frame);
        _preDelayL?.Reset();
        _preDelayR?.Reset();
        _geoEngine?.Reset();
        _fdn?.Reset();
        _hfFilterL?.Reset();
        _hfFilterR?.Reset();
        _limiter?.Reset();
        _convolver?.Reset();
        _currentPreDelaySamples = 1.0f;
        _configured = false;
    }
}
