namespace SpaceAudio.ViewModels;

public sealed class InputDialogViewModel(string message, string title, string defaultText = "") : ViewModelBase
{
    private string _inputText = defaultText;

    public string Title { get; } = title;
    public string Message { get; } = message;

    public string InputText
    {
        get => _inputText;
        set => SetProperty(ref _inputText, value);
    }
}
