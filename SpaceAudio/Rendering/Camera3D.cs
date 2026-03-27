using System.Runtime.CompilerServices;

namespace SpaceAudio.Rendering;

internal sealed class Camera3D
{
    private float _yaw = -30.0f;
    private float _pitch = 25.0f;
    private float _distance = 18.0f;
    private float _targetX;
    private float _targetY = 1.5f;
    private float _targetZ;

    public float Yaw
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _yaw;
        set => _yaw = value % 360.0f;
    }

    public float Pitch
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _pitch;
        set => _pitch = Math.Clamp(value, -89.0f, 89.0f);
    }

    public float Distance
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _distance;
        set => _distance = Math.Clamp(value, 2.0f, 100.0f);
    }

    public float TargetX { get => _targetX; set => _targetX = value; }
    public float TargetY { get => _targetY; set => _targetY = value; }
    public float TargetZ { get => _targetZ; set => _targetZ = value; }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public (float X, float Y, float Z) GetEyePosition()
    {
        float yawRad = _yaw * MathF.PI / 180.0f;
        float pitchRad = _pitch * MathF.PI / 180.0f;
        float cosPitch = MathF.Cos(pitchRad);

        return (
            _targetX + _distance * cosPitch * MathF.Sin(yawRad),
            _targetY + _distance * MathF.Sin(pitchRad),
            _targetZ + _distance * cosPitch * MathF.Cos(yawRad)
        );
    }

    public void Rotate(float deltaYaw, float deltaPitch)
    {
        Yaw += deltaYaw;
        Pitch += deltaPitch;
    }

    public void Pan(float deltaX, float deltaY)
    {
        float yawRad = _yaw * MathF.PI / 180.0f;
        float cosY = MathF.Cos(yawRad);
        float sinY = MathF.Sin(yawRad);

        float scale = _distance * 0.002f;
        _targetX -= (deltaX * cosY) * scale;
        _targetZ += (deltaX * sinY) * scale;
        _targetY += deltaY * scale;
    }

    public void ZoomIn() => Distance = Math.Max(2.0f, _distance * 0.85f);
    public void ZoomOut() => Distance = Math.Min(100.0f, _distance * 1.18f);

    public void ZoomByDelta(float delta)
    {
        float factor = delta > 0 ? 0.9f : 1.11f;
        Distance *= factor;
    }

    public void SetTopView(float cx, float cy, float cz)
    {
        _targetX = cx;
        _targetY = 0;
        _targetZ = cz;
        _yaw = 0;
        _pitch = 89.0f;
        _distance = Math.Max(cx, cz) * 2.5f + 5;
    }

    public void SetFrontView(float cx, float cy, float cz)
    {
        _targetX = cx;
        _targetY = cy;
        _targetZ = cz;
        _yaw = 0;
        _pitch = 5;
        _distance = 18.0f;
    }

    public void SetSideView(float cx, float cy, float cz)
    {
        _targetX = cx;
        _targetY = cy;
        _targetZ = cz;
        _yaw = 90;
        _pitch = 5;
        _distance = 18.0f;
    }

    public void Reset(float cx, float cy, float cz)
    {
        _targetX = cx;
        _targetY = cy;
        _targetZ = cz;
        _yaw = -30.0f;
        _pitch = 25.0f;
        _distance = 18.0f;
    }
}
