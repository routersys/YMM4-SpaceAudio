using SpaceAudio.Enums;
using SpaceAudio.Interfaces;

namespace SpaceAudio.Views;

public sealed class WpfToastPresenter : IToastPresenter
{
    public IToastHandle Show(NotificationSeverity severity, string message, double left, double top)
    {
        var window = new ToastWindow(severity, message);
        window.ShowAt(left, top);
        return window;
    }
}
