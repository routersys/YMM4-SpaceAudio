using SpaceAudio.Models;

namespace SpaceAudio.Interfaces;

public interface IRoomGeometryService
{
    event EventHandler? GeometriesChanged;
    IReadOnlyList<string> GetAllIds();
    RoomGeometry? Load(string id);
    bool Save(RoomGeometry geometry);
    void Delete(string id);
    bool Rename(string oldId, string newId);
}
