using SpaceAudio.Interfaces;
using SpaceAudio.Localization;
using SpaceAudio.Models;
using SpaceAudio.Services;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Input;

namespace SpaceAudio.ViewModels;

public sealed class RoomShapeEditorViewModel : ViewModelBase
{
    private const double VertexEpsilon = 0.001;

    private readonly IRoomGeometryService _geometryService;
    private readonly IMaterialService _materialService;
    private readonly IUserNotificationService _notifications;

    private RoomGeometry _geometry;
    private int _selectedVertexIndex = -1;
    private int _selectedFaceIndex = -1;
    private double _vertexX;
    private double _vertexY;
    private double _vertexZ;
    private bool _updatingMemberships;
    private float _effectRoomWidth = 8f;
    private float _effectRoomHeight = 3f;
    private float _effectRoomDepth = 6f;
    private bool _showGrid;
    private float _gridSize = 1.0f;
    private bool _showDimensions;

    private readonly Stack<RoomGeometry> _undoStack = new();
    private readonly Stack<RoomGeometry> _redoStack = new();

    public event EventHandler? GeometryChanged;
    public event EventHandler? RequestClose;

    public RoomGeometry Geometry
    {
        get => _geometry;
        set
        {
            if (!SetProperty(ref _geometry, value)) return;
            OnPropertyChanged(nameof(GeometryMaterials));
            RefreshCollections();
        }
    }

    public float EffectRoomWidth
    {
        get => _effectRoomWidth;
        set => SetProperty(ref _effectRoomWidth, value);
    }

    public float EffectRoomHeight
    {
        get => _effectRoomHeight;
        set => SetProperty(ref _effectRoomHeight, value);
    }

    public float EffectRoomDepth
    {
        get => _effectRoomDepth;
        set => SetProperty(ref _effectRoomDepth, value);
    }

    public bool ShowGrid
    {
        get => _showGrid;
        set => SetProperty(ref _showGrid, value, () => GeometryChanged?.Invoke(this, EventArgs.Empty));
    }

    public float GridSize
    {
        get => _gridSize;
        set
        {
            float clamped = Math.Clamp(value, 0.1f, 10f);
            SetProperty(ref _gridSize, clamped, () => GeometryChanged?.Invoke(this, EventArgs.Empty));
        }
    }

    public bool ShowDimensions
    {
        get => _showDimensions;
        set => SetProperty(ref _showDimensions, value, () => GeometryChanged?.Invoke(this, EventArgs.Empty));
    }

    public ObservableCollection<VertexItem> Vertices { get; } = [];
    public ObservableCollection<FaceItem> FaceItems { get; } = [];
    public ObservableCollection<CustomMaterial> AvailableMaterials { get; } = [];
    public ObservableCollection<FaceVertexMembership> FaceVertexMemberships { get; } = [];

    public int SelectedVertexIndex
    {
        get => _selectedVertexIndex;
        set
        {
            if (!SetProperty(ref _selectedVertexIndex, value)) return;
            if (value >= 0 && value < _geometry.Vertices.Length)
            {
                var v = _geometry.Vertices[value];
                _vertexX = v.X; _vertexY = v.Y; _vertexZ = v.Z;
                OnPropertyChanged(nameof(VertexX));
                OnPropertyChanged(nameof(VertexY));
                OnPropertyChanged(nameof(VertexZ));
            }
            OnPropertyChanged(nameof(HasSelectedVertex));
        }
    }

    public int SelectedFaceIndex
    {
        get => _selectedFaceIndex;
        set
        {
            if (!SetProperty(ref _selectedFaceIndex, value)) return;
            OnPropertyChanged(nameof(SelectedFaceVertexIndices));
            OnPropertyChanged(nameof(SelectedFaceVerticesInfo));
            OnPropertyChanged(nameof(HasSelectedFace));
            OnPropertyChanged(nameof(SelectedFaceMaterialIndex));
            RebuildFaceMemberships();
        }
    }

    public double VertexX
    {
        get => _vertexX;
        set
        {
            if (IsNearlyEqual(_vertexX, value)) return;
            _vertexX = value;
            OnPropertyChanged();
            UpdateSelectedVertex();
        }
    }

    public double VertexY
    {
        get => _vertexY;
        set
        {
            if (IsNearlyEqual(_vertexY, value)) return;
            _vertexY = value;
            OnPropertyChanged();
            UpdateSelectedVertex();
        }
    }

    public double VertexZ
    {
        get => _vertexZ;
        set
        {
            if (IsNearlyEqual(_vertexZ, value)) return;
            _vertexZ = value;
            OnPropertyChanged();
            UpdateSelectedVertex();
        }
    }

