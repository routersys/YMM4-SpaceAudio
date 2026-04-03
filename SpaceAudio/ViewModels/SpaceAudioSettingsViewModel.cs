using SpaceAudio.Enums;
using SpaceAudio.Interfaces;
using SpaceAudio.Localization;
using SpaceAudio.Models;
using SpaceAudio.Services;
using System.Collections.ObjectModel;
using System.Windows.Input;

namespace SpaceAudio.ViewModels;

public sealed record QualityItem(string DisplayName, ReverbQuality Value);

public sealed class SpaceAudioSettingsViewModel : ViewModelBase
{
    private readonly IPresetService _presetService;
    private PresetInfo? _selectedPreset;
    private QualityItem? _selectedQualityItem;
    private string? _defaultPreset;

    public ObservableCollection<PresetInfo> Presets { get; } = [];
    public ObservableCollection<string> AllPresetNames { get; } = [];
    public IReadOnlyList<QualityItem> QualityOptions { get; } =
    [
        new(Texts.QualityEconomy, ReverbQuality.Economy),
        new(Texts.QualityStandard, ReverbQuality.Standard),
        new(Texts.QualityHigh, ReverbQuality.High)
    ];

    public PresetInfo? SelectedPreset
    {
        get => _selectedPreset;
        set => SetProperty(ref _selectedPreset, value);
    }

    public QualityItem? SelectedQuality
    {
        get => _selectedQualityItem;
        set
        {
            if (!SetProperty(ref _selectedQualityItem, value) || value is null) return;
            SpaceAudioSettings.Default.Quality = value.Value;
            SpaceAudioSettings.Default.Save();
        }
    }

    public string? SelectedDefaultPreset
    {
        get => _defaultPreset;
        set
        {
            if (!SetProperty(ref _defaultPreset, value) || value is null) return;
            SpaceAudioSettings.Default.DefaultPreset = value;
            SpaceAudioSettings.Default.Save();
        }
    }

    public ICommand DeleteCommand { get; }

    public SpaceAudioSettingsViewModel() : this(ServiceLocator.PresetService) { }

    public SpaceAudioSettingsViewModel(IPresetService presetService)
    {
        _presetService = presetService;
        _selectedQualityItem = QualityOptions.FirstOrDefault(q => q.Value == SpaceAudioSettings.Default.Quality) ?? QualityOptions[0];
        _presetService.PresetsChanged += (_, _) => LoadData();
        DeleteCommand = new RelayCommand(_ => DeletePreset(), _ => SelectedPreset is not null);
        LoadData();
    }

    private void LoadData()
    {
        Presets.Clear();
        AllPresetNames.Clear();
        AllPresetNames.Add("");
        foreach (var name in _presetService.GetAllPresetNames())
        {
            Presets.Add(_presetService.GetPresetInfo(name));
            AllPresetNames.Add(name);
        }
        _defaultPreset = string.IsNullOrEmpty(SpaceAudioSettings.Default.DefaultPreset)
            ? ""
            : SpaceAudioSettings.Default.DefaultPreset;
        OnPropertyChanged(nameof(SelectedDefaultPreset));
    }

    private void DeletePreset()
    {
        if (SelectedPreset is null) return;
        _presetService.DeletePreset(SelectedPreset.Name);
    }
}
