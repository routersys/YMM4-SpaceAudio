using SpaceAudio.Interfaces;

namespace SpaceAudio.Infrastructure;

internal sealed class ResourceTracker : IResourceTracker
{
    private readonly Lock _lock = new();
    private readonly HashSet<IDisposable> _resources = [];
    private volatile bool _disposed;

    public int TrackedCount
    {
        get
        {
            lock (_lock) return _resources.Count;
        }
    }

    public T Track<T>(T resource) where T : IDisposable
    {
        ArgumentNullException.ThrowIfNull(resource);
        lock (_lock)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            _resources.Add(resource);
        }
        return resource;
    }

    public void Release<T>(T resource) where T : IDisposable
    {
        ArgumentNullException.ThrowIfNull(resource);
        lock (_lock) _resources.Remove(resource);
        resource.Dispose();
    }

    public void Dispose()
    {
        if (_disposed) return;
        lock (_lock)
        {
            if (_disposed) return;
            _disposed = true;
            foreach (var r in _resources) r.Dispose();
            _resources.Clear();
        }
    }
}