    private double _multiVertexDeltaX;
    private double _multiVertexDeltaY;
    private double _multiVertexDeltaZ;

    public bool IsMultiVertexSelection => SelectedVertexIndices.Count > 1;
    public bool IsSingleVertexSelection => SelectedVertexIndices.Count == 1;

    private bool _isShiftDown;
    public bool IsShiftDown
    {
        get => _isShiftDown;
        set => SetProperty(ref _isShiftDown, value, () => OnPropertyChanged(nameof(MultiVertexFormat)));
    }

    public string MultiVertexFormat => _isShiftDown ? "-0.000;-0.000;0.000" : "+0.000;-0.000;0.000";

    public double MultiVertexDeltaX
    {
        get => _multiVertexDeltaX;
        set
        {
            if (IsNearlyEqual(_multiVertexDeltaX, value)) return;
            float dx = (float)(value - _multiVertexDeltaX);
            _multiVertexDeltaX = value;
            OnPropertyChanged();
            if (_isShiftDown) dx = -dx;
            ApplyDeltasToVertices(SelectedVertexIndices, dx, 0, 0);
        }
    }

    public double MultiVertexDeltaY
    {
        get => _multiVertexDeltaY;
        set
        {
            if (IsNearlyEqual(_multiVertexDeltaY, value)) return;
            float dy = (float)(value - _multiVertexDeltaY);
            _multiVertexDeltaY = value;
            OnPropertyChanged();
            if (_isShiftDown) dy = -dy;
            ApplyDeltasToVertices(SelectedVertexIndices, 0, dy, 0);
        }
    }

    public double MultiVertexDeltaZ
    {
        get => _multiVertexDeltaZ;
        set
        {
            if (IsNearlyEqual(_multiVertexDeltaZ, value)) return;
            float dz = (float)(value - _multiVertexDeltaZ);
            _multiVertexDeltaZ = value;
            OnPropertyChanged();
            if (_isShiftDown) dz = -dz;
            ApplyDeltasToVertices(SelectedVertexIndices, 0, 0, dz);
        }
    }

    public void ResetMultiVertexDeltas()
    {
        _multiVertexDeltaX = 0;
        _multiVertexDeltaY = 0;
        _multiVertexDeltaZ = 0;
        OnPropertyChanged(nameof(MultiVertexDeltaX));
        OnPropertyChanged(nameof(MultiVertexDeltaY));
        OnPropertyChanged(nameof(MultiVertexDeltaZ));
        OnPropertyChanged(nameof(IsMultiVertexSelection));
        OnPropertyChanged(nameof(IsSingleVertexSelection));
        OnPropertyChanged(nameof(HasSelectedVertex));
    }

    public double VertexXMin => 0.0;
    public double VertexXMax => _effectRoomWidth;
    public double VertexYMin => 0.0;
    public double VertexYMax => _effectRoomHeight;
    public double VertexZMin => 0.0;
    public double VertexZMax => _effectRoomDepth;

    public bool HasSelectedVertex => SelectedVertexIndices.Count > 0;
    public bool HasSelectedFace => _selectedFaceIndex >= 0 && _selectedFaceIndex < _geometry.Faces.Length;

    public int[] SelectedFaceVertexIndices
    {
        get
        {
            if (_selectedFaceIndex < 0 || _selectedFaceIndex >= _geometry.Faces.Length)
                return [];
            return _geometry.Faces[_selectedFaceIndex].VertexIndices;
        }
    }

    public string SelectedFaceVerticesInfo
    {
        get
        {
            if (_selectedFaceIndex < 0 || _selectedFaceIndex >= _geometry.Faces.Length)
                return "";
            return string.Join("  →  ", _geometry.Faces[_selectedFaceIndex].VertexIndices.Select(i => $"V{i}"));
        }
    }

    public int SelectedFaceMaterialIndex
    {
        get => _selectedFaceIndex >= 0 && _selectedFaceIndex < _geometry.Faces.Length
            ? _geometry.Faces[_selectedFaceIndex].MaterialIndex : -1;
        set
        {
            if (_selectedFaceIndex < 0 || _selectedFaceIndex >= _geometry.Faces.Length) return;
            UpdateFaceMaterial(_selectedFaceIndex, value);
            OnPropertyChanged();
        }
    }

    public CustomMaterial[] GeometryMaterials => _geometry.Materials;

