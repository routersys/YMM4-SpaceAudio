using SpaceAudio.Localization;

namespace SpaceAudio.Models;

public sealed class CustomMaterial
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8];
    public string Name { get; set; } = "";
    public float Absorption { get; set; } = 0.1f;
    public bool IsBuiltIn { get; set; }

    public string LocalizedName => Name switch
    {
        "Floor" => Texts.FloorLabel,
        "Ceiling" or "Ceil" => Texts.CeilingLabel,
        "Wall" => Texts.WallLabel,
        _ => Name
    };

    public CustomMaterial() { }

    public CustomMaterial(string id, string name, float absorption, bool builtIn = false)
    {
        Id = id;
        Name = name;
        Absorption = absorption;
        IsBuiltIn = builtIn;
    }

    public CustomMaterial Clone() => new()
    {
        Id = Id,
        Name = Name,
        Absorption = Absorption,
        IsBuiltIn = IsBuiltIn
    };
}
