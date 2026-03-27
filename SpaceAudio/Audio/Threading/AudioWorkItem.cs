namespace SpaceAudio.Audio.Threading;

internal struct AudioWorkItem
{
    public float[] Buffer;
    public int Offset;
    public int Count;
    public Action<float[], int, int>? Callback;
    public int IsReady;
}
