using SpaceAudio.Infrastructure;
using SpaceAudio.Interfaces;
using SpaceAudio.Localization;
using SpaceAudio.Models;
using SpaceAudio.Services;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Reflection;
using System.Windows;
using System.Windows.Input;

namespace SpaceAudio.ViewModels;

public sealed class RoomEditorViewModel : ViewModelBase
{

    private readonly IPresetService _presetService;
    private readonly IUserNotificationService _notifications;
    private readonly IUpdateService _updateService;

    private SpaceAudioEffect? _effect;
    private SpaceAudioEffect.RoomParameters? _roomParameters;
    private SpaceAudioEffect.SourceParameters? _sourceParameters;
    private SpaceAudioEffect.ListenerParameters? _listenerParameters;
    private SpaceAudioEffect.ReverbParameters? _reverbParameters;
    private SpaceAudioEffect.MaterialParameters? _materialParameters;
    private string _selectedPresetName = Texts.SelectPreset;
    private bool _isPopupOpen;
    private int _selectedTab;
    private object? _selectedTarget;
    private string _updateUrl = string.Empty;
    private bool _hasUpdate;

    public event EventHandler? RequestRedraw;
    public event EventHandler? BeginEdit;
    public event EventHandler? EndEdit;

    public SpaceAudioEffect? Effect
    {
        get => _effect;
        set
        {
            if (!SetProperty(ref _effect, value)) return;
            if (_effect is not null)
            {
                _roomParameters = _effect.GetRoomParameters();
                _sourceParameters = _effect.GetSourceParameters();
                _listenerParameters = _effect.GetListenerParameters();
                _reverbParameters = _effect.GetReverbParameters();
                _materialParameters = _effect.GetMaterialParameters();
            }
            else
            {
                _roomParameters = null;
                _sourceParameters = null;
                _listenerParameters = null;
                _reverbParameters = null;
                _materialParameters = null;
            }
            UpdateSelectedTarget();
        }
    }

    public string SelectedPresetName
    {
        get => _selectedPresetName;
        set => SetProperty(ref _selectedPresetName, value);
    }

    public bool IsPopupOpen
    {
        get => _isPopupOpen;
        set => SetProperty(ref _isPopupOpen, value);
    }

    public int SelectedTab
    {
        get => _selectedTab;
        set
        {
            if (!SetProperty(ref _selectedTab, value)) return;
            OnPropertyChanged(nameof(IsRoomTabActive));
            OnPropertyChanged(nameof(IsReverbTabActive));
            OnPropertyChanged(nameof(IsMaterialTabActive));
            UpdateSelectedTarget();
        }
    }

    public double EditorHeight
    {
        get => SpaceAudioSettings.Default.EditorHeight;
        set
        {
            SpaceAudioSettings.Default.EditorHeight = value;
            OnPropertyChanged();
        }
    }

    public object? SelectedTarget
    {
        get => _selectedTarget;
        private set => SetProperty(ref _selectedTarget, value);
    }

    public string VersionText => $"v{Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "0.0.0"}";

    public bool HasUpdate
    {
        get => _hasUpdate;
        set => SetProperty(ref _hasUpdate, value);
    }

    public string UpdateUrl
    {
        get => _updateUrl;
        set => SetProperty(ref _updateUrl, value);
    }

    public bool IsRoomTabActive => _selectedTab == 0;
    public bool IsReverbTabActive => _selectedTab == 1;
    public bool IsMaterialTabActive => _selectedTab == 2;

    public ObservableCollection<PresetInfo> Presets { get; } = [];

    public ICommand SavePresetCommand { get; }
    public ICommand LoadPresetCommand { get; }
    public ICommand DeletePresetCommand { get; }
    public ICommand RenamePresetCommand { get; }
    public ICommand SelectTabCommand { get; }
    public ICommand OpenUpdateUrlCommand { get; }

    public RoomEditorViewModel() : this(ServiceLocator.PresetService, ServiceLocator.NotificationService, ServiceLocator.UpdateService) { }

    public RoomEditorViewModel(IPresetService presetService, IUserNotificationService notifications, IUpdateService updateService)
    {
        _presetService = presetService;
        _notifications = notifications;
        _updateService = updateService;
        _presetService.PresetsChanged += (_, _) => LoadPresets();

        SavePresetCommand = new AsyncRelayCommand(_ => SavePresetAsync());
        LoadPresetCommand = new RelayCommand(LoadPreset, p => p is PresetInfo);
        DeletePresetCommand = new AsyncRelayCommand(DeletePresetAsync, p => p is PresetInfo);
        RenamePresetCommand = new AsyncRelayCommand(RenamePresetAsync, p => p is PresetInfo);
        SelectTabCommand = new RelayCommand(p => { if (p is string s && int.TryParse(s, out int t)) SelectedTab = t; });
        OpenUpdateUrlCommand = new RelayCommand(_ =>
        {
            if (!string.IsNullOrWhiteSpace(UpdateUrl))
            {
                Process.Start(new ProcessStartInfo { FileName = UpdateUrl, UseShellExecute = true });
            }
        });

        LoadPresets();
        _ = CheckForUpdatesAsync();
    }

