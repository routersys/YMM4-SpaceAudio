namespace SpaceAudio.Interfaces;

public interface IResourceTracker : IDisposable
{
    T Track<T>(T resource) where T : IDisposable;
    void Release<T>(T resource) where T : IDisposable;
    int TrackedCount { get; }
}
