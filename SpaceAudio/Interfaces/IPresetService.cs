using SpaceAudio.Models;

namespace SpaceAudio.Interfaces;

public interface IPresetService
{
    event EventHandler? PresetsChanged;
    IReadOnlyList<string> GetAllPresetNames();
    PresetInfo GetPresetInfo(string name);
    RoomConfiguration? LoadPreset(string name);
    bool SavePreset(string name, RoomConfiguration config);
    void DeletePreset(string name);
    bool RenamePreset(string oldName, string newName);
}
