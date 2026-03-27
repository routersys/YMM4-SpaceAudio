using SpaceAudio.Enums;
using SpaceAudio.Interfaces;
using SpaceAudio.Services;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Threading;

namespace SpaceAudio.Views;

internal sealed class ToastWindow : Window, IToastHandle
{
    private static readonly TimeSpan AutoDismiss = TimeSpan.FromSeconds(4);
    private static readonly TimeSpan SlideIn = TimeSpan.FromMilliseconds(280);
    private static readonly TimeSpan SlideOut = TimeSpan.FromMilliseconds(220);
    private static readonly CubicEase EaseOut = new() { EasingMode = EasingMode.EaseOut };
    private static readonly CubicEase EaseIn = new() { EasingMode = EasingMode.EaseIn };

    private readonly DispatcherTimer _timer;
    private double _targetLeft;
    private bool _dismissing;

    internal ToastWindow(NotificationSeverity severity, string message)
    {
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        Topmost = true;
        ShowInTaskbar = false;
        ResizeMode = ResizeMode.NoResize;
        Width = ToastManager.ToastWidth;
        Height = ToastManager.ToastHeight;
        Content = BuildContent(severity, message);
        _timer = new DispatcherTimer(DispatcherPriority.Normal, Dispatcher) { Interval = AutoDismiss };
        _timer.Tick += (_, _) => Dismiss();
        MouseEnter += (_, _) => _timer.Stop();
        MouseLeave += (_, _) => { if (!_dismissing) _timer.Start(); };
    }

    public void ShowAt(double targetLeft, double targetTop)
    {
        _targetLeft = targetLeft;
        Left = SystemParameters.WorkArea.Right + 20;
        Top = targetTop;
        Show();
        BeginAnimation(LeftProperty, new DoubleAnimation(targetLeft, SlideIn) { EasingFunction = EaseOut });
        _timer.Start();
    }

    public void AnimateTop(double targetTop) =>
        BeginAnimation(TopProperty, new DoubleAnimation(Top, targetTop, TimeSpan.FromMilliseconds(240)) { EasingFunction = EaseOut });

    private void Dismiss()
    {
        if (_dismissing) return;
        _dismissing = true;
        _timer.Stop();
        var fade = new DoubleAnimation(1, 0, SlideOut);
        fade.Completed += (_, _) => Close();
        BeginAnimation(LeftProperty, new DoubleAnimation(_targetLeft, SystemParameters.WorkArea.Right + 20, SlideOut) { EasingFunction = EaseIn });
        BeginAnimation(OpacityProperty, fade);
    }

    private UIElement BuildContent(NotificationSeverity severity, string message)
    {
        var (bg, fg) = severity switch
        {
            NotificationSeverity.Error => (Frozen(Color.FromRgb(185, 28, 28)), Frozen(Colors.White)),
            NotificationSeverity.Warning => (Frozen(Color.FromRgb(180, 83, 9)), Frozen(Colors.White)),
            _ => (Frozen(Color.FromRgb(29, 78, 216)), Frozen(Colors.White))
        };

        var border = new Border
        {
            Background = bg,
            CornerRadius = new CornerRadius(7),
            Padding = new Thickness(14, 10, 10, 10),
            Effect = new DropShadowEffect { BlurRadius = 10, ShadowDepth = 3, Opacity = 0.28, Direction = 270 }
        };

        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var text = new TextBlock { Text = message, Foreground = fg, TextWrapping = TextWrapping.Wrap, FontSize = 12.5, VerticalAlignment = VerticalAlignment.Center };
        Grid.SetColumn(text, 0);

        var close = new Button { Content = "\u2715", Foreground = fg, Background = Brushes.Transparent, BorderThickness = new Thickness(0), Cursor = Cursors.Hand, VerticalAlignment = VerticalAlignment.Top };
        close.Click += (_, _) => Dismiss();
        var ct = new ControlTemplate(typeof(Button));
        ct.VisualTree = new FrameworkElementFactory(typeof(ContentPresenter));
        close.Template = ct;
        Grid.SetColumn(close, 1);

        grid.Children.Add(text);
        grid.Children.Add(close);
        border.Child = grid;
        return border;
    }

    private static SolidColorBrush Frozen(Color c) { var b = new SolidColorBrush(c); b.Freeze(); return b; }
}
