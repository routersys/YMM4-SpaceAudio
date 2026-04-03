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

    public ObservableCollection<CustomMaterial> Materials { get; } = [];

    public CustomMaterial? SelectedMaterial
    {
        get => _selectedMaterial;
        set
        {
            if (!SetProperty(ref _selectedMaterial, value)) return;
            if (value is not null)
            {
                NewName = value.Name;
                NewAbsorption = value.Absorption;
                NewColor = value.MaterialColor;
            }
        }
    }

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

    public ICommand AddCommand { get; }
    public ICommand UpdateCommand { get; }
    public ICommand DeleteCommand { get; }

    public MaterialManagerViewModel() : this(ServiceLocator.MaterialService, ServiceLocator.NotificationService) { }

    public MaterialManagerViewModel(IMaterialService materialService, IUserNotificationService notifications)
    {
        _materialService = materialService;
        _notifications = notifications;

        AddCommand = new RelayCommand(_ => AddMaterial(), _ => !string.IsNullOrWhiteSpace(_newName));
        UpdateCommand = new RelayCommand(_ => UpdateMaterial(), _ => _selectedMaterial is not null && !_selectedMaterial.IsBuiltIn);
        DeleteCommand = new AsyncRelayCommand(DeleteMaterialAsync, _ => _selectedMaterial is not null && !_selectedMaterial.IsBuiltIn);

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
            MaterialColor = _newColor
        };
        _materialService.Save(mat);
        NewName = "";
        NewAbsorption = 0.1f;
        NewColor = CustomMaterial.GenerateCustomColor(Guid.NewGuid().ToString("N"));
    }

    private void UpdateMaterial()
    {
        if (_selectedMaterial is null || _selectedMaterial.IsBuiltIn) return;
        _selectedMaterial.Name = _newName;
        _selectedMaterial.Absorption = _newAbsorption;
        _selectedMaterial.MaterialColor = _newColor;
        _materialService.Save(_selectedMaterial);
    }

    private async Task DeleteMaterialAsync(object? _)
    {
        if (_selectedMaterial is null || _selectedMaterial.IsBuiltIn) return;
        if (!await _notifications.ConfirmAsync(
            string.Format(Texts.DeleteMaterialConfirm, _selectedMaterial.Name),
            Texts.Confirmation)) return;
        _materialService.Delete(_selectedMaterial.Id);
    }
}
