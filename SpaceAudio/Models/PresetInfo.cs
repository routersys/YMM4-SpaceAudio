namespace SpaceAudio.Models;

public sealed class PresetInfo
{
    public required string Name { get; init; }
    public string Group { get; set; } = "";
    public bool IsFavorite { get; set; }
}
