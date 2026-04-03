using SpaceAudio.Infrastructure;
using SpaceAudio.Interfaces;
using SpaceAudio.Localization;
using SpaceAudio.Models;
using System.IO;
using System.Reflection;

namespace SpaceAudio.Services;

internal sealed class PresetService : FileBackedServiceBase, IPresetService
{
    private const string PresetExtension = ".sap";
    private readonly string _presetsDir;
    private readonly IUserNotificationService _notifications;

    public event EventHandler? PresetsChanged;

    public PresetService(IUserNotificationService notifications)
    {
        _notifications = notifications;
        var assemblyDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? "";
        _presetsDir = Path.Combine(assemblyDir, "presets");
    }

    protected override string DirectoryPath => _presetsDir;

    public IReadOnlyList<string> GetAllPresetNames()
    {
        EnsureInitialized();
        if (!Directory.Exists(_presetsDir)) return [];
        return [.. Directory.GetFiles(_presetsDir, "*" + PresetExtension)
            .Select(p => Path.GetFileNameWithoutExtension(p)!)
            .Order(StringComparer.OrdinalIgnoreCase)];
    }

    public PresetInfo GetPresetInfo(string name)
    {
        EnsureInitialized();
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return new PresetInfo { Name = name };
    }

    public RoomConfiguration? LoadPreset(string name)
    {
        EnsureInitialized();
        byte[]? data = AtomicFileOperations.ReadWithFallback(PresetPath(name));
        if (data is null) { _notifications.ShowError(Texts.PresetLoadFailed); return null; }
        var result = PresetFileFormat.Deserialize(data);
        if (result is null) _notifications.ShowError(Texts.PresetLoadFailed);
        return result;
    }

    public bool SavePreset(string name, RoomConfiguration config)
    {
        EnsureInitialized();
        ArgumentNullException.ThrowIfNull(config);
        if (!IsValidName(name)) { _notifications.ShowError(Texts.InvalidPresetName); return false; }
        try
        {
            byte[] data = PresetFileFormat.Serialize(config);
            AtomicFileOperations.Write(PresetPath(name), data);
            PresetsChanged?.Invoke(this, EventArgs.Empty);
            return true;
        }
        catch (IOException ex)
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
        catch (IOException ex) { _notifications.ShowError($"{Texts.PresetSaveFailed}\n{ex.Message}"); return; }
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
        catch (IOException ex) { _notifications.ShowError($"{Texts.PresetSaveFailed}\n{ex.Message}"); return false; }
        PresetsChanged?.Invoke(this, EventArgs.Empty);
        return true;
    }

    private string PresetPath(string name) => Path.Combine(_presetsDir, name + PresetExtension);

    private static bool IsValidName(string name) =>
        !string.IsNullOrWhiteSpace(name) && name.IndexOfAny(Path.GetInvalidFileNameChars()) < 0;
}
