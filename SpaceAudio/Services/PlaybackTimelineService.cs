using SpaceAudio.Interfaces;
using System.Runtime.CompilerServices;

namespace SpaceAudio.Services;

internal sealed class PlaybackTimelineService : IPlaybackTimelineService
{
    private long _currentFrame;
    private long _totalFrames;
    private int _fps;
    private long _startTick;
    private long _expectedEndTick;

    public long CurrentFrame
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            long f0 = Interlocked.Read(ref _currentFrame);
            if (!IsPlaying) return f0;
            int fps = Volatile.Read(ref _fps);
            if (fps <= 0) return f0;
            long total = Interlocked.Read(ref _totalFrames);
            long elapsedMs = Environment.TickCount64 - Interlocked.Read(ref _startTick);
            if (elapsedMs <= 0L) return f0;
            return Math.Min(f0 + elapsedMs * fps / 1000L, total);
        }
    }

    public long TotalFrames
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => Interlocked.Read(ref _totalFrames);
    }

    public bool IsPlaying
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => Environment.TickCount64 <= Interlocked.Read(ref _expectedEndTick);
    }

    public int Fps
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => Volatile.Read(ref _fps);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void UpdateFromProcessor(long currentFrame, long totalFrames, int fps, long blockMs)
    {
        long now = Environment.TickCount64;
        Interlocked.Exchange(ref _currentFrame, currentFrame);
        Interlocked.Exchange(ref _totalFrames, Math.Max(totalFrames, 1L));
        Volatile.Write(ref _fps, fps);
        Interlocked.Exchange(ref _startTick, now);
        Interlocked.Exchange(ref _expectedEndTick, now + blockMs + 500L);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void NotifySeek(long currentFrame)
    {
        Interlocked.Exchange(ref _currentFrame, Math.Max(currentFrame, 0L));
        Interlocked.Exchange(ref _startTick, Environment.TickCount64);
        Interlocked.Exchange(ref _expectedEndTick, 0L);
    }
}
