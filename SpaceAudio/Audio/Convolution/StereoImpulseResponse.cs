namespace SpaceAudio.Audio.Convolution;

internal sealed class StereoImpulseResponse
{
    public readonly float[] Left;
    public readonly float[] Right;
    public readonly int Length;

    public StereoImpulseResponse(float[] left, float[] right)
    {
        Left = left;
        Right = right;
        Length = left.Length;
    }
}
