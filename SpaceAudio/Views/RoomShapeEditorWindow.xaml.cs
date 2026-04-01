using SpaceAudio.Models;
using SpaceAudio.Rendering;
using SpaceAudio.Services;
using SpaceAudio.ViewModels;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace SpaceAudio.Views;

public partial class RoomShapeEditorWindow : Window
{
    private RoomShapeEditorViewModel ViewModel => (RoomShapeEditorViewModel)DataContext;
    private readonly Camera3D _camera = new();
    private ThemePalette _palette;
    private bool _isDragging;
    private bool _isRightDragging;
    private Point _lastMousePos;
    private bool _needsRedraw = true;

    public RoomGeometry? ResultGeometry { get; private set; }

    public RoomShapeEditorWindow()
    {
        InitializeComponent();
        DataContext = new RoomShapeEditorViewModel();
        ServiceLocator.WindowThemeService.Bind(this);
        _palette = ThemePalette.Detect(Colors.Black);

        ViewModel.GeometryChanged += (_, _) => _needsRedraw = true;
        ViewModel.RequestClose += (_, _) =>
        {
            ResultGeometry = ViewModel.Geometry.Clone();
            DialogResult = true;
        };

        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    public void SetGeometry(RoomGeometry geometry)
    {
        ViewModel.Geometry = geometry.Clone();
        var center = geometry.CalculateCenter();
        _camera.Reset(center.X, center.Y, center.Z);
        _needsRedraw = true;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        UpdateTheme();
        CompositionTarget.Rendering += OnRenderFrame;
        var center = ViewModel.Geometry.CalculateCenter();
        _camera.Reset(center.X, center.Y, center.Z);
        _needsRedraw = true;
    }

    private void OnUnloaded(object sender, RoutedEventArgs e) =>
        CompositionTarget.Rendering -= OnRenderFrame;

    private void OnRenderFrame(object? sender, EventArgs e)
    {
        if (!IsLoaded || !IsVisible || !_needsRedraw) return;
        _needsRedraw = false;
        DrawPreview();
    }

    private void DrawPreview()
    {
        if (PreviewHost.ActualWidth <= 0 || PreviewHost.ActualHeight <= 0) return;
        var geo = ViewModel.Geometry;
        var snap = new RoomSnapshot(
            geo.Vertices.Length > 0 ? geo.Vertices.Max(v => v.X) : 8,
            geo.Vertices.Length > 0 ? geo.Vertices.Max(v => v.Y) : 3,
            geo.Vertices.Length > 0 ? geo.Vertices.Max(v => v.Z) : 6,
            0, 0, 0, 0, 0, 0, 0, 1.5f, 0.5f, 0.7f, -3, -6, 0.3f,
            0.12f, 0.1f, 0.12f,
            SpaceAudio.Enums.ReverbQuality.Standard,
            SpaceAudio.Enums.RoomShape.Custom,
            SpaceAudio.Enums.WallMaterial.Drywall,
            SpaceAudio.Enums.WallMaterial.Wood,
            SpaceAudio.Enums.WallMaterial.Drywall,
            geo);
        PreviewHost.Render(_camera, _palette, snap, PreviewHost.ActualWidth, PreviewHost.ActualHeight, false, false);
    }

    private void UpdateTheme()
    {
        var bg = (SystemColors.WindowBrush as SolidColorBrush)?.Color ?? Colors.Black;
        _palette = ThemePalette.Detect(bg);
    }

    private void Preview_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _isDragging = true;
        _lastMousePos = e.GetPosition(PreviewCanvas);
        PreviewCanvas.CaptureMouse();
    }

    private void Preview_MouseMove(object sender, MouseEventArgs e)
    {
        var pos = e.GetPosition(PreviewCanvas);
        double dx = pos.X - _lastMousePos.X;
        double dy = pos.Y - _lastMousePos.Y;
        if (_isDragging) { _camera.Rotate((float)(dx * 0.5), (float)(dy * 0.4)); _needsRedraw = true; }
        else if (_isRightDragging) { _camera.Pan((float)dx, (float)dy); _needsRedraw = true; }
        _lastMousePos = pos;
    }

    private void Preview_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        _isDragging = false;
        PreviewCanvas.ReleaseMouseCapture();
    }

    private void Preview_MouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        _isRightDragging = true;
        _lastMousePos = e.GetPosition(PreviewCanvas);
        PreviewCanvas.CaptureMouse();
    }

    private void Preview_MouseRightButtonUp(object sender, MouseButtonEventArgs e)
    {
        _isRightDragging = false;
        PreviewCanvas.ReleaseMouseCapture();
    }

    private void Preview_MouseWheel(object sender, MouseWheelEventArgs e)
    {
        _camera.ZoomByDelta(e.Delta);
        _needsRedraw = true;
        e.Handled = true;
    }
}
