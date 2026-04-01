using SpaceAudio.Interfaces;
using SpaceAudio.Localization;
using SpaceAudio.Models;
using SpaceAudio.Services;
using System.Collections.ObjectModel;
using System.Windows.Input;

namespace SpaceAudio.ViewModels;

public sealed class RoomShapeEditorViewModel : ViewModelBase
{
    private readonly IRoomGeometryService _geometryService;
    private readonly IMaterialService _materialService;
    private readonly IUserNotificationService _notifications;

    private RoomGeometry _geometry;
    private int _selectedVertexIndex = -1;
    private int _selectedFaceIndex = -1;
    private float _vertexX;
    private float _vertexY;
    private float _vertexZ;

    public event EventHandler? GeometryChanged;
    public event EventHandler? RequestClose;

    public RoomGeometry Geometry
    {
        get => _geometry;
        set
        {
            if (!SetProperty(ref _geometry, value)) return;
            RefreshCollections();
        }
    }

    public ObservableCollection<VertexItem> Vertices { get; } = [];
    public ObservableCollection<FaceItem> FaceItems { get; } = [];
    public ObservableCollection<CustomMaterial> AvailableMaterials { get; } = [];

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
        }
    }

    public int SelectedFaceIndex
    {
        get => _selectedFaceIndex;
        set => SetProperty(ref _selectedFaceIndex, value);
    }

    public float VertexX
    {
        get => _vertexX;
        set { _vertexX = value; OnPropertyChanged(); UpdateSelectedVertex(); }
    }

    public float VertexY
    {
        get => _vertexY;
        set { _vertexY = value; OnPropertyChanged(); UpdateSelectedVertex(); }
    }

    public float VertexZ
    {
        get => _vertexZ;
        set { _vertexZ = value; OnPropertyChanged(); UpdateSelectedVertex(); }
    }

    public ICommand AddVertexCommand { get; }
    public ICommand RemoveVertexCommand { get; }
    public ICommand AddFaceCommand { get; }
    public ICommand RemoveFaceCommand { get; }
    public ICommand SaveGeometryCommand { get; }
    public ICommand ApplyCommand { get; }

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

        RefreshMaterials();
        _materialService.MaterialsChanged += (_, _) => RefreshMaterials();
    }

    private void AddVertex()
    {
        var center = _geometry.CalculateCenter();
        var list = _geometry.Vertices.ToList();
        list.Add(new GeometryVertex(center.X, center.Y, center.Z));
        _geometry.Vertices = [.. list];
        _geometry.Invalidate();
        RefreshCollections();
        SelectedVertexIndex = list.Count - 1;
        GeometryChanged?.Invoke(this, EventArgs.Empty);
    }

    private void RemoveVertex()
    {
        if (_selectedVertexIndex < 0 || _selectedVertexIndex >= _geometry.Vertices.Length) return;
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
        RefreshCollections();
        GeometryChanged?.Invoke(this, EventArgs.Empty);
    }

    private void AddFace()
    {
        if (_geometry.Vertices.Length < 3) return;
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
        var faces = _geometry.Faces.ToList();
        faces.RemoveAt(_selectedFaceIndex);
        _geometry.Faces = [.. faces];
        _geometry.Invalidate();
        SelectedFaceIndex = -1;
        RefreshCollections();
        GeometryChanged?.Invoke(this, EventArgs.Empty);
    }

    public void UpdateFaceIndices(int faceIndex, int[] indices)
    {
        if (faceIndex < 0 || faceIndex >= _geometry.Faces.Length) return;
        _geometry.Faces[faceIndex].VertexIndices = indices;
        _geometry.Invalidate();
        GeometryChanged?.Invoke(this, EventArgs.Empty);
    }

    public void UpdateFaceMaterial(int faceIndex, int materialIndex)
    {
        if (faceIndex < 0 || faceIndex >= _geometry.Faces.Length) return;
        _geometry.Faces[faceIndex].MaterialIndex = materialIndex;
        _geometry.Invalidate();
        GeometryChanged?.Invoke(this, EventArgs.Empty);
    }

    private void UpdateSelectedVertex()
    {
        if (_selectedVertexIndex < 0 || _selectedVertexIndex >= _geometry.Vertices.Length) return;
        _geometry.Vertices[_selectedVertexIndex] = new GeometryVertex(_vertexX, _vertexY, _vertexZ);
        _geometry.Invalidate();
        if (_selectedVertexIndex < Vertices.Count)
        {
            Vertices[_selectedVertexIndex] = new VertexItem(_selectedVertexIndex, _vertexX, _vertexY, _vertexZ);
        }
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

    private void Apply()
    {
        GeometryChanged?.Invoke(this, EventArgs.Empty);
        RequestClose?.Invoke(this, EventArgs.Empty);
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
                ? _geometry.Materials[f.MaterialIndex].Name : "?";
            FaceItems.Add(new FaceItem(i, string.Join(",", f.VertexIndices), matName));
        }
    }

    private void RefreshMaterials()
    {
        AvailableMaterials.Clear();
        foreach (var m in _materialService.GetAll())
            AvailableMaterials.Add(m);
    }

    public sealed record VertexItem(int Index, float X, float Y, float Z)
    {
        public override string ToString() => $"V{Index}: ({X:F2}, {Y:F2}, {Z:F2})";
    }

    public sealed record FaceItem(int Index, string Indices, string Material)
    {
        public override string ToString() => $"F{Index}: [{Indices}] ({Material})";
    }
}
