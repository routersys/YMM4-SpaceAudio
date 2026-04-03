namespace SpaceAudio.Infrastructure;

public sealed class EditScope : IDisposable
{
    private Action? _endAction;

    private EditScope(Action beginAction, Action endAction)
    {
        _endAction = endAction;
        beginAction();
    }

    public static EditScope Begin(Action beginAction, Action endAction)
    {
        ArgumentNullException.ThrowIfNull(beginAction);
        ArgumentNullException.ThrowIfNull(endAction);
        return new(beginAction, endAction);
    }

    public void Dispose() => Interlocked.Exchange(ref _endAction, null)?.Invoke();
}