    public ICommand AddVertexCommand { get; }
    public ICommand RemoveVertexCommand { get; }
    public ICommand AddFaceCommand { get; }
    public ICommand RemoveFaceCommand { get; }
    public ICommand SaveGeometryCommand { get; }
    public ICommand ApplyCommand { get; }
    public ICommand MoveFaceVertexUpCommand { get; }
    public ICommand MoveFaceVertexDownCommand { get; }
    public ICommand ApplyBoxPresetCommand { get; }
    public ICommand ApplyLShapePresetCommand { get; }
    public ICommand ApplyCathedralPresetCommand { get; }
    public ICommand ApplyStudioPresetCommand { get; }
    public ICommand ApplyTShapePresetCommand { get; }
    public ICommand ApplyUShapePresetCommand { get; }
    public ICommand UndoCommand { get; }
    public ICommand RedoCommand { get; }
    public ICommand ExportBlueprintCommand { get; }
    public ICommand ImportBlueprintCommand { get; }

    public HashSet<int> SelectedVertexIndices { get; } = [];

    public RoomShapeEditorViewModel() : this(
        ServiceLocator.GeometryService,
        ServiceLocator.MaterialService,
        ServiceLocator.NotificationService)
    { }

    public RoomShapeEditorViewModel(
        IRoomGeometryService geometryService,
        IMaterialService materialService,
        IUserNotificationService notifications)
    {
        _geometryService = geometryService;
        _materialService = materialService;
        _notifications = notifications;
        _geometry = RoomGeometry.CreateBox(8, 3, 6, 0.12f, 0.1f, 0.12f);

        AddVertexCommand = new RelayCommand(_ => AddVertex());
        RemoveVertexCommand = new RelayCommand(_ => RemoveVertex(), _ => _selectedVertexIndex >= 0);
        AddFaceCommand = new RelayCommand(_ => AddFace());
        RemoveFaceCommand = new RelayCommand(_ => RemoveFace(), _ => _selectedFaceIndex >= 0);
        SaveGeometryCommand = new AsyncRelayCommand(_ => SaveGeometryAsync());
        ApplyCommand = new RelayCommand(_ => Apply());
        MoveFaceVertexUpCommand = new RelayCommand(
            p => { if (p is int vi) MoveFaceVertexUp(vi); },
            p => p is int vi && CanMoveFaceVertexUp(vi));
        MoveFaceVertexDownCommand = new RelayCommand(
            p => { if (p is int vi) MoveFaceVertexDown(vi); },
            p => p is int vi && CanMoveFaceVertexDown(vi));
        ApplyBoxPresetCommand = new RelayCommand(_ => ApplyBoxPreset());
        ApplyLShapePresetCommand = new RelayCommand(_ => ApplyLShapePreset());
        ApplyCathedralPresetCommand = new RelayCommand(_ => ApplyCathedralPreset());
        ApplyStudioPresetCommand = new RelayCommand(_ => ApplyStudioPreset());
        ApplyTShapePresetCommand = new RelayCommand(_ => ApplyTShapePreset());
        ApplyUShapePresetCommand = new RelayCommand(_ => ApplyUShapePreset());
        UndoCommand = new RelayCommand(_ => Undo(), _ => _undoStack.Count > 0);
        RedoCommand = new RelayCommand(_ => Redo(), _ => _redoStack.Count > 0);
        ExportBlueprintCommand = new RelayCommand(p => { if (p is string path) ExportBlueprint(path); }, p => p is string s && !string.IsNullOrWhiteSpace(s));
        ImportBlueprintCommand = new RelayCommand(p => { if (p is string path) ImportBlueprint(path); }, p => p is string s && !string.IsNullOrWhiteSpace(s));

        RefreshMaterials();
        _materialService.MaterialsChanged += (_, _) => RefreshMaterials();
    }

    public void PushUndo()
    {
        _undoStack.Push(_geometry.Clone());
        _redoStack.Clear();
    }

    public void Undo()
    {
        if (_undoStack.Count == 0) return;
        _redoStack.Push(_geometry.Clone());
        RestoreGeometry(_undoStack.Pop());
    }

    public void Redo()
    {
        if (_redoStack.Count == 0) return;
        _undoStack.Push(_geometry.Clone());
        RestoreGeometry(_redoStack.Pop());
    }

    public void ToggleVertexInSelectedFace(int vertexIndex)
    {
        if (_selectedFaceIndex < 0 || _selectedFaceIndex >= _geometry.Faces.Length) return;
        PushUndo();
        var face = _geometry.Faces[_selectedFaceIndex];
        var indices = face.VertexIndices.ToList();
        if (indices.Contains(vertexIndex))
            indices.Remove(vertexIndex);
        else
            indices.Add(vertexIndex);
        face.VertexIndices = [.. indices];
        _geometry.Invalidate();
        RebuildFaceMemberships();
        OnPropertyChanged(nameof(SelectedFaceVertexIndices));
        OnPropertyChanged(nameof(SelectedFaceVerticesInfo));
        GeometryChanged?.Invoke(this, EventArgs.Empty);
    }

