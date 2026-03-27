using SpaceAudio.Enums;
using SpaceAudio.Interfaces;
using System.Windows;

namespace SpaceAudio.Services;

internal sealed class ToastManager(IToastPresenter presenter)
{
    public const double ToastWidth = 340;
    public const double ToastHeight = 72;
    private const double RightMargin = 14;
    private const double BottomMargin = 14;
    private const double StackSpacing = 6;
    private const int MaxVisible = 4;

    private readonly List<IToastHandle> _stack = [];

    internal void Push(NotificationSeverity severity, string message)
    {
        if (_stack.Count >= MaxVisible) return;
        var area = SystemParameters.WorkArea;
        double left = area.Right - ToastWidth - RightMargin;
        double top = area.Bottom - BottomMargin - (_stack.Count + 1) * (ToastHeight + StackSpacing);
        var handle = presenter.Show(severity, message, left, top);
        _stack.Add(handle);
        handle.Closed += (_, _) => { _stack.Remove(handle); RePosition(); };
    }

    private void RePosition()
    {
        var area = SystemParameters.WorkArea;
        double bottom = area.Bottom - BottomMargin;
        for (int i = 0; i < _stack.Count; i++)
        {
            double top = bottom - ToastHeight;
            _stack[i].AnimateTop(top);
            bottom = top - StackSpacing;
        }
    }
}
