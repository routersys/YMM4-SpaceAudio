using Newtonsoft.Json;
using SpaceAudio.Localization;
using System.Windows.Media;

namespace SpaceAudio.Models;

public sealed class CustomMaterial
{
    private const string BuiltInFloor = "Floor";
    private const string BuiltInCeiling = "Ceiling";
    private const string BuiltInCeilingAlt = "Ceil";
    private const string BuiltInWall = "Wall";

    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8];
    public string Name { get; set; } = "";
    public float Absorption { get; set; } = 0.1f;
    public bool IsBuiltIn { get; set; }

    [JsonIgnore]
    public Color MaterialColor { get; set; }

    [JsonProperty("Color")]
    public string MaterialColorHex
    {
        get => MaterialColor.ToString();
        set
        {
            try { MaterialColor = (Color)ColorConverter.ConvertFromString(value); }
            catch { MaterialColor = GenerateCustomColor(Id); }
        }
    }

    public string LocalizedName => Name switch
    {
        BuiltInFloor => Texts.FloorLabel,
        BuiltInCeiling or BuiltInCeilingAlt => Texts.CeilingLabel,
        BuiltInWall => Texts.WallLabel,
        _ => Name
    };

    public CustomMaterial() 
    {
        MaterialColor = GenerateCustomColor(Id);
    }

    public CustomMaterial(string id, string name, float absorption, bool builtIn = false, Color? defaultColor = null)
    {
        Id = id;
        Name = name;
        Absorption = absorption;
        IsBuiltIn = builtIn;
        MaterialColor = defaultColor ?? GenerateCustomColor(id);
    }

    public CustomMaterial Clone() => new()
    {
        Id = Id,
        Name = Name,
        Absorption = Absorption,
        IsBuiltIn = IsBuiltIn,
        MaterialColor = MaterialColor
    };

    public static Color GenerateCustomColor(string m)
    {
        if (string.IsNullOrEmpty(m)) return Color.FromRgb(120, 120, 120);
        int hash = m.GetHashCode();
        byte r = (byte)(100 + (hash & 0xFF) % 100);
        byte g = (byte)(100 + ((hash >> 8) & 0xFF) % 100);
        byte b = (byte)(100 + ((hash >> 16) & 0xFF) % 100);
        return Color.FromRgb(r, g, b);
    }
}
