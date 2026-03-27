using SpaceAudio.Enums;

namespace SpaceAudio.Interfaces;

public interface IToastPresenter
{
    IToastHandle Show(NotificationSeverity severity, string message, double left, double top);
}
