using SpaceAudio.Interfaces;
using SpaceAudio.Localization;
using SpaceAudio.Models;
using SpaceAudio.Services;
using System.Collections.ObjectModel;
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
            Band125 = bands.Length > 0 ? bands[0] : value.Absorption;
            Band250 = bands.Length > 1 ? bands[1] : value.Absorption;
            Band500 = bands.Length > 2 ? bands[2] : value.Absorption;
            Band1k = bands.Length > 3 ? bands[3] : value.Absorption;
            Band2k = bands.Length > 4 ? bands[4] : value.Absorption;
            Band4k = bands.Length > 5 ? bands[5] : value.Absorption;
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
        set => SetProperty(ref _newName, value);
    }

    public float NewAbsorption
    {
        get => _newAbsorption;
        set
        {
            if (!SetProperty(ref _newAbsorption, Math.Clamp(value, 0.0f, 1.0f))) return;
            OnPropertyChanged(nameof(NewAbsorptionDouble));
        }
    }

    public Color NewColor
    {
        get => _newColor;
        set => SetProperty(ref _newColor, value);
    }

    public double NewAbsorptionDouble
    {
        get => _newAbsorption;
        set => NewAbsorption = (float)value;
    }

    public float Band125 { get => _newBands[0]; set { _newBands[0] = Math.Clamp(value, 0f, 0.99f); OnPropertyChanged(); SyncBroadband(); } }
    public float Band250 { get => _newBands[1]; set { _newBands[1] = Math.Clamp(value, 0f, 0.99f); OnPropertyChanged(); SyncBroadband(); } }
    public float Band500 { get => _newBands[2]; set { _newBands[2] = Math.Clamp(value, 0f, 0.99f); OnPropertyChanged(); SyncBroadband(); } }
    public float Band1k { get => _newBands[3]; set { _newBands[3] = Math.Clamp(value, 0f, 0.99f); OnPropertyChanged(); SyncBroadband(); } }
    public float Band2k { get => _newBands[4]; set { _newBands[4] = Math.Clamp(value, 0f, 0.99f); OnPropertyChanged(); SyncBroadband(); } }
    public float Band4k { get => _newBands[5]; set { _newBands[5] = Math.Clamp(value, 0f, 0.99f); OnPropertyChanged(); SyncBroadband(); } }

    public double Band125Double { get => Band125; set => Band125 = (float)value; }
    public double Band250Double { get => Band250; set => Band250 = (float)value; }
    public double Band500Double { get => Band500; set => Band500 = (float)value; }
    public double Band1kDouble { get => Band1k; set => Band1k = (float)value; }
    public double Band2kDouble { get => Band2k; set => Band2k = (float)value; }
    public double Band4kDouble { get => Band4k; set => Band4k = (float)value; }

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

    public MaterialManagerViewModel() : this(ServiceLocator.MaterialService, ServiceLocator.NotificationService) { }

    public MaterialManagerViewModel(IMaterialService materialService, IUserNotificationService notifications)
    {
        _materialService = materialService;
        _notifications = notifications;

        AddCommand = new RelayCommand(_ => AddMaterial(), _ => !string.IsNullOrWhiteSpace(_newName) && !_isBuiltIn);
        UpdateCommand = new RelayCommand(_ => UpdateMaterial(), _ => _selectedMaterial is not null && !_selectedMaterial.IsBuiltIn);
        DeleteCommand = new AsyncRelayCommand(DeleteMaterialAsync, _ => _selectedMaterial is not null && !_selectedMaterial.IsBuiltIn);
        DuplicateCommand = new RelayCommand(_ => DuplicateMaterial(), _ => _selectedMaterial is not null);

        _materialService.MaterialsChanged += (_, _) => LoadMaterials();
        LoadMaterials();
    }

    private void LoadMaterials()
    {
        Materials.Clear();
        foreach (var m in _materialService.GetAll())
            Materials.Add(m);
    }

    private void AddMaterial()
    {
        if (string.IsNullOrWhiteSpace(_newName)) return;
        var mat = new CustomMaterial
        {
            Name = _newName,
            Absorption = _newAbsorption,
            BandAbsorption = (float[])_newBands.Clone(),
            MaterialColor = _newColor
        };
        _materialService.Save(mat);
        ResetNewFields();
    }

    private void UpdateMaterial()
    {
        if (_selectedMaterial is null || _selectedMaterial.IsBuiltIn) return;
        _selectedMaterial.Name = _newName;
        _selectedMaterial.Absorption = _newAbsorption;
        _selectedMaterial.BandAbsorption = (float[])_newBands.Clone();
        _selectedMaterial.MaterialColor = _newColor;
        _materialService.Save(_selectedMaterial);
    }

    private void DuplicateMaterial()
    {
        if (_selectedMaterial is null) return;
        var clone = _selectedMaterial.Clone();
        clone.Id = Guid.NewGuid().ToString("N")[..8];
        clone.Name = _selectedMaterial.Name + " (copy)";
        clone.IsBuiltIn = false;
        _materialService.Save(clone);
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
        OnPropertyChanged(nameof(Band125)); OnPropertyChanged(nameof(Band250));
        OnPropertyChanged(nameof(Band500)); OnPropertyChanged(nameof(Band1k));
        OnPropertyChanged(nameof(Band2k)); OnPropertyChanged(nameof(Band4k));
        NewColor = CustomMaterial.GenerateCustomColor(Guid.NewGuid().ToString("N"));
        IsBuiltIn = false;
    }
}
