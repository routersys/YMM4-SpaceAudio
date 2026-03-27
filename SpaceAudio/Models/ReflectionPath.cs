namespace SpaceAudio.Models;

public readonly record struct ReflectionPath(
    float DelaySeconds,
    float Attenuation,
    float PanLeft,
    float PanRight
);
