using SpaceAudio.Models;

namespace SpaceAudio.Audio;

internal static class ImpulseResponseExporter
{
    public static float[] GenerateIR(RoomSnapshot snapshot, int sampleRate, float lengthSeconds = 2.0f)
    {
        int totalSamples = (int)(sampleRate * lengthSeconds);
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
        int preDelaySamples = Math.Clamp((int)(snapshot.PreDelayMs * 0.001f * sampleRate), 0, maxPreDelay - 1);

        for (int i = 0; i < totalSamples; i++)
        {
            float impulse = i == 0 ? 1.0f : 0.0f;

            float delayedL = preDelayL.Process(impulse, preDelaySamples);
            float delayedR = preDelayR.Process(impulse, preDelaySamples);

            geoEngine.Process(delayedL, delayedR, out float earlyL, out float earlyR);
            fdn.Process(delayedL, delayedR, out float lateL, out float lateR);

            lateL = hfL.Process(lateL);
            lateR = hfR.Process(lateR);

            irL[i] = earlyL * earlyGain + lateL * lateGain;
            irR[i] = earlyR * earlyGain + lateR * lateGain;
        }

        float[] stereo = new float[totalSamples * 2];
        for (int i = 0; i < totalSamples; i++)
        {
            stereo[i * 2] = irL[i];
            stereo[i * 2 + 1] = irR[i];
        }
        return stereo;
    }
}
