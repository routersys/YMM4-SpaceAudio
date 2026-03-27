using SpaceAudio.Services;
using SpaceAudio.ViewModels;
using System.Windows;

namespace SpaceAudio.Views;

public partial class ConfirmationDialogWindow : Window
{
    public ConfirmationDialogWindow(string message, string title)
    {
        InitializeComponent();
        DataContext = new ConfirmationDialogViewModel(message, title);
        ServiceLocator.WindowThemeService.Bind(this);
    }

    private void YesButton_Click(object sender, RoutedEventArgs e) => DialogResult = true;
    private void NoButton_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}
