using SpaceAudio.Models;
using System.Threading.Channels;

namespace SpaceAudio.Audio.Convolution;

internal sealed class AsyncIrPipeline : IDisposable
{
    private const float IrLengthSeconds = 2.0f;

    private readonly OlaConvolver _convolver;
    private readonly Channel<(RoomSnapshot Snapshot, int SampleRate)> _channel;
    private readonly CancellationTokenSource _cts = new();
    private readonly Task _workerTask;

    public AsyncIrPipeline(OlaConvolver convolver)
    {
        _convolver = convolver;
        _channel = Channel.CreateBounded<(RoomSnapshot, int)>(new BoundedChannelOptions(1)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
            SingleWriter = false
        });
        _workerTask = Task.Factory.StartNew(WorkerAsync, _cts.Token,
            TaskCreationOptions.LongRunning, TaskScheduler.Default).Unwrap();
    }

    public void Submit(in RoomSnapshot snapshot, int sampleRate)
    {
        _channel.Writer.TryWrite((snapshot, sampleRate));
    }

    private async Task WorkerAsync()
    {
        await foreach (var (snapshot, sampleRate) in _channel.Reader.ReadAllAsync(_cts.Token))
        {
            try
            {
                var ir = GenerateStereoIr(snapshot, sampleRate);
                _convolver.SubmitIr(ir);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch
            {
            }
        }
    }

    private static StereoImpulseResponse GenerateStereoIr(in RoomSnapshot snapshot, int sampleRate)
    {
        int totalSamples = (int)(sampleRate * IrLengthSeconds);
        float[] irL = new float[totalSamples];
        float[] irR = new float[totalSamples];

        int maxPreDelay = (int)(0.2f * sampleRate) + 256;
        int maxEarlyDelay = (int)(0.1f * sampleRate) + 256;

        using var preDelayL = new DelayLine(maxPreDelay);
        using var preDelayR = new DelayLine(maxPreDelay);
        using var geoEngine = new GeometricReflectionEngine(maxEarlyDelay);
        using var fdn = new FeedbackDelayNetwork(sampleRate);
        using var hfL = new LowPassOnePoleCascade(0.4f);
        using var hfR = new LowPassOnePoleCascade(0.4f);

        geoEngine.Configure(snapshot.Geometry, in snapshot, sampleRate);
        fdn.Configure(in snapshot, sampleRate);

        float earlyGain = MathF.Pow(10.0f, snapshot.EarlyLevel / 20.0f);
        float lateGain = MathF.Pow(10.0f, snapshot.LateLevel / 20.0f);
        float hfCutoff = 0.45f * (1.0f - snapshot.HfDamping * 0.8f);
        hfL.SetCutoff(hfCutoff);
        hfR.SetCutoff(hfCutoff);

        int preDelaySamples = Math.Clamp((int)(snapshot.PreDelayMs * 0.001f * sampleRate), 0, maxPreDelay - 1);

        for (int i = 0; i < totalSamples; i++)
        {
            float impulse = i == 0 ? 1.0f : 0.0f;

            float dL = preDelayL.Process(impulse, preDelaySamples);
            float dR = preDelayR.Process(impulse, preDelaySamples);

            geoEngine.Process(dL, dR, out float earlyL, out float earlyR);
            fdn.Process(dL, dR, out float lateL, out float lateR);

            lateL = hfL.Process(lateL);
            lateR = hfR.Process(lateR);

            irL[i] = earlyL * earlyGain + lateL * lateGain;
            irR[i] = earlyR * earlyGain + lateR * lateGain;
        }

        return new StereoImpulseResponse(irL, irR);
    }

    public void Dispose()
    {
        _cts.Cancel();
        _channel.Writer.TryComplete();
        try { _workerTask.Wait(500); } catch { }
        _cts.Dispose();
    }
}
