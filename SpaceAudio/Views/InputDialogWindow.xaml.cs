using SpaceAudio.Services;
using SpaceAudio.ViewModels;
using System.Windows;

namespace SpaceAudio.Views;

public partial class InputDialogWindow : Window
{
    public string InputText => ((InputDialogViewModel)DataContext).InputText;

    public InputDialogWindow(string message, string title, string defaultText = "")
    {
        InitializeComponent();
        DataContext = new InputDialogViewModel(message, title, defaultText);
        ServiceLocator.WindowThemeService.Bind(this);
        InputTextBox.Focus();
        InputTextBox.SelectAll();
    }

    private void OkButton_Click(object sender, RoutedEventArgs e) => DialogResult = true;
    private void CancelButton_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}
