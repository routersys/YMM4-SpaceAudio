using SpaceAudio.Enums;

namespace SpaceAudio.Models;

public readonly record struct RoomSnapshot(
    float Width,
    float Height,
    float Depth,
    float SourceX,
    float SourceY,
    float SourceZ,
    float ListenerX,
    float ListenerY,
    float ListenerZ,
    float PreDelayMs,
    float DecayTime,
    float HfDamping,
    float Diffusion,
    float EarlyLevel,
    float LateLevel,
    float DryWetMix,
    float WallAbsorption,
    float FloorAbsorption,
    float CeilingAbsorption,
    float WallSpectralDamping,
    float FloorSpectralDamping,
    float CeilingSpectralDamping,
    ReverbQuality Quality,
    RoomShape Shape,
    string WallMaterialId,
    string FloorMaterialId,
    string CeilingMaterialId,
    RoomGeometry? Geometry = null
);