    public void ApplyDeltasToVertices(IEnumerable<int> indices, float dx, float dy, float dz)
    {
        bool changed = false;
        foreach (var index in indices)
        {
            if (index < 0 || index >= _geometry.Vertices.Length) continue;
            var v = _geometry.Vertices[index];
            float nx = Math.Clamp(v.X + dx, 0, _effectRoomWidth);
            float ny = Math.Clamp(v.Y + dy, 0, _effectRoomHeight);
            float nz = Math.Clamp(v.Z + dz, 0, _effectRoomDepth);

            if (nx == v.X && ny == v.Y && nz == v.Z) continue;

            _geometry.Vertices[index] = new GeometryVertex(nx, ny, nz);

            if (index < Vertices.Count)
            {
                Vertices[index].X = nx;
                Vertices[index].Y = ny;
                Vertices[index].Z = nz;
            }

            if (_selectedVertexIndex == index)
            {
                _vertexX = nx; _vertexY = ny; _vertexZ = nz;
                OnPropertyChanged(nameof(VertexX));
                OnPropertyChanged(nameof(VertexY));
                OnPropertyChanged(nameof(VertexZ));
            }
            changed = true;
        }

        if (changed)
        {
            _geometry.Invalidate();
            RebuildFaceMemberships();
            GeometryChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public void UpdateVertexPosition(int index, float x, float y, float z)
    {
        if (index < 0 || index >= _geometry.Vertices.Length) return;
        x = Math.Clamp(x, 0, _effectRoomWidth);
        y = Math.Clamp(y, 0, _effectRoomHeight);
        z = Math.Clamp(z, 0, _effectRoomDepth);
        _geometry.Vertices[index] = new GeometryVertex(x, y, z);
        _geometry.Invalidate();
        if (index < Vertices.Count)
            Vertices[index] = new VertexItem(index, x, y, z);
        if (_selectedVertexIndex == index)
        {
            _vertexX = x; _vertexY = y; _vertexZ = z;
            OnPropertyChanged(nameof(VertexX));
            OnPropertyChanged(nameof(VertexY));
            OnPropertyChanged(nameof(VertexZ));
        }
        RebuildFaceMemberships();
        GeometryChanged?.Invoke(this, EventArgs.Empty);
    }

    public void UpdateFaceIndices(int faceIndex, int[] indices)
    {
        if (faceIndex < 0 || faceIndex >= _geometry.Faces.Length) return;
        _geometry.Faces[faceIndex].VertexIndices = indices;
        _geometry.Invalidate();
        OnPropertyChanged(nameof(SelectedFaceVertexIndices));
        OnPropertyChanged(nameof(SelectedFaceVerticesInfo));
        RebuildFaceMemberships();
        GeometryChanged?.Invoke(this, EventArgs.Empty);
    }

    public void UpdateFaceMaterial(int faceIndex, int materialIndex)
    {
        if (faceIndex < 0 || faceIndex >= _geometry.Faces.Length) return;
        _geometry.Faces[faceIndex].MaterialIndex = materialIndex;
        _geometry.Invalidate();
        GeometryChanged?.Invoke(this, EventArgs.Empty);
    }

    public void NotifyEffectDimensionsChanged()
    {
        NotifyVertexBounds();
        GeometryChanged?.Invoke(this, EventArgs.Empty);
    }

    private static bool IsNearlyEqual(double a, double b) => Math.Abs(a - b) < VertexEpsilon;

    private void RestoreGeometry(RoomGeometry geometry)
    {
        _geometry = geometry;
        _selectedVertexIndex = -1;
        _selectedFaceIndex = -1;
        SelectedVertexIndices.Clear();
        RefreshCollections();
        OnPropertyChanged(nameof(GeometryMaterials));
        GeometryChanged?.Invoke(this, EventArgs.Empty);
    }

    private static RoomGeometry ScaleGeometry(RoomGeometry geo, float targetW, float targetH, float targetD)
    {
        if (geo.Vertices.Length == 0) return geo;
        float maxX = geo.Vertices.Max(v => v.X);
        float maxY = geo.Vertices.Max(v => v.Y);
        float maxZ = geo.Vertices.Max(v => v.Z);
        if (maxX <= 0) maxX = 1;
        if (maxY <= 0) maxY = 1;
        if (maxZ <= 0) maxZ = 1;
        float sx = targetW / maxX;
        float sy = targetH / maxY;
        float sz = targetD / maxZ;
        var result = geo.Clone();
        for (int i = 0; i < result.Vertices.Length; i++)
        {
            var v = result.Vertices[i];
            result.Vertices[i] = new GeometryVertex(v.X * sx, v.Y * sy, v.Z * sz);
        }
        result.Invalidate();
        return result;
    }

    private void CommitPreset(RoomGeometry geo)
    {
        _geometry = geo;
        _selectedVertexIndex = -1;
        _selectedFaceIndex = -1;
        SelectedVertexIndices.Clear();
        RefreshCollections();
        OnPropertyChanged(nameof(GeometryMaterials));
        GeometryChanged?.Invoke(this, EventArgs.Empty);
    }

    private void ApplyBoxPreset()
    {
        PushUndo();
        var mats = ResolvePresetMaterials();
        var geo = RoomGeometry.CreateBox(_effectRoomWidth, _effectRoomHeight, _effectRoomDepth,
            mats.Length > 2 ? mats[2].Absorption : 0.12f,
            mats.Length > 0 ? mats[0].Absorption : 0.10f,
            mats.Length > 1 ? mats[1].Absorption : 0.12f);
        geo.Materials = mats;
        CommitPreset(geo);
    }

    private void ApplyLShapePreset()
    {
        PushUndo();
        var mats = ResolvePresetMaterials();
        int matFloor = 0;
        int matCeil = mats.Length > 1 ? 1 : 0;
        int matWall = mats.Length > 2 ? 2 : 0;

        var verts = new GeometryVertex[]
        {
            new(0f, 0f, 0f),  new(8f, 0f, 0f),  new(8f, 0f, 3f),
            new(5f, 0f, 3f),  new(5f, 0f, 6f),  new(0f, 0f, 6f),
            new(0f, 3f, 0f),  new(8f, 3f, 0f),  new(8f, 3f, 3f),
            new(5f, 3f, 3f),  new(5f, 3f, 6f),  new(0f, 3f, 6f),
        };

        var faces = new RoomFace[]
        {
            new([0, 1, 2, 3, 4, 5], matFloor),
            new([6, 11, 10, 9, 8, 7], matCeil),
            new([0, 6, 7, 1], matWall),
            new([1, 7, 8, 2], matWall),
            new([2, 8, 9, 3], matWall),
            new([3, 9, 10, 4], matWall),
            new([4, 10, 11, 5], matWall),
            new([5, 11, 6, 0], matWall),
        };

        var template = new RoomGeometry { Vertices = verts, Faces = faces, Materials = mats, Name = Texts.PresetLShape, ShapeId = "l_shape" };
        template.Invalidate();
        var geo = ScaleGeometry(template, _effectRoomWidth, _effectRoomHeight, _effectRoomDepth);
        geo.Name = Texts.PresetLShape; geo.ShapeId = "l_shape"; geo.Materials = mats;
        CommitPreset(geo);
    }

    private void ApplyCathedralPreset()
    {
        PushUndo();
        var mats = ResolvePresetMaterials();
        var geo = RoomGeometry.CreateCathedral(_effectRoomWidth, _effectRoomHeight, _effectRoomDepth,
            mats.Length > 2 ? mats[2].Absorption : 0.12f,
            mats.Length > 0 ? mats[0].Absorption : 0.10f,
            mats.Length > 1 ? mats[1].Absorption : 0.08f);
        geo.Materials = mats;
        CommitPreset(geo);
    }

    private void ApplyStudioPreset()
    {
        PushUndo();
        var mats = ResolvePresetMaterials();
        var geo = RoomGeometry.CreateStudio(_effectRoomWidth, _effectRoomHeight, _effectRoomDepth,
            mats.Length > 2 ? mats[2].Absorption : 0.25f,
            mats.Length > 0 ? mats[0].Absorption : 0.15f,
            mats.Length > 1 ? mats[1].Absorption : 0.20f);
        geo.Materials = mats;
        CommitPreset(geo);
    }

    private void ApplyTShapePreset()
    {
        PushUndo();
        var mats = ResolvePresetMaterials();
        int mF = 0, mC = mats.Length > 1 ? 1 : 0, mW = mats.Length > 2 ? 2 : 0;
        var verts = new GeometryVertex[]
        {
            new(0f,0f,0f),   new(12f,0f,0f),  new(12f,0f,4f),
            new(8f,0f,4f),   new(8f,0f,10f),  new(4f,0f,10f),
            new(4f,0f,4f),   new(0f,0f,4f),
            new(0f,3f,0f),   new(12f,3f,0f),  new(12f,3f,4f),
            new(8f,3f,4f),   new(8f,3f,10f),  new(4f,3f,10f),
            new(4f,3f,4f),   new(0f,3f,4f),
        };
        var faces = new RoomFace[]
        {
            new([0,1,2,3,4,5,6,7],      mF),
            new([8,15,14,13,12,11,10,9], mC),
            new([0,8,9,1],   mW), new([1,9,10,2],  mW),
            new([2,10,11,3], mW), new([3,11,12,4], mW),
            new([4,12,13,5], mW), new([5,13,14,6], mW),
            new([6,14,15,7], mW), new([7,15,8,0],  mW),
        };
        var template = new RoomGeometry { Vertices = verts, Faces = faces, Materials = mats, Name = Texts.PresetTShape, ShapeId = "t_shape" };
        template.Invalidate();
        var geo = ScaleGeometry(template, _effectRoomWidth, _effectRoomHeight, _effectRoomDepth);
        geo.Name = Texts.PresetTShape; geo.ShapeId = "t_shape"; geo.Materials = mats;
        CommitPreset(geo);
    }

    private void ApplyUShapePreset()
    {
        PushUndo();
        var mats = ResolvePresetMaterials();
        int mF = 0, mC = mats.Length > 1 ? 1 : 0, mW = mats.Length > 2 ? 2 : 0;
        var verts = new GeometryVertex[]
        {
            new(0f,0f,0f),  new(3f,0f,0f),  new(3f,0f,6f),
            new(6f,0f,6f),  new(6f,0f,0f),  new(9f,0f,0f),
            new(9f,0f,9f),  new(0f,0f,9f),
            new(0f,3f,0f),  new(3f,3f,0f),  new(3f,3f,6f),
            new(6f,3f,6f),  new(6f,3f,0f),  new(9f,3f,0f),
            new(9f,3f,9f),  new(0f,3f,9f),
        };
        var faces = new RoomFace[]
        {
            new([0,1,2,3,4,5,6,7],      mF),
            new([8,15,14,13,12,11,10,9], mC),
            new([0,8,9,1],  mW), new([1,9,10,2],  mW),
            new([2,10,11,3],mW), new([3,11,12,4], mW),
            new([4,12,13,5],mW), new([5,13,14,6], mW),
            new([6,14,15,7],mW), new([7,15,8,0],  mW),
        };
        var template = new RoomGeometry { Vertices = verts, Faces = faces, Materials = mats, Name = Texts.PresetUShape, ShapeId = "u_shape" };
        template.Invalidate();
        var geo = ScaleGeometry(template, _effectRoomWidth, _effectRoomHeight, _effectRoomDepth);
        geo.Name = Texts.PresetUShape; geo.ShapeId = "u_shape"; geo.Materials = mats;
        CommitPreset(geo);
    }

    private CustomMaterial[] ResolvePresetMaterials()
    {
        if (_geometry.Materials.Length >= 3) return _geometry.Materials;
        return
        [
            new CustomMaterial("floor_p", "Floor", 0.10f),
            new CustomMaterial("ceil_p", "Ceiling", 0.12f),
            new CustomMaterial("wall_p", "Wall", 0.12f),
        ];
    }

    private void AddVertex()
    {
        PushUndo();
        var center = _geometry.CalculateCenter();
        var list = _geometry.Vertices.ToList();
        list.Add(new GeometryVertex(
            Math.Clamp(center.X, 0, _effectRoomWidth),
            Math.Clamp(center.Y, 0, _effectRoomHeight),
            Math.Clamp(center.Z, 0, _effectRoomDepth)));
        _geometry.Vertices = [.. list];
        _geometry.Invalidate();
        RefreshCollections();
        SelectedVertexIndex = list.Count - 1;
        GeometryChanged?.Invoke(this, EventArgs.Empty);
    }

    private void RemoveVertex()
    {
        if (_selectedVertexIndex < 0 || _selectedVertexIndex >= _geometry.Vertices.Length) return;
        PushUndo();
        int removed = _selectedVertexIndex;
        var verts = _geometry.Vertices.ToList();
        verts.RemoveAt(removed);
        _geometry.Vertices = [.. verts];
        var faces = _geometry.Faces.ToList();
        faces.RemoveAll(f => f.VertexIndices.Any(vi => vi == removed));
        foreach (var f in faces)
            f.VertexIndices = f.VertexIndices.Select(vi => vi > removed ? vi - 1 : vi).ToArray();
        _geometry.Faces = [.. faces];
        _geometry.Invalidate();
        SelectedVertexIndex = -1;
        SelectedVertexIndices.Clear();
        RefreshCollections();
        GeometryChanged?.Invoke(this, EventArgs.Empty);
    }

    private void AddFace()
    {
        if (_geometry.Vertices.Length < 3) return;
        PushUndo();
        var faces = _geometry.Faces.ToList();
        faces.Add(new RoomFace([0, 1, 2], 0));
        _geometry.Faces = [.. faces];
        _geometry.Invalidate();
        RefreshCollections();
        SelectedFaceIndex = faces.Count - 1;
        GeometryChanged?.Invoke(this, EventArgs.Empty);
    }

    private void RemoveFace()
    {
        if (_selectedFaceIndex < 0 || _selectedFaceIndex >= _geometry.Faces.Length) return;
        PushUndo();
        var faces = _geometry.Faces.ToList();
        faces.RemoveAt(_selectedFaceIndex);
        _geometry.Faces = [.. faces];
        _geometry.Invalidate();
        SelectedFaceIndex = -1;
        RefreshCollections();
        GeometryChanged?.Invoke(this, EventArgs.Empty);
    }

    private void MoveFaceVertexUp(int vertexIndex)
    {
        if (_selectedFaceIndex < 0 || _selectedFaceIndex >= _geometry.Faces.Length) return;
        var face = _geometry.Faces[_selectedFaceIndex];
        var indices = face.VertexIndices.ToList();
        int pos = indices.IndexOf(vertexIndex);
        if (pos <= 0) return;
        (indices[pos], indices[pos - 1]) = (indices[pos - 1], indices[pos]);
        face.VertexIndices = [.. indices];
        _geometry.Invalidate();
        RebuildFaceMemberships();
        OnPropertyChanged(nameof(SelectedFaceVertexIndices));
        OnPropertyChanged(nameof(SelectedFaceVerticesInfo));
        GeometryChanged?.Invoke(this, EventArgs.Empty);
    }

    private void MoveFaceVertexDown(int vertexIndex)
    {
        if (_selectedFaceIndex < 0 || _selectedFaceIndex >= _geometry.Faces.Length) return;
        var face = _geometry.Faces[_selectedFaceIndex];
        var indices = face.VertexIndices.ToList();
        int pos = indices.IndexOf(vertexIndex);
        if (pos < 0 || pos >= indices.Count - 1) return;
        (indices[pos], indices[pos + 1]) = (indices[pos + 1], indices[pos]);
        face.VertexIndices = [.. indices];
        _geometry.Invalidate();
        RebuildFaceMemberships();
        OnPropertyChanged(nameof(SelectedFaceVertexIndices));
        OnPropertyChanged(nameof(SelectedFaceVerticesInfo));
        GeometryChanged?.Invoke(this, EventArgs.Empty);
    }

    private bool CanMoveFaceVertexUp(int vertexIndex)
    {
        if (_selectedFaceIndex < 0 || _selectedFaceIndex >= _geometry.Faces.Length) return false;
        var indices = _geometry.Faces[_selectedFaceIndex].VertexIndices;
        return Array.IndexOf(indices, vertexIndex) > 0;
    }

    private bool CanMoveFaceVertexDown(int vertexIndex)
    {
        if (_selectedFaceIndex < 0 || _selectedFaceIndex >= _geometry.Faces.Length) return false;
        var indices = _geometry.Faces[_selectedFaceIndex].VertexIndices;
        int pos = Array.IndexOf(indices, vertexIndex);
        return pos >= 0 && pos < indices.Length - 1;
    }

    private void UpdateSelectedVertex()
    {
        if (_selectedVertexIndex < 0 || _selectedVertexIndex >= _geometry.Vertices.Length) return;
        float cx = Math.Clamp((float)_vertexX, 0, _effectRoomWidth);
        float cy = Math.Clamp((float)_vertexY, 0, _effectRoomHeight);
        float cz = Math.Clamp((float)_vertexZ, 0, _effectRoomDepth);
        _geometry.Vertices[_selectedVertexIndex] = new GeometryVertex(cx, cy, cz);
        if (_selectedVertexIndex < Vertices.Count)
        {
            Vertices[_selectedVertexIndex].X = cx;
            Vertices[_selectedVertexIndex].Y = cy;
            Vertices[_selectedVertexIndex].Z = cz;
        }
        _geometry.Invalidate();
        RebuildFaceMemberships();
        GeometryChanged?.Invoke(this, EventArgs.Empty);
    }

    private async Task SaveGeometryAsync()
    {
        var name = await _notifications.PromptAsync(Texts.EnterGeometryName, Texts.SaveGeometryTitle, _geometry.Name);
        if (string.IsNullOrWhiteSpace(name)) return;
        _geometry.Name = name;
        _geometry.ShapeId = name.Replace(" ", "_").ToLowerInvariant();
        _geometryService.Save(_geometry);
    }

    private void ExportBlueprint(string filePath) =>
        BlueprintManager.ExportBlueprint(_geometry, filePath);

    private void ImportBlueprint(string filePath)
    {
        var geo = BlueprintManager.ImportBlueprint(filePath);
        if (geo is null)
        {
            _notifications.ShowError(Texts.BlueprintErrorNoData);
            return;
        }
        PushUndo();
        Geometry = geo;
        geo.Invalidate();
        GeometryChanged?.Invoke(this, EventArgs.Empty);
    }

    private void Apply()
    {
        GeometryChanged?.Invoke(this, EventArgs.Empty);
        RequestClose?.Invoke(this, EventArgs.Empty);
    }

    private void RebuildFaceMemberships()
    {
        foreach (var item in FaceVertexMemberships)
            item.PropertyChanged -= OnFaceVertexMembershipChanged;
        FaceVertexMemberships.Clear();

        if (_selectedFaceIndex < 0 || _selectedFaceIndex >= _geometry.Faces.Length)
            return;

        var faceIndices = _geometry.Faces[_selectedFaceIndex].VertexIndices;

        for (int vi = 0; vi < _geometry.Vertices.Length; vi++)
        {
            var v = _geometry.Vertices[vi];
            int pos = Array.IndexOf(faceIndices, vi);
            var item = new FaceVertexMembership
            {
                VertexIndex = vi,
                VertexLabel = $"V{vi}  ({v.X:F2}, {v.Y:F2}, {v.Z:F2})",
                IsInFace = pos >= 0,
                FaceOrder = pos
            };
            item.PropertyChanged += OnFaceVertexMembershipChanged;
            FaceVertexMemberships.Add(item);
        }
    }

    private void OnFaceVertexMembershipChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_updatingMemberships || sender is not FaceVertexMembership item) return;
        if (e.PropertyName != nameof(FaceVertexMembership.IsInFace)) return;
        if (_selectedFaceIndex < 0 || _selectedFaceIndex >= _geometry.Faces.Length) return;

        _updatingMemberships = true;
        try
        {
            var face = _geometry.Faces[_selectedFaceIndex];
            var indices = face.VertexIndices.ToList();

            if (item.IsInFace && !indices.Contains(item.VertexIndex))
                indices.Add(item.VertexIndex);
            else if (!item.IsInFace)
                indices.Remove(item.VertexIndex);

            face.VertexIndices = [.. indices];
            _geometry.Invalidate();

            foreach (var m in FaceVertexMemberships)
            {
                int newPos = Array.IndexOf(face.VertexIndices, m.VertexIndex);
                m.IsInFace = newPos >= 0;
                m.FaceOrder = newPos;
            }
        }
        finally { _updatingMemberships = false; }

        OnPropertyChanged(nameof(SelectedFaceVertexIndices));
        OnPropertyChanged(nameof(SelectedFaceVerticesInfo));
        GeometryChanged?.Invoke(this, EventArgs.Empty);
    }

