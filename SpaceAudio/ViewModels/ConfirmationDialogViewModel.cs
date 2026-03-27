namespace SpaceAudio.ViewModels;

public sealed class ConfirmationDialogViewModel(string message, string title) : ViewModelBase
{
    public string Title { get; } = title;
    public string Message { get; } = message;
}