    public EditScope CreateEditScope() => EditScope.Begin(
        () => BeginEdit?.Invoke(this, EventArgs.Empty),
        () => EndEdit?.Invoke(this, EventArgs.Empty));

    public void NotifyRedraw() => RequestRedraw?.Invoke(this, EventArgs.Empty);

    public RoomSnapshot CreateSnapshotFromEffect()
    {
        if (_effect is null) return default;
        return _effect.CreateSnapshot(0, 1, 60);
    }

    public RoomConfiguration ToConfiguration()
    {
        if (_effect is null) return new();
        return new()
        {
            Shape = _effect.RoomShapeValue,
            Width = _effect.RoomWidthValue,
            Height = _effect.RoomHeightValue,
            Depth = _effect.RoomDepthValue,
            WallMaterial = _effect.WallMaterialValue,
            FloorMaterial = _effect.FloorMaterialValue,
            CeilingMaterial = _effect.CeilingMaterialValue,
            SourceX = _effect.SourceXValue,
            SourceY = _effect.SourceYValue,
            SourceZ = _effect.SourceZValue,
            ListenerX = _effect.ListenerXValue,
            ListenerY = _effect.ListenerYValue,
            ListenerZ = _effect.ListenerZValue,
            PreDelayMs = _effect.PreDelayMsValue,
            DecayTime = _effect.DecayTimeValue,
            HfDamping = _effect.HfDampingValue,
            Diffusion = _effect.DiffusionValue,
            EarlyLevel = _effect.EarlyLevelValue,
            LateLevel = _effect.LateLevelValue,
            DryWetMix = _effect.DryWetMixValue
        };
    }

    public void SelectSourceTarget() => SelectedTarget = _sourceParameters;
    public void SelectListenerTarget() => SelectedTarget = _listenerParameters;
    public void SelectRoomTarget() => SelectedTarget = _roomParameters;

    private void UpdateSelectedTarget()
    {
        SelectedTarget = _selectedTab switch
        {
            1 => _reverbParameters,
            2 => _materialParameters,
            _ => _roomParameters
        };
    }

    private void LoadPresets()
    {
        Application.Current?.Dispatcher.Invoke(() =>
        {
            Presets.Clear();
            foreach (var name in _presetService.GetAllPresetNames())
                Presets.Add(_presetService.GetPresetInfo(name));
        });
    }

    private void LoadPreset(object? parameter)
    {
        if (parameter is not PresetInfo info || _effect is null) return;
        var config = _presetService.LoadPreset(info.Name);
        if (config is null) return;
        using (CreateEditScope()) ApplyConfiguration(config);
        SelectedPresetName = info.Name;
        IsPopupOpen = false;
        NotifyRedraw();
    }

    private void ApplyConfiguration(RoomConfiguration config)
    {
        if (_effect is null) return;
        _effect.RoomWidthValue = config.Width;
        _effect.RoomHeightValue = config.Height;
        _effect.RoomDepthValue = config.Depth;
        _effect.SourceXValue = config.SourceX;
        _effect.SourceYValue = config.SourceY;
        _effect.SourceZValue = config.SourceZ;
        _effect.ListenerXValue = config.ListenerX;
        _effect.ListenerYValue = config.ListenerY;
        _effect.ListenerZValue = config.ListenerZ;
        _effect.PreDelayMsValue = config.PreDelayMs;
        _effect.DecayTimeValue = config.DecayTime;
        _effect.HfDampingValue = config.HfDamping;
        _effect.DiffusionValue = config.Diffusion;
        _effect.EarlyLevelValue = config.EarlyLevel;
        _effect.LateLevelValue = config.LateLevel;
        _effect.DryWetMixValue = config.DryWetMix;
        _effect.WallMaterialValue = config.WallMaterial;
        _effect.FloorMaterialValue = config.FloorMaterial;
        _effect.CeilingMaterialValue = config.CeilingMaterial;
        _effect.RoomShapeValue = config.Shape;
    }

    private async Task SavePresetAsync()
    {
        var name = await _notifications.PromptAsync(Texts.EnterPresetName, Texts.SavePresetTitle);
        if (string.IsNullOrWhiteSpace(name)) return;
        if (_presetService.SavePreset(name, ToConfiguration()))
            SelectedPresetName = name;
    }

    private async Task DeletePresetAsync(object? parameter)
    {
        if (parameter is not PresetInfo info) return;
        if (!await _notifications.ConfirmAsync(string.Format(Texts.DeleteConfirm, info.Name), Texts.Confirmation)) return;
        _presetService.DeletePreset(info.Name);
    }

    private async Task RenamePresetAsync(object? parameter)
    {
        if (parameter is not PresetInfo info) return;
        var newName = await _notifications.PromptAsync(Texts.EnterNewName, Texts.RenamePresetTitle, info.Name);
        if (string.IsNullOrWhiteSpace(newName) || newName == info.Name) return;
        _presetService.RenamePreset(info.Name, newName);
    }

    private async Task CheckForUpdatesAsync()
    {
        var (hasUpdate, url) = await _updateService.CheckForUpdatesAsync();
        if (hasUpdate)
        {
            UpdateUrl = url;
            HasUpdate = true;
        }
    }
}
