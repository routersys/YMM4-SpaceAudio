using System.Buffers;

namespace SpaceAudio.Audio;

internal sealed class AudioBufferPool
{
    private readonly ArrayPool<float> _pool = ArrayPool<float>.Create();

    public float[] Rent(int minimumLength) => _pool.Rent(minimumLength);
    public void Return(float[] buffer) => _pool.Return(buffer);
}
