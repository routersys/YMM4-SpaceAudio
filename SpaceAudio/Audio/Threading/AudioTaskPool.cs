namespace SpaceAudio.Audio.Threading;

internal sealed class AudioTaskPool : IDisposable
{
    private readonly Thread[] _workers;
    private readonly AudioWorkItem[] _items;
    private readonly ManualResetEventSlim[] _signals;
    private readonly ManualResetEventSlim[] _completions;
    private volatile bool _shutdown;

    public int WorkerCount => _workers.Length;

    public AudioTaskPool(int workerCount)
    {
        _workers = new Thread[workerCount];
        _items = new AudioWorkItem[workerCount];
        _signals = new ManualResetEventSlim[workerCount];
        _completions = new ManualResetEventSlim[workerCount];

        for (int i = 0; i < workerCount; i++)
        {
            _signals[i] = new ManualResetEventSlim(false);
            _completions[i] = new ManualResetEventSlim(true);
            int idx = i;
            _workers[i] = new Thread(() => WorkerLoop(idx))
            {
                IsBackground = true,
                Priority = ThreadPriority.AboveNormal,
                Name = $"SpaceAudio-Worker-{i}"
            };
            _workers[i].Start();
        }
    }

    public void Submit(int workerIndex, float[] buffer, int offset, int count, Action<float[], int, int> callback)
    {
        _completions[workerIndex].Wait();
        _completions[workerIndex].Reset();
        _items[workerIndex] = new AudioWorkItem
        {
            Buffer = buffer,
            Offset = offset,
            Count = count,
            Callback = callback,
            IsReady = 1
        };
        _signals[workerIndex].Set();
    }

    public void WaitAll()
    {
        for (int i = 0; i < _workers.Length; i++)
            _completions[i].Wait();
    }

    private void WorkerLoop(int index)
    {
        while (!_shutdown)
        {
            _signals[index].Wait();
            _signals[index].Reset();

            if (_shutdown) break;

            ref var item = ref _items[index];
            if (Volatile.Read(ref item.IsReady) == 1)
            {
                item.Callback?.Invoke(item.Buffer, item.Offset, item.Count);
                Volatile.Write(ref item.IsReady, 0);
            }

            _completions[index].Set();
        }
    }

    public void Dispose()
    {
        _shutdown = true;
        foreach (var s in _signals) s.Set();
        foreach (var w in _workers) w.Join(500);
        foreach (var s in _signals) s.Dispose();
        foreach (var c in _completions) c.Dispose();
    }
}
