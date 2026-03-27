using SpaceAudio.Services;
using SpaceAudio.ViewModels;
using System.Windows;

namespace SpaceAudio.Views;

public partial class SpaceAudioSettingsWindow : Window
{
    public SpaceAudioSettingsWindow()
    {
        InitializeComponent();
        DataContext = new SpaceAudioSettingsViewModel();
        ServiceLocator.WindowThemeService.Bind(this);
    }
}
