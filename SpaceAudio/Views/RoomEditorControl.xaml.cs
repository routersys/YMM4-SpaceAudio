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

    private RoomShapeEditorWindow? _shapeEditorWindow;
    private MaterialManagerWindow? _materialManagerWindow;

    public event EventHandler? BeginEdit;
    public event EventHandler? EndEdit;

    private float _lastSx = -1, _lastSz = -1;
    private float _lastLx = -1, _lastLz = -1;

    public new SpaceAudioEffect? Effect
    {
        get => _effect;
        set
        {
            if (ReferenceEquals(_effect, value)) return;

            if (_effect is INotifyPropertyChanged oldNpc)
                oldNpc.PropertyChanged -= OnEffectPropertyChanged;

            _shapeEditorWindow?.Close();
            _shapeEditorWindow = null;
            _materialManagerWindow?.Close();
            _materialManagerWindow = null;

            _effect = value;

            if (_effect is INotifyPropertyChanged newNpc)
                newNpc.PropertyChanged += OnEffectPropertyChanged;

            if (_effect is not null)
            {
                ViewModel.Effect = _effect;
                _lastSx = _effect.SourceXValue;
                _lastSz = _effect.SourceZValue;
                _lastLx = _effect.ListenerXValue;
                _lastLz = _effect.ListenerZValue;
            }

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

    private void OnEffectPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        _needsRedraw = true;
        if (_shapeEditorWindow is not null
            && (e.PropertyName is nameof(SpaceAudioEffect.RoomWidthValue)
                                or nameof(SpaceAudioEffect.RoomHeightValue)
                                or nameof(SpaceAudioEffect.RoomDepthValue))
            && _effect is not null)
        {
            _shapeEditorWindow.UpdateEffectDimensions(
                _effect.RoomWidthValue,
                _effect.RoomHeightValue,
                _effect.RoomDepthValue);
        }
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        UpdateTheme();
        ResetCamera();
        TimelineOverlay.SeekStarted += OnTimelineSeekStarted;
        TimelineOverlay.SeekChanged += OnTimelineSeekChanged;
        TimelineOverlay.SeekEnded += OnTimelineSeekEnded;
        CompositionTarget.Rendering += OnRenderFrame;
        _needsRedraw = true;
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        CompositionTarget.Rendering -= OnRenderFrame;
        TimelineOverlay.SeekStarted -= OnTimelineSeekStarted;
        TimelineOverlay.SeekChanged -= OnTimelineSeekChanged;
        TimelineOverlay.SeekEnded -= OnTimelineSeekEnded;

        if (_effect is INotifyPropertyChanged npc)
            npc.PropertyChanged -= OnEffectPropertyChanged;

        _shapeEditorWindow?.Close();
        _shapeEditorWindow = null;
        _materialManagerWindow?.Close();
        _materialManagerWindow = null;
    }

    private void OnRenderFrame(object? sender, EventArgs e)
    {
        if (!IsLoaded || !IsVisible) return;
        UpdateTimeline();
        if (!_needsRedraw) return;
        EnforceConstraints();
        _needsRedraw = false;
        DrawAll();
    }

    private void UpdateTimeline()
    {
        var service = ServiceLocator.TimelineService;
        var timeline = ViewModel.Timeline;
        if (service.IsPlaying)
            timeline.ReleasePin();
        long displayFrame = timeline.DisplayFrame;
        long totalFrames = service.TotalFrames;
        int fps = service.Fps > 0 ? service.Fps : 30;
        TimelineOverlay.Update(displayFrame, totalFrames, fps, service.IsPlaying);
        if (ViewModel.SyncTimeline(displayFrame, totalFrames, fps))
            _needsRedraw = true;
    }

    private void OnTimelineSeekStarted(long frame)
    {
        ViewModel.Timeline.BeginSeek(frame);
        _needsRedraw = true;
    }

    private void OnTimelineSeekChanged(long frame)
    {
        ViewModel.Timeline.UpdateSeek(frame);
        _needsRedraw = true;
    }

    private void OnTimelineSeekEnded(long frame)
    {
        ViewModel.Timeline.EndSeek();
        _needsRedraw = true;
    }

    private void EnforceConstraints()
    {
        if (_effect == null) return;

        float rw = _effect.RoomWidthValue;
        float rh = _effect.RoomHeightValue;
        float rd = _effect.RoomDepthValue;
        var geo = _effect.ResolveScaledGeometry(rw, rh, rd);

        void ClampValues(Animation animX, Animation animY, Animation animZ, ref float lastX, ref float lastZ)
        {
            if (geo is not null)
            {
                for (int i = 0; i < animX.Values.Count || i < animZ.Values.Count || i < animY.Values.Count; i++)
                {
                    double vx = i < animX.Values.Count ? animX.Values[i].Value : animX.Values[0].Value;
                    double vz = i < animZ.Values.Count ? animZ.Values[i].Value : animZ.Values[0].Value;
                    double vy = i < animY.Values.Count ? animY.Values[i].Value : (animY.Values.Count > 0 ? animY.Values[0].Value : 0);

                    (float newX, float newZ) = geo.RayCastXZ(lastX, lastZ, (float)vx, (float)vz);

                    float tmpX = newX;
                    float tmpY = (float)vy;
                    float tmpZ = newZ;
                    ConstrainToHeight(geo, rh, ref tmpX, ref tmpY, ref tmpZ, lastX, lastZ);
                    newX = tmpX;
                    newZ = tmpZ;

                    if (i < animX.Values.Count && (float)vx != newX) animX.Values[i].Value = newX;
                    if (i < animZ.Values.Count && (float)vz != newZ) animZ.Values[i].Value = newZ;

                    if (i < animY.Values.Count)
                    {
                        var (yMin, yMax) = geo.GetYBoundsAtXZ(newX, newZ, 0, rh);
                        double currentVy = animY.Values[i].Value;
                        if (currentVy < yMin) animY.Values[i].Value = yMin;
                        else if (currentVy > yMax) animY.Values[i].Value = yMax;
                    }

                    if (i == 0)
                    {
                        lastX = newX;
                        lastZ = newZ;
                    }
                }
            }
            else
            {
                foreach (var k in animX.Values) if (k.Value > rw) k.Value = rw; else if (k.Value < 0) k.Value = 0;
                foreach (var k in animY.Values) if (k.Value > rh) k.Value = rh; else if (k.Value < 0) k.Value = 0;
                foreach (var k in animZ.Values) if (k.Value > rd) k.Value = rd; else if (k.Value < 0) k.Value = 0;
            }
        }

        if (_lastSx < 0) _lastSx = _effect.SourceXValue;
        if (_lastSz < 0) _lastSz = _effect.SourceZValue;
        if (_lastLx < 0) _lastLx = _effect.ListenerXValue;
        if (_lastLz < 0) _lastLz = _effect.ListenerZValue;

        ClampValues(_effect.SourceX, _effect.SourceY, _effect.SourceZ, ref _lastSx, ref _lastSz);
        ClampValues(_effect.ListenerX, _effect.ListenerY, _effect.ListenerZ, ref _lastLx, ref _lastLz);
    }

    private static void ConstrainToHeight(RoomGeometry geo, float rh, ref float nx, ref float ny, ref float nz, float startX, float startZ)
    {
        float targetX = nx;
        float targetZ = nz;
        float validX = startX;
        float validZ = startZ;
        for (int step = 0; step < 20; step++)
        {
            float midX = (validX + targetX) * 0.5f;
            float midZ = (validZ + targetZ) * 0.5f;
            var (min, max) = geo.GetYBoundsAtXZ(midX, midZ, 0, rh);
            if (ny >= min && ny <= max)
            {
                validX = midX;
                validZ = midZ;
            }
            else
            {
                targetX = midX;
                targetZ = midZ;
            }
        }
        nx = validX;
        nz = validZ;
        var (fMin, fMax) = geo.GetYBoundsAtXZ(nx, nz, 0, rh);
        ny = Math.Clamp(ny, fMin, fMax);
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
                float nx = Math.Clamp(_effect.SourceXValue + dXWorld, 0, _effect.RoomWidthValue);
                float ny = Math.Clamp(_effect.SourceYValue + dYWorld, 0, _effect.RoomHeightValue);
                float nz = Math.Clamp(_effect.SourceZValue + dZWorld, 0, _effect.RoomDepthValue);
                var geo = _effect.ResolveScaledGeometry(_effect.RoomWidthValue, _effect.RoomHeightValue, _effect.RoomDepthValue);
                if (geo is not null)
                {
                    var clamped = geo.ClampToPolygonXZ(nx, nz);
                    nx = clamped.X;
                    nz = clamped.Z;
                    ConstrainToHeight(geo, _effect.RoomHeightValue, ref nx, ref ny, ref nz, _effect.SourceXValue, _effect.SourceZValue);
                }
                _effect.SourceXValue = nx;
                _effect.SourceYValue = ny;
                _effect.SourceZValue = nz;
            }
            else
            {
                float nx = Math.Clamp(_effect.ListenerXValue + dXWorld, 0, _effect.RoomWidthValue);
                float ny = Math.Clamp(_effect.ListenerYValue + dYWorld, 0, _effect.RoomHeightValue);
                float nz = Math.Clamp(_effect.ListenerZValue + dZWorld, 0, _effect.RoomDepthValue);
                var geo = _effect.ResolveScaledGeometry(_effect.RoomWidthValue, _effect.RoomHeightValue, _effect.RoomDepthValue);
                if (geo is not null)
                {
                    var clamped = geo.ClampToPolygonXZ(nx, nz);
                    nx = clamped.X;
                    nz = clamped.Z;
                    ConstrainToHeight(geo, _effect.RoomHeightValue, ref nx, ref ny, ref nz, _effect.ListenerXValue, _effect.ListenerZValue);
                }
                _effect.ListenerXValue = nx;
                _effect.ListenerYValue = ny;
                _effect.ListenerZValue = nz;
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

    private Point _rightClickStartPos;

    private void Canvas_MouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        _isRightDragging = true;
        _lastMousePos = e.GetPosition(InteractionCanvas);
        _rightClickStartPos = _lastMousePos;
        InteractionCanvas.CaptureMouse();
    }

    private void Canvas_MouseRightButtonUp(object sender, MouseButtonEventArgs e)
    {
        _isRightDragging = false;
        InteractionCanvas.ReleaseMouseCapture();

        var currentPos = e.GetPosition(InteractionCanvas);
        if (Math.Abs(currentPos.X - _rightClickStartPos.X) < 3 &&
            Math.Abs(currentPos.Y - _rightClickStartPos.Y) < 3)
        {
            ShowContextMenu(currentPos);
        }
    }

    private void ShowContextMenu(Point position)
    {
        var bg = TryFindResource(SystemColors.ControlBrushKey) as Brush ?? SystemColors.ControlBrush;
        var fg = TryFindResource(SystemColors.ControlTextBrushKey) as Brush ?? SystemColors.ControlTextBrush;
        var border = TryFindResource(SystemColors.ControlDarkBrushKey) as Brush ?? SystemColors.ControlDarkBrush;
        var highlightBg = TryFindResource(SystemColors.HighlightBrushKey) as Brush ?? SystemColors.HighlightBrush;
        var highlightFg = TryFindResource(SystemColors.HighlightTextBrushKey) as Brush ?? SystemColors.HighlightTextBrush;

        var menu = new ContextMenu
        {
            Background = bg,
            Foreground = fg,
            BorderBrush = border,
            BorderThickness = new Thickness(1)
        };

        MenuItem MakeItem(string header, ICommand? command = null, object? parameter = null, object? icon = null)
        {
            var item = new MenuItem
            {
                Header = header,
                Command = command,
                CommandParameter = parameter,
                Background = bg,
                Foreground = fg,
                Icon = icon
            };
            ApplyMenuItemStyle(item, bg, fg, highlightBg, highlightFg);
            return item;
        }

        System.Windows.Shapes.Ellipse MakeCircleIcon(Color color) => new()
        {
            Width = 12,
            Height = 12,
            Fill = new SolidColorBrush(color),
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center
        };

        Separator MakeSeparator()
        {
            var sep = new Separator();
            sep.SetResourceReference(BackgroundProperty, SystemColors.ControlDarkBrushKey);
            return sep;
        }

        menu.Items.Add(MakeItem(Localization.Texts.EditRoomShape, ViewModel.EditShapeCommand));
        menu.Items.Add(MakeItem(Localization.Texts.ManageMaterials, ViewModel.ManageMaterialsCommand));
        menu.Items.Add(MakeSeparator());

        var geometries = ServiceLocator.GeometryService.GetAllIds();
        if (geometries.Count > 0)
        {
            var loadGeoItem = MakeItem(Localization.Texts.LoadGeometry);
            foreach (var id in geometries)
                loadGeoItem.Items.Add(MakeItem(id, ViewModel.LoadGeometryCommand, id));
            menu.Items.Add(loadGeoItem);
            menu.Items.Add(MakeSeparator());
        }

        menu.Items.Add(MakeItem(Localization.Texts.ResetSource, ViewModel.ResetSourceCommand, null, MakeCircleIcon(Colors.Orange)));
        menu.Items.Add(MakeItem(Localization.Texts.ResetListener, ViewModel.ResetListenerCommand, null, MakeCircleIcon(Colors.LimeGreen)));
        menu.Items.Add(MakeItem(Localization.Texts.CenterSource, ViewModel.CenterSourceCommand, null, MakeCircleIcon(Colors.Orange)));
        menu.Items.Add(MakeItem(Localization.Texts.CenterListener, ViewModel.CenterListenerCommand, null, MakeCircleIcon(Colors.LimeGreen)));
        menu.Items.Add(MakeItem(Localization.Texts.SwapPositions, ViewModel.SwapPositionsCommand));
        menu.Items.Add(MakeSeparator());

        var viewItem = MakeItem(Localization.Texts.ViewOptions);
        viewItem.Items.Add(MakeItem(Localization.Texts.TopView, new RelayCommand(_ => TopView_Click(null!, null!))));
        viewItem.Items.Add(MakeItem(Localization.Texts.FrontView, new RelayCommand(_ => FrontView_Click(null!, null!))));
        viewItem.Items.Add(MakeItem(Localization.Texts.SideView, new RelayCommand(_ => SideView_Click(null!, null!))));
        viewItem.Items.Add(MakeItem(Localization.Texts.ResetView, new RelayCommand(_ => ResetView_Click(null!, null!))));
        menu.Items.Add(viewItem);

        menu.PlacementTarget = InteractionCanvas;
        menu.IsOpen = true;
    }

    private static void ApplyMenuItemStyle(MenuItem item, Brush bg, Brush fg, Brush highlightBg, Brush highlightFg)
    {
        var style = new Style(typeof(MenuItem));
        style.Setters.Add(new Setter(BackgroundProperty, bg));
        style.Setters.Add(new Setter(ForegroundProperty, fg));

        var trigger = new Trigger { Property = MenuItem.IsHighlightedProperty, Value = true };
        trigger.Setters.Add(new Setter(BackgroundProperty, highlightBg));
        trigger.Setters.Add(new Setter(ForegroundProperty, highlightFg));
        style.Triggers.Add(trigger);

        item.Style = style;
    }

    private void OpenShapeEditor()
    {
        if (_shapeEditorWindow is not null)
        {
            _shapeEditorWindow.Activate();
            return;
        }

        if (_effect is null) return;

        var owner = Window.GetWindow(this);
        var window = new RoomShapeEditorWindow();
        try { window.Owner = owner; } catch { }
        window.WindowStartupLocation = WindowStartupLocation.CenterOwner;

        var currentGeo = _effect.ResolveScaledGeometry(_effect.RoomWidthValue, _effect.RoomHeightValue, _effect.RoomDepthValue) ?? RoomGeometry.FromShape(
            _effect.RoomShapeValue,
            _effect.RoomWidthValue,
            _effect.RoomHeightValue,
            _effect.RoomDepthValue,
            MaterialCoefficients.GetAbsorption(_effect.WallMaterialValue),
            MaterialCoefficients.GetAbsorption(_effect.FloorMaterialValue),
            MaterialCoefficients.GetAbsorption(_effect.CeilingMaterialValue));

        window.SetGeometry(currentGeo,
            _effect.RoomWidthValue,
            _effect.RoomHeightValue,
            _effect.RoomDepthValue);
        window.GeometryApplied += (_, geo) => ViewModel.ApplyGeometry(geo);
        window.Closed += (_, _) =>
        {
            _shapeEditorWindow = null;
            owner?.Activate();
        };

        _shapeEditorWindow = window;
        window.Show();
    }

    private void OpenMaterialManager()
    {
        if (_materialManagerWindow != null && _materialManagerWindow.IsLoaded)
        {
            if (_materialManagerWindow.WindowState == WindowState.Minimized)
                _materialManagerWindow.WindowState = WindowState.Normal;
            _materialManagerWindow.Activate();
            return;
        }

        var owner = Window.GetWindow(this);
        var window = new MaterialManagerWindow { WindowStartupLocation = WindowStartupLocation.CenterOwner };
        try { window.Owner = owner; } catch { }

        window.Closed += (s, e) =>
        {
            _materialManagerWindow = null;
            owner?.Activate();
        };

        _materialManagerWindow = window;
        window.Show();
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
