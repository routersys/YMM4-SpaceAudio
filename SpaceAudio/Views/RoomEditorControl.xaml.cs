using SpaceAudio.Models;
using SpaceAudio.Rendering;
using SpaceAudio.Services;
using SpaceAudio.ViewModels;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using YukkuriMovieMaker.Commons;

namespace SpaceAudio.Views;

public partial class RoomEditorControl : UserControl, IPropertyEditorControl
{
    private const double HitRadius = 14.0;

    private SpaceAudioEffect? _effect;
    private RoomEditorViewModel ViewModel => (RoomEditorViewModel)DataContext;

    private ThemePalette _palette;
    private readonly Camera3D _camera = new();
    private bool _isDragging;
    private bool _isRightDragging;
    private Point _lastMousePos;
    private bool _needsRedraw = true;
    private int _draggingTarget;

    private bool _isCompactMode;

    public event EventHandler? BeginEdit;
    public event EventHandler? EndEdit;

    public new SpaceAudioEffect? Effect
    {
        get => _effect;
        set
        {
            if (ReferenceEquals(_effect, value)) return;

            if (_effect is INotifyPropertyChanged oldNpc)
                oldNpc.PropertyChanged -= OnEffectPropertyChanged;

            _effect = value;

            if (_effect is INotifyPropertyChanged newNpc)
                newNpc.PropertyChanged += OnEffectPropertyChanged;

            if (_effect is not null)
                ViewModel.Effect = _effect;

            _needsRedraw = true;
        }
    }

    static RoomEditorControl()
    {
        try { ServiceLocator.RegisterToastPresenter(new WpfToastPresenter()); } catch { }
    }

    public RoomEditorControl()
    {
        InitializeComponent();
        DataContext = new RoomEditorViewModel();
        _palette = ThemePalette.Detect(Colors.Black);

        ViewModel.RequestRedraw += (_, _) => _needsRedraw = true;
        ViewModel.BeginEdit += (_, _) => BeginEdit?.Invoke(this, EventArgs.Empty);
        ViewModel.EndEdit += (_, _) => EndEdit?.Invoke(this, EventArgs.Empty);
        ViewModel.RequestOpenShapeEditor += (_, _) => OpenShapeEditor();
        ViewModel.RequestOpenMaterialManager += (_, _) => OpenMaterialManager();

        PresetToggleButton.Unchecked += async (_, _) =>
        {
            PresetToggleButton.IsHitTestVisible = false;
            await Task.Delay(200);
            PresetToggleButton.IsHitTestVisible = true;
        };

        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnEffectPropertyChanged(object? sender, PropertyChangedEventArgs e) =>
        _needsRedraw = true;

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        UpdateTheme();
        ResetCamera();
        CompositionTarget.Rendering += OnRenderFrame;
        _needsRedraw = true;
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        CompositionTarget.Rendering -= OnRenderFrame;
        if (_effect is INotifyPropertyChanged npc)
            npc.PropertyChanged -= OnEffectPropertyChanged;
    }

    private void OnRenderFrame(object? sender, EventArgs e)
    {
        if (!IsLoaded || !IsVisible || !_needsRedraw) return;
        EnforceConstraints();
        _needsRedraw = false;
        DrawAll();
    }

    private void EnforceConstraints()
    {
        if (_effect == null) return;

        float rw = _effect.RoomWidthValue;
        float rh = _effect.RoomHeightValue;
        float rd = _effect.RoomDepthValue;

        foreach (var k in _effect.SourceX.Values) if (k.Value > rw) k.Value = rw;
        foreach (var k in _effect.SourceY.Values) if (k.Value > rh) k.Value = rh;
        foreach (var k in _effect.SourceZ.Values) if (k.Value > rd) k.Value = rd;

        foreach (var k in _effect.ListenerX.Values) if (k.Value > rw) k.Value = rw;
        foreach (var k in _effect.ListenerY.Values) if (k.Value > rh) k.Value = rh;
        foreach (var k in _effect.ListenerZ.Values) if (k.Value > rd) k.Value = rd;
    }

