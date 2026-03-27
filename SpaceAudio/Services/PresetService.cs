using SpaceAudio.Infrastructure;
using SpaceAudio.Interfaces;
using SpaceAudio.Localization;
using SpaceAudio.Models;
using System.IO;
using System.Reflection;

namespace SpaceAudio.Services;

public sealed class PresetService : IPresetService
{
    private const string PresetExtension = ".sap";
    private readonly string _presetsDir;
    private readonly IUserNotificationService _notifications;
    private readonly Lock _lock = new();
    private volatile bool _initialized;

    public event EventHandler? PresetsChanged;

    public PresetService(IUserNotificationService notifications)
    {
        _notifications = notifications;
        var assemblyDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? "";
        _presetsDir = Path.Combine(assemblyDir, "presets");
    }

    public IReadOnlyList<string> GetAllPresetNames()
    {
        EnsureInitialized();
        if (!Directory.Exists(_presetsDir)) return [];
        return Directory.GetFiles(_presetsDir, "*" + PresetExtension)
            .Select(Path.GetFileNameWithoutExtension)
            .Where(name => name is not null)
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToList()!;
    }

    public PresetInfo GetPresetInfo(string name)
    {
        EnsureInitialized();
        return new PresetInfo { Name = name };
    }

    public RoomConfiguration? LoadPreset(string name)
    {
        EnsureInitialized();
        string filePath = PresetPath(name);
        byte[]? data = AtomicFileOperations.ReadWithFallback(filePath);
        if (data is null) { _notifications.ShowError(Texts.PresetLoadFailed); return null; }
        var result = PresetFileFormat.Deserialize(data);
        if (result is null) _notifications.ShowError(Texts.PresetLoadFailed);
        return result;
    }

    public bool SavePreset(string name, RoomConfiguration config)
    {
        EnsureInitialized();
        if (!IsValidName(name)) { _notifications.ShowError(Texts.InvalidPresetName); return false; }
        try
        {
            byte[] data = PresetFileFormat.Serialize(config);
            AtomicFileOperations.Write(PresetPath(name), data);
            PresetsChanged?.Invoke(this, EventArgs.Empty);
            return true;
        }
        catch (Exception ex)
        {
            _notifications.ShowError($"{Texts.PresetSaveFailed}\n{ex.Message}");
            return false;
        }
    }

    public void DeletePreset(string name)
    {
        EnsureInitialized();
        string filePath = PresetPath(name);
        if (!File.Exists(filePath)) return;
        try { File.Delete(filePath); }
        catch (Exception ex) { _notifications.ShowError($"{Texts.PresetSaveFailed}\n{ex.Message}"); return; }
        PresetsChanged?.Invoke(this, EventArgs.Empty);
    }

    public bool RenamePreset(string oldName, string newName)
    {
        EnsureInitialized();
        if (!IsValidName(newName)) { _notifications.ShowError(Texts.InvalidPresetName); return false; }
        string oldPath = PresetPath(oldName);
        string newPath = PresetPath(newName);
        if (File.Exists(newPath)) { _notifications.ShowError(Texts.PresetExists); return false; }
        if (!File.Exists(oldPath)) return false;
        try { File.Move(oldPath, newPath); }
        catch (Exception ex) { _notifications.ShowError($"{Texts.PresetSaveFailed}\n{ex.Message}"); return false; }
        PresetsChanged?.Invoke(this, EventArgs.Empty);
        return true;
    }

    private void EnsureInitialized()
    {
        if (_initialized) return;
        lock (_lock) { if (_initialized) return; Directory.CreateDirectory(_presetsDir); _initialized = true; }
    }

    private string PresetPath(string name) => Path.Combine(_presetsDir, name + PresetExtension);

    private static bool IsValidName(string name) =>
        !string.IsNullOrWhiteSpace(name) && name.IndexOfAny(Path.GetInvalidFileNameChars()) < 0;
}
