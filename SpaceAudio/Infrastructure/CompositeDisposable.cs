using System.ComponentModel;

namespace SpaceAudio.Infrastructure;

internal sealed class CompositeDisposable : IDisposable
{
    private readonly Lock _lock = new();
    private readonly List<IDisposable> _disposables = [];
    private volatile bool _disposed;

    public void Add(IDisposable disposable)
    {
        ArgumentNullException.ThrowIfNull(disposable);
        lock (_lock)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            _disposables.Add(disposable);
        }
    }

    public IDisposable Subscribe(INotifyPropertyChanged source, PropertyChangedEventHandler handler)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(handler);
        source.PropertyChanged += handler;
        var subscription = new ActionDisposable(() => source.PropertyChanged -= handler);
        Add(subscription);
        return subscription;
    }

    public void Clear()
    {
        List<IDisposable> snapshot;
        lock (_lock)
        {
            snapshot = [.. _disposables];
            _disposables.Clear();
        }
        foreach (var d in snapshot) d.Dispose();
    }

    public void Dispose()
    {
        if (_disposed) return;
        lock (_lock)
        {
            if (_disposed) return;
            _disposed = true;
        }
        Clear();
    }

    private sealed class ActionDisposable(Action action) : IDisposable
    {
        private Action? _action = action;
        public void Dispose() { Interlocked.Exchange(ref _action, null)?.Invoke(); }
    }
}
