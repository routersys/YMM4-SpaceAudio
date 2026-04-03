using SpaceAudio.Interfaces;
using SpaceAudio.Localization;
using SpaceAudio.Models;
using SpaceAudio.Services;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using System.Windows.Media;

namespace SpaceAudio.ViewModels;

public sealed class MaterialManagerViewModel : ViewModelBase
{
    private readonly IMaterialService _materialService;
    private readonly IUserNotificationService _notifications;
    private CustomMaterial? _selectedMaterial;
    private string _newName = "";
    private float _newAbsorption = 0.1f;
    private Color _newColor = CustomMaterial.GenerateCustomColor(Guid.NewGuid().ToString("N"));
    private float[] _newBands = new float[6];
    private bool _isBuiltIn;

    public ObservableCollection<CustomMaterial> Materials { get; } = [];

    public CustomMaterial? SelectedMaterial
    {
        get => _selectedMaterial;
        set
        {
            if (!SetProperty(ref _selectedMaterial, value)) return;
            if (value is null) return;

            NewName = value.Name;
            NewAbsorption = value.Absorption;
            NewColor = value.MaterialColor;
            IsBuiltIn = value.IsBuiltIn;

            var bands = value.GetEffectiveBandAbsorption();
            _newBands[0] = Math.Clamp(bands.Length > 0 ? bands[0] : value.Absorption, 0f, 0.99f);
            _newBands[1] = Math.Clamp(bands.Length > 1 ? bands[1] : value.Absorption, 0f, 0.99f);
            _newBands[2] = Math.Clamp(bands.Length > 2 ? bands[2] : value.Absorption, 0f, 0.99f);
            _newBands[3] = Math.Clamp(bands.Length > 3 ? bands[3] : value.Absorption, 0f, 0.99f);
            _newBands[4] = Math.Clamp(bands.Length > 4 ? bands[4] : value.Absorption, 0f, 0.99f);
            _newBands[5] = Math.Clamp(bands.Length > 5 ? bands[5] : value.Absorption, 0f, 0.99f);
            
            OnPropertyChanged(nameof(Band125Double));
            OnPropertyChanged(nameof(Band250Double));
            OnPropertyChanged(nameof(Band500Double));
            OnPropertyChanged(nameof(Band1kDouble));
            OnPropertyChanged(nameof(Band2kDouble));
            OnPropertyChanged(nameof(Band4kDouble));
            
            SyncBroadband();
            CommandManager.InvalidateRequerySuggested();
        }
    }

    public bool IsBuiltIn
    {
        get => _isBuiltIn;
        private set
        {
            if (!SetProperty(ref _isBuiltIn, value)) return;
            OnPropertyChanged(nameof(CanEdit));
        }
    }

    public bool CanEdit => !_isBuiltIn;

    public string NewName
    {
        get => _newName;
        set { if (SetProperty(ref _newName, value)) CommandManager.InvalidateRequerySuggested(); }
    }

    public float NewAbsorption
    {
        get => _newAbsorption;
        set { if (SetProperty(ref _newAbsorption, Math.Clamp(value, 0f, 0.99f))) CommandManager.InvalidateRequerySuggested(); }
    }

    public Color NewColor
    {
        get => _newColor;
        set { if (SetProperty(ref _newColor, value)) CommandManager.InvalidateRequerySuggested(); }
    }

    public double NewAbsorptionDouble
    {
        get => _newAbsorption;
        set => NewAbsorption = (float)value;
    }

    public double Band125Double { get => _newBands[0]; set { _newBands[0] = Math.Clamp((float)value, 0f, 0.99f); OnPropertyChanged(); SyncBroadband(); CommandManager.InvalidateRequerySuggested(); } }
    public double Band250Double { get => _newBands[1]; set { _newBands[1] = Math.Clamp((float)value, 0f, 0.99f); OnPropertyChanged(); SyncBroadband(); CommandManager.InvalidateRequerySuggested(); } }
    public double Band500Double { get => _newBands[2]; set { _newBands[2] = Math.Clamp((float)value, 0f, 0.99f); OnPropertyChanged(); SyncBroadband(); CommandManager.InvalidateRequerySuggested(); } }
    public double Band1kDouble { get => _newBands[3]; set { _newBands[3] = Math.Clamp((float)value, 0f, 0.99f); OnPropertyChanged(); SyncBroadband(); CommandManager.InvalidateRequerySuggested(); } }
    public double Band2kDouble { get => _newBands[4]; set { _newBands[4] = Math.Clamp((float)value, 0f, 0.99f); OnPropertyChanged(); SyncBroadband(); CommandManager.InvalidateRequerySuggested(); } }
    public double Band4kDouble { get => _newBands[5]; set { _newBands[5] = Math.Clamp((float)value, 0f, 0.99f); OnPropertyChanged(); SyncBroadband(); CommandManager.InvalidateRequerySuggested(); } }

    private void SyncBroadband()
    {
        float sum = 0;
        for (int i = 0; i < 6; i++) sum += _newBands[i];
        NewAbsorption = sum / 6.0f;
    }

    public ICommand AddCommand { get; }
    public ICommand UpdateCommand { get; }
    public ICommand DeleteCommand { get; }
    public ICommand DuplicateCommand { get; }
    public ICommand MoveUpCommand { get; }
    public ICommand MoveDownCommand { get; }

