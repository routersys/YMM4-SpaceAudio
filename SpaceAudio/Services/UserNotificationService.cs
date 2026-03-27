using SpaceAudio.Enums;
using SpaceAudio.Interfaces;
using SpaceAudio.Views;
using System.Windows;

namespace SpaceAudio.Services;

public sealed class UserNotificationService(IToastPresenter presenter) : IUserNotificationService
{
    private readonly ToastManager _toastManager = new(presenter);
    private readonly Lock _cooldownLock = new();
    private readonly Dictionary<string, DateTime> _cooldown = [];

    public void ShowError(string message) => TryDispatch(NotificationSeverity.Error, message, TimeSpan.FromSeconds(5));
    public void ShowWarning(string message) => TryDispatch(NotificationSeverity.Warning, message, TimeSpan.FromSeconds(3));
    public void ShowInfo(string message) => TryDispatch(NotificationSeverity.Info, message, TimeSpan.FromSeconds(2));

    public Task<bool> ConfirmAsync(string message, string title)
    {
        if (Application.Current.Dispatcher.CheckAccess())
            return Task.FromResult(ShowConfirm(message, title));
        return Application.Current.Dispatcher.InvokeAsync(() => ShowConfirm(message, title)).Task;
    }

    public Task<string?> PromptAsync(string message, string title, string defaultText = "")
    {
        if (Application.Current.Dispatcher.CheckAccess())
            return Task.FromResult(ShowPrompt(message, title, defaultText));
        return Application.Current.Dispatcher.InvokeAsync(() => ShowPrompt(message, title, defaultText)).Task;
    }

    private static bool ShowConfirm(string message, string title)
    {
        var dialog = new ConfirmationDialogWindow(message, title) { Owner = ResolveOwner() };
        return dialog.ShowDialog() == true;
    }

    private static string? ShowPrompt(string message, string title, string defaultText)
    {
        var dialog = new InputDialogWindow(message, title, defaultText) { Owner = ResolveOwner() };
        return dialog.ShowDialog() == true ? dialog.InputText : null;
    }

    private void TryDispatch(NotificationSeverity severity, string message, TimeSpan cooldown)
    {
        var now = DateTime.UtcNow;
        lock (_cooldownLock)
        {
            if (_cooldown.TryGetValue(message, out var last) && now - last < cooldown) return;
            _cooldown[message] = now;
        }
        Application.Current.Dispatcher.BeginInvoke(() => _toastManager.Push(severity, message));
    }

    private static Window ResolveOwner() =>
        Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive) ?? Application.Current.MainWindow;
}