    private void RefreshCollections()
    {
        Vertices.Clear();
        for (int i = 0; i < _geometry.Vertices.Length; i++)
        {
            var v = _geometry.Vertices[i];
            Vertices.Add(new VertexItem(i, v.X, v.Y, v.Z));
        }
        FaceItems.Clear();
        for (int i = 0; i < _geometry.Faces.Length; i++)
        {
            var f = _geometry.Faces[i];
            string matName = f.MaterialIndex >= 0 && f.MaterialIndex < _geometry.Materials.Length
                ? _geometry.Materials[f.MaterialIndex].LocalizedName : "?";
            FaceItems.Add(new FaceItem(i, string.Join(", ", f.VertexIndices), matName));
        }
        NotifyVertexBounds();
        OnPropertyChanged(nameof(SelectedFaceVertexIndices));
        OnPropertyChanged(nameof(SelectedFaceVerticesInfo));
        OnPropertyChanged(nameof(HasSelectedVertex));
        OnPropertyChanged(nameof(HasSelectedFace));
        OnPropertyChanged(nameof(GeometryMaterials));
        RebuildFaceMemberships();
    }

    private void NotifyVertexBounds()
    {
        OnPropertyChanged(nameof(VertexXMax));
        OnPropertyChanged(nameof(VertexYMax));
        OnPropertyChanged(nameof(VertexZMax));
    }

    private void RefreshMaterials()
    {
        AvailableMaterials.Clear();
        foreach (var m in _materialService.GetAll())
            AvailableMaterials.Add(m);
    }
}
