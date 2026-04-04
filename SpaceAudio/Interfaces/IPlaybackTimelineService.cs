using System.Runtime.CompilerServices;

namespace SpaceAudio.Interfaces;

public interface IPlaybackTimelineService
{
    long CurrentFrame { get; }
    long TotalFrames { get; }
    bool IsPlaying { get; }
    int Fps { get; }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    void UpdateFromProcessor(long currentFrame, long totalFrames, int fps, long blockMs);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    void NotifySeek(long currentFrame);
}
