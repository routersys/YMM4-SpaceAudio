using SpaceAudio.Enums;
using SpaceAudio.Models;
using System.Runtime.CompilerServices;

namespace SpaceAudio.Audio;

internal sealed class FeedbackDelayNetwork : IDisposable
{
    private const int Lines = 8;
    private const float ReferenceRate = 48000.0f;
    private const float SpeedOfSound = 343.0f;

    private static readonly int[] FallbackPrimeDelays = [1433, 1601, 1787, 1933, 2143, 2293, 2467, 2647];
    private static readonly float InvSqrt8 = 1.0f / MathF.Sqrt(Lines);

    private static readonly (int P, int Q, int R)[] RoomModes =
    [
        (1, 0, 0), (0, 1, 0), (0, 0, 1),
        (1, 1, 0), (1, 0, 1), (0, 1, 1),
        (1, 1, 1), (2, 0, 0)
    ];

    private readonly DelayLine[] _delays = new DelayLine[Lines];
    private readonly DampingFilter[] _dampers = new DampingFilter[Lines];
    private readonly AllPassFilter[] _diffusers;
    private readonly float[] _feedbacks = new float[Lines];
    private readonly float[] _readOut = new float[Lines];
    private readonly float[] _mixed = new float[Lines];
    private readonly int[] _scaledDelays = new int[Lines];
    private readonly float[] _lineDampingCoeffs = new float[Lines];

    public FeedbackDelayNetwork(int sampleRate)
    {
        float ratio = sampleRate / ReferenceRate;

        for (int i = 0; i < Lines; i++)
        {
            int scaled = Math.Max(1, (int)(FallbackPrimeDelays[i] * ratio));
            _scaledDelays[i] = scaled;
            _delays[i] = new DelayLine(scaled * 4);
            _dampers[i] = new DampingFilter(0.3f);
            _feedbacks[i] = 0.84f;
            _lineDampingCoeffs[i] = 0.3f;
        }

        _diffusers =
        [
            new AllPassFilter(Math.Max(1, (int)(142 * ratio)), 0.5f),
            new AllPassFilter(Math.Max(1, (int)(107 * ratio)), 0.5f),
            new AllPassFilter(Math.Max(1, (int)(379 * ratio)), 0.5f),
            new AllPassFilter(Math.Max(1, (int)(277 * ratio)), 0.5f)
        ];
    }

    public void Configure(in RoomSnapshot snapshot, int sampleRate)
    {
        float rt60 = Math.Max(snapshot.DecayTime, 0.1f);
        float damping = Math.Clamp(snapshot.HfDamping, 0.0f, 0.999f);
        float diffusion = Math.Clamp(snapshot.Diffusion, 0.0f, 1.0f);

        float w = Math.Max(snapshot.Width, 0.5f);
        float h = Math.Max(snapshot.Height, 0.5f);
        float d = Math.Max(snapshot.Depth, 0.5f);

        if (snapshot.Quality != ReverbQuality.Economy)
            ComputeRoomModeDelays(w, h, d, sampleRate);
        else
            ComputeFallbackDelays(w, h, d, sampleRate);

        float invRt60 = -3.0f / rt60;
        float invSampleRate = 1.0f / sampleRate;

        float avgSpectralDamping = (snapshot.WallSpectralDamping + snapshot.FloorSpectralDamping + snapshot.CeilingSpectralDamping) / 3.0f;

        for (int i = 0; i < Lines; i++)
        {
            float delaySeconds = _scaledDelays[i] * invSampleRate;
            float fb = MathF.Pow(10.0f, invRt60 * delaySeconds);
            _feedbacks[i] = Math.Clamp(fb, 0.0f, 0.998f);

            float lineDamping = damping;
            if (snapshot.Quality == ReverbQuality.High)
            {
                float modeFreq = ComputeModeFrequency(RoomModes[i].P, RoomModes[i].Q, RoomModes[i].R, w, h, d);
                float freqFactor = Math.Clamp(modeFreq / 1000.0f, 0.1f, 3.0f);
                lineDamping = Math.Clamp(damping + avgSpectralDamping * freqFactor * 0.3f, 0.0f, 0.999f);
            }

            _lineDampingCoeffs[i] = lineDamping;
            _dampers[i].SetCoefficient(lineDamping);
        }

        float apGain = Math.Clamp(0.3f + diffusion * 0.4f, 0.0f, 0.99f);
        foreach (var ap in _diffusers)
            ap.SetGain(apGain);
    }