    private void DrawAll()
    {
        if (VisualHost.ActualWidth <= 0 || VisualHost.ActualHeight <= 0) return;
        var snapshot = ViewModel.CreateSnapshotFromEffect();
        bool isSourceSelected = ViewModel.SelectedTarget?.GetType() == typeof(SpaceAudioEffect.SourceParameters);
        bool isListenerSelected = ViewModel.SelectedTarget?.GetType() == typeof(SpaceAudioEffect.ListenerParameters);
        VisualHost.Render(_camera, _palette, snapshot, VisualHost.ActualWidth, VisualHost.ActualHeight, isSourceSelected, isListenerSelected);
    }

    private void UpdateTheme()
    {
        if (CanvasBorder.Background is SolidColorBrush bg)
            _palette = ThemePalette.Detect(bg.Color);
    }

    private void ResetCamera()
    {
        if (_effect is null) { _camera.Reset(4, 1.5f, 3); return; }
        _camera.Reset(_effect.RoomWidthValue / 2, _effect.RoomHeightValue / 2, _effect.RoomDepthValue / 2);
    }

    private (double Sx, double Sy, double Lx, double Ly) GetMarkerScreenPositions()
    {
        if (_effect is null) return (0, 0, 0, 0);
        double w = VisualHost.ActualWidth;
        double h = VisualHost.ActualHeight;
        var viewMat = ProjectionMatrix.CreateViewMatrix(_camera);

        var sCam = System.Numerics.Vector3.Transform(new System.Numerics.Vector3(_effect.SourceXValue, _effect.SourceYValue, _effect.SourceZValue), viewMat);
        var lCam = System.Numerics.Vector3.Transform(new System.Numerics.Vector3(_effect.ListenerXValue, _effect.ListenerYValue, _effect.ListenerZValue), viewMat);

        var pSrc = ProjectionMatrix.ProjectToScreen(sCam, w, h);
        var pLis = ProjectionMatrix.ProjectToScreen(lCam, w, h);
        return (pSrc.X, pSrc.Y, pLis.X, pLis.Y);
    }

    private void Canvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        var pos = e.GetPosition(InteractionCanvas);

        if (_effect is not null)
        {
            var (sx, sy, lx, ly) = GetMarkerScreenPositions();
            double dSrc = Math.Sqrt((pos.X - sx) * (pos.X - sx) + (pos.Y - sy) * (pos.Y - sy));
            double dLis = Math.Sqrt((pos.X - lx) * (pos.X - lx) + (pos.Y - ly) * (pos.Y - ly));

            if (dSrc <= HitRadius && dSrc <= dLis)
            {
                if (ViewModel.SelectedTab != 0) ViewModel.SelectedTab = 0;
                ViewModel.SelectSourceTarget();
                _draggingTarget = 1;
                _lastMousePos = pos;
                InteractionCanvas.CaptureMouse();
                _needsRedraw = true;
                return;
            }
            if (dLis <= HitRadius)
            {
                if (ViewModel.SelectedTab != 0) ViewModel.SelectedTab = 0;
                ViewModel.SelectListenerTarget();
                _draggingTarget = 2;
                _lastMousePos = pos;
                InteractionCanvas.CaptureMouse();
                _needsRedraw = true;
                return;
            }

            ViewModel.SelectedTab = 0;
            ViewModel.SelectRoomTarget();
        }

