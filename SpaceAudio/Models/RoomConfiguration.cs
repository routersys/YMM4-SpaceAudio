using SpaceAudio.Enums;

namespace SpaceAudio.Models;

public sealed record RoomConfiguration
{
    public RoomShape Shape { get; set; } = RoomShape.Rectangular;
    public float Width { get; set; } = 8.0f;
    public float Height { get; set; } = 3.0f;
    public float Depth { get; set; } = 6.0f;
    public WallMaterial WallMaterial { get; set; } = WallMaterial.Drywall;
    public WallMaterial FloorMaterial { get; set; } = WallMaterial.Wood;
    public WallMaterial CeilingMaterial { get; set; } = WallMaterial.Drywall;
    
    public string WallMaterialId { get; set; } = "";
    public string FloorMaterialId { get; set; } = "";
    public string CeilingMaterialId { get; set; } = "";
    
    public float SourceX { get; set; } = 2.0f;
    public float SourceY { get; set; } = 1.5f;
    public float SourceZ { get; set; } = 3.0f;
    public float ListenerX { get; set; } = 6.0f;
    public float ListenerY { get; set; } = 1.5f;
    public float ListenerZ { get; set; } = 3.0f;
    public float PreDelayMs { get; set; } = 20.0f;
    public float DecayTime { get; set; } = 1.5f;
    public float HfDamping { get; set; } = 0.5f;
    public float Diffusion { get; set; } = 0.7f;
    public float EarlyLevel { get; set; } = -3.0f;
    public float LateLevel { get; set; } = -6.0f;
    public float DryWetMix { get; set; } = 0.3f;
    public string CustomGeometryId { get; set; } = "";
    public RoomGeometry? EmbeddedGeometry { get; set; }

    public RoomConfiguration DeepClone() => this with { EmbeddedGeometry = EmbeddedGeometry?.Clone() };
}