    private void ComputeRoomModeDelays(float w, float h, float d, int sampleRate)
    {
        for (int i = 0; i < Lines; i++)
        {
            float freq = ComputeModeFrequency(RoomModes[i].P, RoomModes[i].Q, RoomModes[i].R, w, h, d);
            freq = Math.Max(freq, 5.0f);
            int delay = (int)(sampleRate / freq);
            delay = EnsureCoprime(delay, i);
            _scaledDelays[i] = Math.Clamp(delay, 1, _delays[i].MaxDelay - 2);
        }
    }

    private void ComputeFallbackDelays(float w, float h, float d, int sampleRate)
    {
        float ratio = sampleRate / ReferenceRate;
        float roomScale = MathF.Cbrt(w * h * d) / 5.0f;
        roomScale = Math.Clamp(roomScale, 0.5f, 3.0f);

        for (int i = 0; i < Lines; i++)
        {
            int scaled = Math.Max(1, (int)(FallbackPrimeDelays[i] * ratio * roomScale));
            _scaledDelays[i] = Math.Clamp(scaled, 1, _delays[i].MaxDelay - 2);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static float ComputeModeFrequency(int p, int q, int r, float w, float h, float d)
    {
        float px = p / w;
        float qy = q / h;
        float rz = r / d;
        return SpeedOfSound * 0.5f * MathF.Sqrt(px * px + qy * qy + rz * rz);
    }

    private static int EnsureCoprime(int delay, int lineIndex)
    {
        int[] offsets = [0, 1, -1, 2, -2, 3, -3, 5];
        int candidate = delay + offsets[lineIndex % offsets.Length];
        return Math.Max(1, candidate);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Process(float inputL, float inputR, out float outL, out float outR)
    {
        float input = (inputL + inputR) * 0.5f;

        for (int i = 0; i < Lines; i++)
            _readOut[i] = _delays[i].Read(_scaledDelays[i]);

        HadamardMix(_readOut, _mixed);

        for (int i = 0; i < Lines; i++)
        {
            float damped = _dampers[i].Process(_mixed[i]);
            _delays[i].Write(input + damped * _feedbacks[i]);
        }

        float sumL = _readOut[0] + _readOut[2] + _readOut[4] + _readOut[6];
        float sumR = _readOut[1] + _readOut[3] + _readOut[5] + _readOut[7];

        outL = _diffusers[0].Process(_diffusers[1].Process(sumL)) * 0.25f;
        outR = _diffusers[2].Process(_diffusers[3].Process(sumR)) * 0.25f;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void HadamardMix(float[] input, float[] output)
    {
        float a = input[0] + input[4];
        float b = input[1] + input[5];
        float c = input[2] + input[6];
        float d = input[3] + input[7];
        float e = input[0] - input[4];
        float f = input[1] - input[5];
        float g = input[2] - input[6];
        float h = input[3] - input[7];

        output[0] = (a + c) * InvSqrt8;
        output[1] = (b + d) * InvSqrt8;
        output[2] = (a - c) * InvSqrt8;
        output[3] = (b - d) * InvSqrt8;
        output[4] = (e + g) * InvSqrt8;
        output[5] = (f + h) * InvSqrt8;
        output[6] = (e - g) * InvSqrt8;
        output[7] = (f - h) * InvSqrt8;
    }

    public void Reset()
    {
        foreach (var d in _delays) d.Reset();
        foreach (var d in _dampers) d.Reset();
        foreach (var d in _diffusers) d.Reset();
    }

    public void Dispose()
    {
        foreach (var d in _delays) d.Dispose();
        foreach (var d in _dampers) d.Dispose();
        foreach (var d in _diffusers) d.Dispose();
    }
}