        _draggingTarget = 0;
        _isDragging = true;
        _lastMousePos = pos;
        InteractionCanvas.CaptureMouse();
    }

    private void Canvas_MouseMove(object sender, MouseEventArgs e)
    {
        var pos = e.GetPosition(InteractionCanvas);
        double dx = pos.X - _lastMousePos.X;
        double dy = pos.Y - _lastMousePos.Y;

        if (_draggingTarget != 0 && _effect is not null)
        {
            float yawRad = _camera.Yaw * MathF.PI / 180.0f;
            float pitchRad = _camera.Pitch * MathF.PI / 180.0f;

            float cY = MathF.Cos(yawRad);
            float sY = MathF.Sin(yawRad);
            float cP = MathF.Cos(pitchRad);
            float sP = MathF.Sin(pitchRad);

            float rX = cY, rY = 0, rZ = -sY;
            float uX = -sY * sP, uY = cP, uZ = -cY * sP;

            float scale = _camera.Distance * 0.0022f;
            float dXWorld = (float)(rX * dx - uX * dy) * scale;
            float dYWorld = (float)(rY * dx - uY * dy) * scale;
            float dZWorld = (float)(rZ * dx - uZ * dy) * scale;

            if (_draggingTarget == 1)
            {
                _effect.SourceXValue = Math.Clamp(_effect.SourceXValue + dXWorld, 0, _effect.RoomWidthValue);
                _effect.SourceYValue = Math.Clamp(_effect.SourceYValue + dYWorld, 0, _effect.RoomHeightValue);
                _effect.SourceZValue = Math.Clamp(_effect.SourceZValue + dZWorld, 0, _effect.RoomDepthValue);
            }
            else
            {
                _effect.ListenerXValue = Math.Clamp(_effect.ListenerXValue + dXWorld, 0, _effect.RoomWidthValue);
                _effect.ListenerYValue = Math.Clamp(_effect.ListenerYValue + dYWorld, 0, _effect.RoomHeightValue);
                _effect.ListenerZValue = Math.Clamp(_effect.ListenerZValue + dZWorld, 0, _effect.RoomDepthValue);
            }
            _needsRedraw = true;
        }
        else if (_isDragging)
        {
            _camera.Rotate((float)(dx * 0.5), (float)(dy * 0.4));
            _needsRedraw = true;
        }
        else if (_isRightDragging)
        {
            _camera.Pan((float)dx, (float)dy);
            _needsRedraw = true;
        }
        _lastMousePos = pos;
    }

    private void Canvas_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_draggingTarget != 0)
        {
            _draggingTarget = 0;
            InteractionCanvas.ReleaseMouseCapture();
            return;
        }
        _isDragging = false;
        InteractionCanvas.ReleaseMouseCapture();
    }

    private void Canvas_MouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        _isRightDragging = true;
        _lastMousePos = e.GetPosition(InteractionCanvas);
        InteractionCanvas.CaptureMouse();
    }

    private void Canvas_MouseRightButtonUp(object sender, MouseButtonEventArgs e)
    {
        _isRightDragging = false;
        InteractionCanvas.ReleaseMouseCapture();

        if (Math.Abs(e.GetPosition(InteractionCanvas).X - _lastMousePos.X) < 3 &&
            Math.Abs(e.GetPosition(InteractionCanvas).Y - _lastMousePos.Y) < 3)
        {
            ShowContextMenu(e.GetPosition(InteractionCanvas));
        }
    }

    private void ShowContextMenu(Point position)
    {
        var menu = new ContextMenu();
        menu.Style = null;

        var editShape = new MenuItem { Header = Localization.Texts.EditRoomShape, Command = ViewModel.EditShapeCommand };
        var manageMat = new MenuItem { Header = Localization.Texts.ManageMaterials, Command = ViewModel.ManageMaterialsCommand };

        menu.Items.Add(editShape);
        menu.Items.Add(manageMat);
        menu.Items.Add(new Separator());

        var geometries = ServiceLocator.GeometryService.GetAllIds();
        if (geometries.Count > 0)
        {
            var loadGeoMenu = new MenuItem { Header = Localization.Texts.LoadGeometry };
            foreach (var id in geometries)
            {
                var item = new MenuItem { Header = id, Command = ViewModel.LoadGeometryCommand, CommandParameter = id };
                loadGeoMenu.Items.Add(item);
            }
            menu.Items.Add(loadGeoMenu);
            menu.Items.Add(new Separator());
        }

        var resetSource = new MenuItem { Header = Localization.Texts.ResetSource, Command = ViewModel.ResetSourceCommand };
        var resetListener = new MenuItem { Header = Localization.Texts.ResetListener, Command = ViewModel.ResetListenerCommand };
        var centerSource = new MenuItem { Header = Localization.Texts.CenterSource, Command = ViewModel.CenterSourceCommand };
        var centerListener = new MenuItem { Header = Localization.Texts.CenterListener, Command = ViewModel.CenterListenerCommand };
        var swapPos = new MenuItem { Header = Localization.Texts.SwapPositions, Command = ViewModel.SwapPositionsCommand };

        menu.Items.Add(resetSource);
        menu.Items.Add(resetListener);
        menu.Items.Add(centerSource);
        menu.Items.Add(centerListener);
        menu.Items.Add(swapPos);
        menu.Items.Add(new Separator());

        var viewMenu = new MenuItem { Header = Localization.Texts.ViewOptions };
        viewMenu.Items.Add(new MenuItem { Header = Localization.Texts.TopView, Command = new RelayCommand(_ => { TopView_Click(null!, null!); }) });
        viewMenu.Items.Add(new MenuItem { Header = Localization.Texts.FrontView, Command = new RelayCommand(_ => { FrontView_Click(null!, null!); }) });
        viewMenu.Items.Add(new MenuItem { Header = Localization.Texts.SideView, Command = new RelayCommand(_ => { SideView_Click(null!, null!); }) });
        viewMenu.Items.Add(new MenuItem { Header = Localization.Texts.ResetView, Command = new RelayCommand(_ => { ResetView_Click(null!, null!); }) });
        menu.Items.Add(viewMenu);

        menu.PlacementTarget = InteractionCanvas;
        menu.IsOpen = true;
    }

    private void OpenShapeEditor()
    {
        if (_effect is null) return;
        var window = new RoomShapeEditorWindow { Topmost = true, WindowStartupLocation = WindowStartupLocation.CenterScreen };
        try { window.Owner = Window.GetWindow(this); } catch { }

        var currentGeo = _effect.ResolveGeometry();
        if (currentGeo is null)
        {
            currentGeo = RoomGeometry.FromShape(
                _effect.RoomShapeValue,
                _effect.RoomWidthValue,
                _effect.RoomHeightValue,
                _effect.RoomDepthValue,
                MaterialCoefficients.GetAbsorption(_effect.WallMaterialValue),
                MaterialCoefficients.GetAbsorption(_effect.FloorMaterialValue),
                MaterialCoefficients.GetAbsorption(_effect.CeilingMaterialValue));
        }
        window.SetGeometry(currentGeo);

        if (window.ShowDialog() == true && window.ResultGeometry is not null)
        {
            ViewModel.ApplyGeometry(window.ResultGeometry);
        }
    }

    private void OpenMaterialManager()
    {
        var window = new MaterialManagerWindow { Topmost = true, WindowStartupLocation = WindowStartupLocation.CenterScreen };
        try { window.Owner = Window.GetWindow(this); } catch { }
        window.ShowDialog();
    }

    private void Canvas_MouseWheel(object sender, MouseWheelEventArgs e)
    {
        if ((Keyboard.Modifiers & (ModifierKeys.Control | ModifierKeys.Shift)) != ModifierKeys.None) return;
        _camera.ZoomByDelta(e.Delta);
        _needsRedraw = true;
        e.Handled = true;
    }

    private void ZoomIn_Click(object sender, RoutedEventArgs e) { _camera.ZoomIn(); _needsRedraw = true; }
    private void ZoomOut_Click(object sender, RoutedEventArgs e) { _camera.ZoomOut(); _needsRedraw = true; }
    private void ResetView_Click(object sender, RoutedEventArgs e) { ResetCamera(); _needsRedraw = true; }

    private void TopView_Click(object sender, RoutedEventArgs e)
    {
        if (_effect is null) return;
        _camera.SetTopView(_effect.RoomWidthValue / 2, _effect.RoomHeightValue / 2, _effect.RoomDepthValue / 2);
        _needsRedraw = true;
    }

    private void FrontView_Click(object sender, RoutedEventArgs e)
    {
        if (_effect is null) return;
        _camera.SetFrontView(_effect.RoomWidthValue / 2, _effect.RoomHeightValue / 2, _effect.RoomDepthValue / 2);
        _needsRedraw = true;
    }

    private void SideView_Click(object sender, RoutedEventArgs e)
    {
        if (_effect is null) return;
        _camera.SetSideView(_effect.RoomWidthValue / 2, _effect.RoomHeightValue / 2, _effect.RoomDepthValue / 2);
        _needsRedraw = true;
    }

    private void Settings_Click(object sender, RoutedEventArgs e)
    {
        var window = new SpaceAudioSettingsWindow
        {
            Topmost = true,
            WindowStartupLocation = WindowStartupLocation.CenterScreen
        };
        try { window.Owner = Window.GetWindow(this); } catch { }
        window.ShowDialog();
    }

    private void HeaderGrid_SizeChanged(object sender, SizeChangedEventArgs e) =>
        UpdateHeaderLayout(e.NewSize.Width);

    private void UpdateHeaderLayout(double width)
    {
        bool shouldBeCompact = width < 490;
        if (shouldBeCompact == _isCompactMode) return;
        _isCompactMode = shouldBeCompact;

        var fadeOut = new DoubleAnimation(0, TimeSpan.FromMilliseconds(100));
        var fadeIn = new DoubleAnimation(1, TimeSpan.FromMilliseconds(200))
        {
            BeginTime = TimeSpan.FromMilliseconds(100)
        };

        AnimateTransition(TabPanel, fadeOut, fadeIn, () =>
        {
            if (_isCompactMode)
            {
                Grid.SetRow(TabPanel, 1);
                Grid.SetColumn(TabPanel, 0);
                Grid.SetColumnSpan(TabPanel, 3);
                TabPanel.HorizontalAlignment = HorizontalAlignment.Left;
                TabPanel.Margin = new Thickness(0, 5, 0, 0);
            }
            else
            {
                Grid.SetRow(TabPanel, 0);
                Grid.SetColumn(TabPanel, 2);
                Grid.SetColumnSpan(TabPanel, 1);
                TabPanel.HorizontalAlignment = HorizontalAlignment.Right;
                TabPanel.Margin = new Thickness(10, 0, 10, 0);
            }
        });

        AnimateTransition(SettingsButton, fadeOut, fadeIn, () =>
        {
            if (_isCompactMode)
            {
                Grid.SetRow(SettingsButton, 1);
                Grid.SetColumn(SettingsButton, 3);
                SettingsButton.HorizontalAlignment = HorizontalAlignment.Right;
                SettingsButton.Margin = new Thickness(0, 5, 0, 0);
            }
            else
            {
                Grid.SetRow(SettingsButton, 0);
                Grid.SetColumn(SettingsButton, 3);
                SettingsButton.HorizontalAlignment = HorizontalAlignment.Right;
                SettingsButton.Margin = new Thickness(0);
            }
        });
    }

    private static void AnimateTransition(UIElement element, DoubleAnimation fadeOut, DoubleAnimation fadeIn, Action layoutChange)
    {
        var localFadeOut = fadeOut.Clone();
        localFadeOut.Completed += (_, _) =>
        {
            layoutChange();
            element.BeginAnimation(UIElement.OpacityProperty, fadeIn);
        };
        element.BeginAnimation(UIElement.OpacityProperty, localFadeOut);
    }

    private void PresetToggleButton_PreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (PresetToggleButton.IsChecked != true) return;
        ViewModel.IsPopupOpen = false;
        e.Handled = true;
    }

    private void ResizeThumb_DragDelta(object sender, DragDeltaEventArgs e)
    {
        double newHeight = Math.Clamp(EditorGrid.ActualHeight + e.VerticalChange, 80, 800);
        ViewModel.EditorHeight = newHeight;
        _needsRedraw = true;
    }

    private void PropertiesEditor_BeginEdit(object sender, EventArgs e) =>
        BeginEdit?.Invoke(this, EventArgs.Empty);

    private void PropertiesEditor_EndEdit(object sender, EventArgs e)
    {
        EndEdit?.Invoke(this, EventArgs.Empty);
        _needsRedraw = true;
    }

    protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
    {
        base.OnRenderSizeChanged(sizeInfo);
        _needsRedraw = true;
    }

    private void ScrollViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        e.Handled = true;
        var eventArg = new MouseWheelEventArgs(e.MouseDevice, e.Timestamp, e.Delta)
        {
            RoutedEvent = UIElement.MouseWheelEvent,
            Source = sender
        };
        RaiseEvent(eventArg);
    }
}
