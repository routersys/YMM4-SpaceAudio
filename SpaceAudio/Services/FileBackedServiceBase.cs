using System.IO;

namespace SpaceAudio.Services;

internal abstract class FileBackedServiceBase
{
    private readonly Lock _lock = new();
    private volatile bool _initialized;

    protected abstract string DirectoryPath { get; }

    protected void EnsureInitialized()
    {
        if (_initialized) return;
        lock (_lock)
        {
            if (_initialized) return;
            Directory.CreateDirectory(DirectoryPath);
            OnInitializing();
            _initialized = true;
        }
    }

    protected virtual void OnInitializing() { }

    protected Lock SyncRoot => _lock;
}
