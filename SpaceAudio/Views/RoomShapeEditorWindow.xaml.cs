using Microsoft.Win32;
using SpaceAudio.Models;
using SpaceAudio.Rendering;
using SpaceAudio.Services;
using SpaceAudio.ViewModels;
using System.Numerics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace SpaceAudio.Views;

public partial class RoomShapeEditorWindow : Window
{
    private const double VertexHitRadius = 14.0;

    private RoomShapeEditorViewModel ViewModel => (RoomShapeEditorViewModel)DataContext;
    private readonly Camera3D _camera = new();
    private ThemePalette _palette;
    private bool _isDragging;
    private bool _isRightDragging;
    private Point _lastMousePos;
    private Point _lastRightDownPos;
    private bool _needsRedraw = true;
    private int _draggingVertexIndex = -1;
    private bool _vertexDragPushedUndo;

    public event EventHandler<RoomGeometry>? GeometryApplied;

    public RoomShapeEditorWindow()
    {
        InitializeComponent();
        DataContext = new RoomShapeEditorViewModel();
        ServiceLocator.WindowThemeService.Bind(this);
        _palette = ThemePalette.Detect(SystemColors.WindowColor);

        ViewModel.GeometryChanged += (_, _) => _needsRedraw = true;
        ViewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(RoomShapeEditorViewModel.SelectedFaceIndex)
                               or nameof(RoomShapeEditorViewModel.SelectedVertexIndex))
            {
                _needsRedraw = true;
                if (e.PropertyName == nameof(RoomShapeEditorViewModel.SelectedVertexIndex) && ViewModel.SelectedVertexIndex == -1)
                    VerticesListBox.SelectedItems.Clear();
            }
        };
        ViewModel.RequestClose += (_, _) =>
        {
            GeometryApplied?.Invoke(this, ViewModel.Geometry.Clone());
            var o = Owner;
            Close();
            o?.Activate();
        };

        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    public void SetGeometry(RoomGeometry geometry, float effectW, float effectH, float effectD)
    {
        ViewModel.EffectRoomWidth = effectW;
        ViewModel.EffectRoomHeight = effectH;
        ViewModel.EffectRoomDepth = effectD;
        ViewModel.Geometry = geometry.Clone();
        var center = geometry.CalculateCenter();
        _camera.Reset(center.X, center.Y, center.Z);
        UpdateSliderBounds(effectW, effectH, effectD);
        _needsRedraw = true;
    }

    public void UpdateEffectDimensions(float w, float h, float d)
    {
        ViewModel.EffectRoomWidth = w;
        ViewModel.EffectRoomHeight = h;
        ViewModel.EffectRoomDepth = d;
        ViewModel.NotifyEffectDimensionsChanged();
        UpdateSliderBounds(w, h, d);
        _needsRedraw = true;
    }

    private void UpdateSliderBounds(float w, float h, float d)
    {
        if (!IsLoaded) return;
        if (SliderX is not null) SliderX.Max = w;
        if (SliderY is not null) SliderY.Max = h;
        if (SliderZ is not null) SliderZ.Max = d;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        UpdateTheme();
        CompositionTarget.Rendering += OnRenderFrame;
        var center = ViewModel.Geometry.CalculateCenter();
        _camera.Reset(center.X, center.Y, center.Z);
        UpdateSliderBounds(ViewModel.EffectRoomWidth, ViewModel.EffectRoomHeight, ViewModel.EffectRoomDepth);
        _needsRedraw = true;
    }

    private void OnUnloaded(object sender, RoutedEventArgs e) =>
        CompositionTarget.Rendering -= OnRenderFrame;

    private void OnRenderFrame(object? sender, EventArgs e)
    {
        if (!IsLoaded || !IsVisible || !_needsRedraw) return;
        _needsRedraw = false;
        try { DrawPreview(); }
        catch { _needsRedraw = true; }
    }

    private void DrawPreview()
    {
        if (PreviewHost.ActualWidth <= 0 || PreviewHost.ActualHeight <= 0) return;

        var geo = ViewModel.Geometry;
        float w = ViewModel.EffectRoomWidth;
        float h = ViewModel.EffectRoomHeight;
        float d = ViewModel.EffectRoomDepth;

        var snap = new RoomSnapshot(
            w, h, d,
            0f, 0f, 0f,
            0f, 0f, 0f,
            0f, 1.5f, 0.5f, 0.7f, -3f, -6f, 0.3f,
            0.12f, 0.1f, 0.12f,
            0.3f, 0.3f, 0.3f,
            SpaceAudio.Enums.ReverbQuality.Standard,
            SpaceAudio.Enums.RoomShape.Custom,
            "drywall",
            "wood",
            "drywall",
            geo);

        PreviewHost.Render(_camera, _palette, snap,
            PreviewHost.ActualWidth, PreviewHost.ActualHeight,
            false, false,
            showObjects: false,
            showGrid: ViewModel.ShowGrid,
            gridSize: ViewModel.GridSize,
            showDimensions: ViewModel.ShowDimensions);

        PreviewHost.RenderVertexOverlay(
            _camera,
            geo.Vertices,
            ViewModel.SelectedFaceVertexIndices,
            ViewModel.SelectedVertexIndex,
            ViewModel.SelectedVertexIndices,
            PreviewHost.ActualWidth,
            PreviewHost.ActualHeight,
            snap);
    }

    private void UpdateTheme()
    {
        if (PreviewBorder.Background is SolidColorBrush bg)
            _palette = ThemePalette.Detect(bg.Color);
        else
            _palette = ThemePalette.Detect(SystemColors.WindowColor);
    }

    private int HitTestVertex(Point screenPos)
    {
        var geo = ViewModel.Geometry;
        if (geo.Vertices.Length == 0) return -1;

        var viewMat = ProjectionMatrix.CreateViewMatrix(_camera);
        double w = PreviewHost.ActualWidth;
        double h = PreviewHost.ActualHeight;

        int closest = -1;
        double minDist = VertexHitRadius;

        for (int i = 0; i < geo.Vertices.Length; i++)
        {
            ref readonly var v = ref geo.Vertices[i];
            var cam = Vector3.Transform(new Vector3(v.X, v.Y, v.Z), viewMat);
            if (cam.Z > -ProjectionMatrix.NearPlane) continue;

            var screen = ProjectionMatrix.ProjectToScreen(cam, w, h);
            double dx = screen.X - screenPos.X;
            double dy = screen.Y - screenPos.Y;
            double dist = Math.Sqrt(dx * dx + dy * dy);

            if (dist < minDist)
            {
                minDist = dist;
                closest = i;
            }
        }

        return closest;
    }

    private void Preview_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        var pos = e.GetPosition(PreviewCanvas);

        int hit = HitTestVertex(pos);
        if (hit >= 0)
        {
            if (ViewModel.SelectedFaceIndex >= 0 && (Keyboard.Modifiers & ModifierKeys.Control) != ModifierKeys.None)
            {
                ViewModel.ToggleVertexInSelectedFace(hit);
                _needsRedraw = true;
                e.Handled = true;
                return;
            }

            if (hit < VerticesListBox.Items.Count)
            {
                if ((Keyboard.Modifiers & ModifierKeys.Shift) != ModifierKeys.None || (Keyboard.Modifiers & ModifierKeys.Control) != ModifierKeys.None)
                {
                    var item = VerticesListBox.Items[hit];
                    if (VerticesListBox.SelectedItems.Contains(item))
                        VerticesListBox.SelectedItems.Remove(item);
                    else
                        VerticesListBox.SelectedItems.Add(item);
                }
                else
                {
                    var item = VerticesListBox.Items[hit];
                    if (!VerticesListBox.SelectedItems.Contains(item))
                    {
                        VerticesListBox.SelectedItems.Clear();
                        VerticesListBox.SelectedItems.Add(item);
                    }
                }
            }

            _draggingVertexIndex = hit;
            _vertexDragPushedUndo = false;
            _lastMousePos = pos;
            PreviewCanvas.CaptureMouse();
            _needsRedraw = true;
            return;
        }

        _isDragging = true;
        _lastMousePos = pos;
        PreviewCanvas.CaptureMouse();
    }

    private void Preview_MouseMove(object sender, MouseEventArgs e)
    {
        var pos = e.GetPosition(PreviewCanvas);
        double dx = pos.X - _lastMousePos.X;
        double dy = pos.Y - _lastMousePos.Y;

        if (_draggingVertexIndex >= 0 && _draggingVertexIndex < ViewModel.Geometry.Vertices.Length)
        {
            if (!_vertexDragPushedUndo)
            {
                ViewModel.PushUndo();
                _vertexDragPushedUndo = true;
            }

            var viewMat = ProjectionMatrix.CreateViewMatrix(_camera);
            var v = ViewModel.Geometry.Vertices[_draggingVertexIndex];
            var vCam = Vector3.Transform(new Vector3(v.X, v.Y, v.Z), viewMat);
            float depth = Math.Max(0.1f, -vCam.Z);

            float h = (float)PreviewHost.ActualHeight;
            float fov = 50.0f;
            float tanHalfFov = MathF.Tan(fov * MathF.PI / 360.0f);
            float unitsPerPixel = 2.0f * tanHalfFov * depth / h;

            float yawRad = _camera.Yaw * MathF.PI / 180.0f;
            float pitchRad = _camera.Pitch * MathF.PI / 180.0f;
            float cY = MathF.Cos(yawRad);
            float sY = MathF.Sin(yawRad);
            float cP = MathF.Cos(pitchRad);
            float sP = MathF.Sin(pitchRad);

            float rX = cY, rZ = -sY;
            float uX = -sY * sP, uY = cP, uZ = -cY * sP;

            float dXW = (float)(rX * dx - uX * dy) * unitsPerPixel;
            float dYW = (float)(0 * dx - uY * dy) * unitsPerPixel;
            float dZW = (float)(rZ * dx - uZ * dy) * unitsPerPixel;

            bool lockX = Keyboard.IsKeyDown(Key.X);
            bool lockY = Keyboard.IsKeyDown(Key.Y);
            bool lockZ = Keyboard.IsKeyDown(Key.Z);

            if (lockX) { dYW = 0; dZW = 0; }
            else if (lockY) { dXW = 0; dZW = 0; }
            else if (lockZ) { dXW = 0; dYW = 0; }

            float nx = v.X + dXW;
            float ny = v.Y + dYW;
            float nz = v.Z + dZW;

            if (ViewModel.ShowGrid)
            {
                float gs = ViewModel.GridSize;
                nx = MathF.Round(nx / gs) * gs;
                ny = MathF.Round(ny / gs) * gs;
                nz = MathF.Round(nz / gs) * gs;
            }

            float actualDX = nx - v.X;
            float actualDY = ny - v.Y;
            float actualDZ = nz - v.Z;

            if (actualDX != 0 || actualDY != 0 || actualDZ != 0)
            {
                var targets = new HashSet<int>();
                if (ViewModel.SelectedFaceIndex >= 0 && ViewModel.SelectedFaceVertexIndices.Contains(_draggingVertexIndex))
                {
                    foreach (var i in ViewModel.SelectedFaceVertexIndices)
                        targets.Add(i);
                }
                else if (ViewModel.SelectedVertexIndices.Contains(_draggingVertexIndex))
                {
                    foreach (var i in ViewModel.SelectedVertexIndices)
                        targets.Add(i);
                }
                else
                {
                    targets.Add(_draggingVertexIndex);
                }

                ViewModel.ApplyDeltasToVertices(targets, actualDX, actualDY, actualDZ);
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

    private void Preview_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        _draggingVertexIndex = -1;
        _vertexDragPushedUndo = false;
        _isDragging = false;
        PreviewCanvas.ReleaseMouseCapture();
    }

    private void Preview_MouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        _isRightDragging = true;
        _lastMousePos = e.GetPosition(PreviewCanvas);
        _lastRightDownPos = _lastMousePos;
        PreviewCanvas.CaptureMouse();
    }

    private void Preview_MouseRightButtonUp(object sender, MouseButtonEventArgs e)
    {
        _isRightDragging = false;
        PreviewCanvas.ReleaseMouseCapture();

        var pos = e.GetPosition(PreviewCanvas);
        double dist = Math.Sqrt(Math.Pow(pos.X - _lastRightDownPos.X, 2) + Math.Pow(pos.Y - _lastRightDownPos.Y, 2));

        if (dist < 4.0)
        {
            var menu = new ContextMenu();

            var exportItem = new MenuItem { Header = Localization.Texts.MenuExportBlueprint };
            exportItem.Click += (_, _) =>
            {
                var sfd = new SaveFileDialog { Filter = "PNG Image (*.png)|*.png", DefaultExt = ".png", Title = Localization.Texts.MenuExportBlueprint };
                if (sfd.ShowDialog() == true)
                    ViewModel.ExportBlueprintCommand.Execute(sfd.FileName);
            };
            menu.Items.Add(exportItem);

            var importItem = new MenuItem { Header = Localization.Texts.MenuImportBlueprint };
            importItem.Click += (_, _) =>
            {
                var ofd = new OpenFileDialog { Filter = "PNG Image (*.png)|*.png", DefaultExt = ".png", Title = Localization.Texts.MenuImportBlueprint };
                if (ofd.ShowDialog() == true)
                    ViewModel.ImportBlueprintCommand.Execute(ofd.FileName);
            };
            menu.Items.Add(importItem);

            menu.IsOpen = true;
        }
    }

    private void Preview_MouseWheel(object sender, MouseWheelEventArgs e)
    {
        _camera.ZoomByDelta(e.Delta);
        _needsRedraw = true;
        e.Handled = true;
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        var o = Owner;
        Close();
        o?.Activate();
    }

    private void VerticesList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is not ListBox list) return;
        ViewModel.SelectedVertexIndices.Clear();
        foreach (var item in list.SelectedItems)
        {
            if (item is VertexItem vi)
                ViewModel.SelectedVertexIndices.Add(vi.Index);
        }
        if (list.SelectedItem is VertexItem first)
            ViewModel.SelectedVertexIndex = first.Index;
        else
            ViewModel.SelectedVertexIndex = -1;

        ViewModel.ResetMultiVertexDeltas();
        _needsRedraw = true;
    }

    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        base.OnPreviewKeyDown(e);

        if (e.Key is Key.LeftShift or Key.RightShift)
        {
            ViewModel.IsShiftDown = true;
            return;
        }

        bool ctrl = (Keyboard.Modifiers & ModifierKeys.Control) != ModifierKeys.None;
        bool shift = (Keyboard.Modifiers & ModifierKeys.Shift) != ModifierKeys.None;

        if (ctrl && e.Key == Key.Z && !shift)
        {
            ViewModel.UndoCommand.Execute(null);
            e.Handled = true;
            return;
        }

        if (ctrl && (e.Key == Key.Y || (e.Key == Key.Z && shift)))
        {
            ViewModel.RedoCommand.Execute(null);
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Delete && ViewModel.RemoveVertexCommand.CanExecute(null)
            && ViewModel.SelectedVertexIndex >= 0)
        {
            ViewModel.RemoveVertexCommand.Execute(null);
            e.Handled = true;
        }
    }

    protected override void OnPreviewKeyUp(KeyEventArgs e)
    {
        base.OnPreviewKeyUp(e);
        if (e.Key is Key.LeftShift or Key.RightShift)
            ViewModel.IsShiftDown = false;
    }

    protected override void OnDeactivated(EventArgs e)
    {
        base.OnDeactivated(e);
        ViewModel.IsShiftDown = false;
    }
}
