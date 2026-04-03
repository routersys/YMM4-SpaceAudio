using System.Collections.Concurrent;
using System.Runtime.CompilerServices;

namespace SpaceAudio.Infrastructure;

internal sealed class ObjectPool<T>(Func<T> factory, Action<T>? reset = null, int maxSize = 64)
{
    private readonly ConcurrentBag<T> _bag = [];
    private int _count;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public T Rent()
    {
        if (_bag.TryTake(out T? item))
        {
            Interlocked.Decrement(ref _count);
            return item;
        }
        return factory();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Return(T item)
    {
        if (Volatile.Read(ref _count) >= maxSize) return;
        reset?.Invoke(item);
        _bag.Add(item);
        Interlocked.Increment(ref _count);
    }

    public PooledItem RentScoped() => new(this, Rent());

    public readonly ref struct PooledItem(ObjectPool<T> pool, T item)
    {
        public T Value { get; } = item;
        public void Dispose() => pool.Return(Value);
    }
}
