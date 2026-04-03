using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace SpaceAudio.Audio.Convolution;

internal static class CooleyTukeyFft
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int NextPowerOf2(int n)
    {
        int p = 1;
        while (p < n) p <<= 1;
        return p;
    }

    public static void Forward(float[] re, float[] im)
    {
        int n = re.Length;
        BitReversal(re, im, n);
        Butterfly(re, im, n, forward: true);
    }

    public static void Inverse(float[] re, float[] im)
    {
        int n = re.Length;
        BitReversal(re, im, n);
        Butterfly(re, im, n, forward: false);

        float invN = 1.0f / n;
        ref float reRef = ref MemoryMarshal.GetArrayDataReference(re);
        ref float imRef = ref MemoryMarshal.GetArrayDataReference(im);
        for (int i = 0; i < n; i++)
        {
            Unsafe.Add(ref reRef, i) *= invN;
            Unsafe.Add(ref imRef, i) *= invN;
        }
    }

    private static void BitReversal(float[] re, float[] im, int n)
    {
        int bits = BitOperations.Log2((uint)n);
        ref float reRef = ref MemoryMarshal.GetArrayDataReference(re);
        ref float imRef = ref MemoryMarshal.GetArrayDataReference(im);

        for (int i = 0; i < n; i++)
        {
            int rev = BitReverse(i, bits);
            if (rev > i)
            {
                (Unsafe.Add(ref reRef, i), Unsafe.Add(ref reRef, rev)) =
                    (Unsafe.Add(ref reRef, rev), Unsafe.Add(ref reRef, i));
                (Unsafe.Add(ref imRef, i), Unsafe.Add(ref imRef, rev)) =
                    (Unsafe.Add(ref imRef, rev), Unsafe.Add(ref imRef, i));
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int BitReverse(int x, int bits)
    {
        int result = 0;
        for (int i = 0; i < bits; i++)
        {
            result = (result << 1) | (x & 1);
            x >>= 1;
        }
        return result;
    }

    private static void Butterfly(float[] re, float[] im, int n, bool forward)
    {
        ref float reRef = ref MemoryMarshal.GetArrayDataReference(re);
        ref float imRef = ref MemoryMarshal.GetArrayDataReference(im);
        float sign = forward ? -1.0f : 1.0f;

        for (int len = 2; len <= n; len <<= 1)
        {
            int half = len >> 1;
            float angle = sign * MathF.PI / half;
            float wRe = MathF.Cos(angle);
            float wIm = MathF.Sin(angle);

            for (int i = 0; i < n; i += len)
            {
                float curRe = 1.0f, curIm = 0.0f;
                for (int j = 0; j < half; j++)
                {
                    int u = i + j, v = i + j + half;
                    ref float reU = ref Unsafe.Add(ref reRef, u);
                    ref float imU = ref Unsafe.Add(ref imRef, u);
                    ref float reV = ref Unsafe.Add(ref reRef, v);
                    ref float imV = ref Unsafe.Add(ref imRef, v);

                    float tRe = curRe * reV - curIm * imV;
                    float tIm = curRe * imV + curIm * reV;

                    reV = reU - tRe;
                    imV = imU - tIm;
                    reU += tRe;
                    imU += tIm;

                    float nextRe = curRe * wRe - curIm * wIm;
                    curIm = curRe * wIm + curIm * wRe;
                    curRe = nextRe;
                }
            }
        }
    }

    public static void MultiplyAccumulate(float[] outRe, float[] outIm,
        float[] aRe, float[] aIm, float[] bRe, float[] bIm)
    {
        int n = outRe.Length;
        ref float outReRef = ref MemoryMarshal.GetArrayDataReference(outRe);
        ref float outImRef = ref MemoryMarshal.GetArrayDataReference(outIm);
        ref float aReRef = ref MemoryMarshal.GetArrayDataReference(aRe);
        ref float aImRef = ref MemoryMarshal.GetArrayDataReference(aIm);
        ref float bReRef = ref MemoryMarshal.GetArrayDataReference(bRe);
        ref float bImRef = ref MemoryMarshal.GetArrayDataReference(bIm);

        for (int i = 0; i < n; i++)
        {
            float ar = Unsafe.Add(ref aReRef, i), ai = Unsafe.Add(ref aImRef, i);
            float br = Unsafe.Add(ref bReRef, i), bi = Unsafe.Add(ref bImRef, i);
            Unsafe.Add(ref outReRef, i) = ar * br - ai * bi;
            Unsafe.Add(ref outImRef, i) = ar * bi + ai * br;
        }
    }
}
