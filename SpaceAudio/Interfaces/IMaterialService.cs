using SpaceAudio.Models;

namespace SpaceAudio.Interfaces;

public interface IMaterialService
{
    event EventHandler? MaterialsChanged;
    IReadOnlyList<CustomMaterial> GetAll();
    IReadOnlyList<CustomMaterial> GetBuiltIn();
    CustomMaterial? GetById(string id);
    bool Save(CustomMaterial material);
    void Delete(string id);
}