    public MaterialManagerViewModel() : this(ServiceLocator.MaterialService, ServiceLocator.NotificationService) { }

    public MaterialManagerViewModel(IMaterialService materialService, IUserNotificationService notifications)
    {
        _materialService = materialService;
        _notifications = notifications;

        AddCommand = new RelayCommand(_ => AddMaterial(), _ => !string.IsNullOrWhiteSpace(_newName));
        UpdateCommand = new RelayCommand(_ => UpdateMaterial(), _ => _selectedMaterial is not null && CheckIsDirty());
        DeleteCommand = new AsyncRelayCommand(DeleteMaterialAsync, _ => _selectedMaterial is not null && !_selectedMaterial.IsBuiltIn);
        DuplicateCommand = new RelayCommand(_ => DuplicateMaterial(), _ => _selectedMaterial is not null);
        MoveUpCommand = new RelayCommand(_ => MoveUp(), _ => CanMoveUp());
        MoveDownCommand = new RelayCommand(_ => MoveDown(), _ => CanMoveDown());

        _materialService.MaterialsChanged += (_, _) => LoadMaterials();
        LoadMaterials();
    }

    private void LoadMaterials()
    {
        Materials.Clear();
        foreach (var m in _materialService.GetAll())
            Materials.Add(m);
        CommandManager.InvalidateRequerySuggested();
    }

    private void AddMaterial()
    {
        var mat = new CustomMaterial
        {
            Name = Texts.NewMaterial,
            Absorption = 0.1f,
            BandAbsorption = [0.1f, 0.1f, 0.1f, 0.1f, 0.1f, 0.1f],
            IsBuiltIn = false
        };
        _materialService.Save(mat);
        SelectedMaterial = Materials.FirstOrDefault(m => m.Id == mat.Id) ?? mat;
    }

    private bool CheckIsDirty()
    {
        if (_selectedMaterial == null) return false;
        if (_newName != _selectedMaterial.Name) return true;
        if (_newColor != _selectedMaterial.MaterialColor) return true;
        if (Math.Abs(_newAbsorption - _selectedMaterial.Absorption) > 0.001f) return true;
        var bands = _selectedMaterial.GetEffectiveBandAbsorption();
        for (int i = 0; i < 6 && i < bands.Length; i++)
            if (Math.Abs(_newBands[i] - bands[i]) > 0.001f) return true;
        return false;
    }

    private void UpdateMaterial()
    {
        if (_selectedMaterial is null) return;
        _selectedMaterial.Name = _newName;
        _selectedMaterial.Absorption = _newAbsorption;
        _selectedMaterial.BandAbsorption = (float[])_newBands.Clone();
        _selectedMaterial.MaterialColor = _newColor;
        _materialService.Save(_selectedMaterial);
        _selectedMaterial = null;
        LoadMaterials();
    }

    private void DuplicateMaterial()
    {
        if (_selectedMaterial is null) return;
        var clone = _selectedMaterial.Clone();
        clone.Id = Guid.NewGuid().ToString("N")[..8];
        clone.Name = string.Format(Texts.DuplicateMaterialFormat, _selectedMaterial.Name);
        clone.IsBuiltIn = false;
        _materialService.Save(clone);
        SelectedMaterial = clone;
    }

    private bool CanMoveUp()
    {
        if (_selectedMaterial == null || _selectedMaterial.IsBuiltIn) return false;
        int idx = Materials.IndexOf(_selectedMaterial);
        var builtInCount = _materialService.GetBuiltIn().Count;
        return idx > builtInCount;
    }

    private bool CanMoveDown()
    {
        if (_selectedMaterial == null || _selectedMaterial.IsBuiltIn) return false;
        int idx = Materials.IndexOf(_selectedMaterial);
        return idx >= 0 && idx < Materials.Count - 1;
    }

    private void MoveUp()
    {
        if (_selectedMaterial == null) return;
        string id = _selectedMaterial.Id;
        _materialService.MoveUp(id);
        SelectedMaterial = Materials.FirstOrDefault(m => m.Id == id);
    }

    private void MoveDown()
    {
        if (_selectedMaterial == null) return;
        string id = _selectedMaterial.Id;
        _materialService.MoveDown(id);
        SelectedMaterial = Materials.FirstOrDefault(m => m.Id == id);
    }

    private async Task DeleteMaterialAsync(object? _)
    {
        if (_selectedMaterial is null || _selectedMaterial.IsBuiltIn) return;
        if (!await _notifications.ConfirmAsync(
            string.Format(Texts.DeleteMaterialConfirm, _selectedMaterial.Name),
            Texts.Confirmation)) return;
        _materialService.Delete(_selectedMaterial.Id);
    }

    private void ResetNewFields()
    {
        NewName = "";
        NewAbsorption = 0.1f;
        _newBands = [0.1f, 0.1f, 0.1f, 0.1f, 0.1f, 0.1f];
        OnPropertyChanged(nameof(Band125Double)); OnPropertyChanged(nameof(Band250Double));
        OnPropertyChanged(nameof(Band500Double)); OnPropertyChanged(nameof(Band1kDouble));
        OnPropertyChanged(nameof(Band2kDouble)); OnPropertyChanged(nameof(Band4kDouble));
        NewColor = CustomMaterial.GenerateCustomColor(Guid.NewGuid().ToString("N"));
        IsBuiltIn = false;
    }
}
