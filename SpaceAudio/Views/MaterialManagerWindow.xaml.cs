using SpaceAudio.Services;
using SpaceAudio.ViewModels;
using System.Windows;

namespace SpaceAudio.Views;

public partial class MaterialManagerWindow : Window
{
    public MaterialManagerWindow()
    {
        InitializeComponent();
        DataContext = new MaterialManagerViewModel();
        ServiceLocator.WindowThemeService.Bind(this);
    }
}
