using SpaceAudio.Enums;
using System.Runtime.CompilerServices;

namespace SpaceAudio.Models;

public sealed class RoomGeometry
{
    public string Name { get; set; } = "";
    public string ShapeId { get; set; } = "";
    public GeometryVertex[] Vertices { get; set; } = [];
    public RoomFace[] Faces { get; set; } = [];
    public CustomMaterial[] Materials { get; set; } = [];

    private FacePlane[]? _cachedPlanes;
    private bool _planesDirty = true;

    public void Invalidate() => _planesDirty = true;

    public FacePlane[] GetPlanes()
    {
        if (!_planesDirty && _cachedPlanes is not null) return _cachedPlanes;
        _cachedPlanes = BuildPlanes();
        _planesDirty = false;
        return _cachedPlanes;
    }

    private FacePlane[] BuildPlanes()
    {
        if (Faces.Length == 0 || Vertices.Length < 3) return [];
        var planes = new FacePlane[Faces.Length];
        for (int i = 0; i < Faces.Length; i++)
        {
            var face = Faces[i];
            if (face.VertexIndices.Length < 3) continue;
            int i0 = face.VertexIndices[0], i1 = face.VertexIndices[1], i2 = face.VertexIndices[2];
            if (i0 >= Vertices.Length || i1 >= Vertices.Length || i2 >= Vertices.Length) continue;
            float abs = face.MaterialIndex >= 0 && face.MaterialIndex < Materials.Length
                ? Materials[face.MaterialIndex].Absorption : 0.1f;
            planes[i] = FacePlane.FromVertices(in Vertices[i0], in Vertices[i1], in Vertices[i2], abs);
        }
        return planes;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public float CalculateVolume()
    {
        float volume = 0;
        foreach (var face in Faces)
        {
            if (face.VertexIndices.Length < 3) continue;
            var v0 = Vertices[face.VertexIndices[0]];
            for (int i = 1; i < face.VertexIndices.Length - 1; i++)
            {
                var v1 = Vertices[face.VertexIndices[i]];
                var v2 = Vertices[face.VertexIndices[i + 1]];
                volume += GeometryVertex.Dot(in v0, GeometryVertex.Cross(in v1, in v2));
            }
        }
        return MathF.Abs(volume) / 6.0f;
    }

    public float CalculateSurfaceArea()
    {
        float area = 0;
        foreach (var face in Faces)
        {
            if (face.VertexIndices.Length < 3) continue;
            var v0 = Vertices[face.VertexIndices[0]];
            for (int i = 1; i < face.VertexIndices.Length - 1; i++)
            {
                var e1 = Vertices[face.VertexIndices[i]].Subtract(in v0);
                var e2 = Vertices[face.VertexIndices[i + 1]].Subtract(in v0);
                area += GeometryVertex.Cross(in e1, in e2).Length();
            }
        }
        return area * 0.5f;
    }

    public GeometryVertex CalculateCenter()
    {
        if (Vertices.Length == 0) return default;
        float cx = 0, cy = 0, cz = 0;
        foreach (var v in Vertices) { cx += v.X; cy += v.Y; cz += v.Z; }
        float inv = 1.0f / Vertices.Length;
        return new(cx * inv, cy * inv, cz * inv);
    }

    public RoomGeometry Clone() => new()
    {
        Name = Name,
        ShapeId = ShapeId,
        Vertices = Vertices.Select(v => v).ToArray(),
        Faces = Faces.Select(f => f.Clone()).ToArray(),
        Materials = Materials.Select(m => m.Clone()).ToArray()
    };

    public static RoomGeometry CreateBox(float w, float h, float d, float wallAbs, float floorAbs, float ceilAbs)
    {
        var geo = new RoomGeometry
        {
            Name = "Box",
            ShapeId = "box",
            Vertices =
            [
                new(0, 0, 0), new(w, 0, 0), new(w, h, 0), new(0, h, 0),
                new(0, 0, d), new(w, 0, d), new(w, h, d), new(0, h, d)
            ],
            Materials =
            [
                new("wall", "Wall", wallAbs, true),
                new("floor", "Floor", floorAbs, true),
                new("ceil", "Ceiling", ceilAbs, true)
            ],
            Faces =
            [
                new([0, 1, 2, 3], 0),
                new([5, 4, 7, 6], 0),
                new([4, 0, 3, 7], 0),
                new([1, 5, 6, 2], 0),
                new([4, 5, 1, 0], 1),
                new([3, 2, 6, 7], 2)
            ]
        };
        return geo;
    }

    public static RoomGeometry CreateLShaped(float w, float h, float d, float wallAbs, float floorAbs, float ceilAbs)
    {
        float hw = w * 0.5f, hd = d * 0.5f;
        var geo = new RoomGeometry
        {
            Name = "L-Shaped",
            ShapeId = "lshaped",
            Vertices =
            [
                new(0, 0, 0), new(w, 0, 0), new(w, 0, hd), new(hw, 0, hd),
                new(hw, 0, d), new(0, 0, d),
                new(0, h, 0), new(w, h, 0), new(w, h, hd), new(hw, h, hd),
                new(hw, h, d), new(0, h, d)
            ],
            Materials =
            [
                new("wall", "Wall", wallAbs, true),
                new("floor", "Floor", floorAbs, true),
                new("ceil", "Ceiling", ceilAbs, true)
            ],
            Faces =
            [
                new([0, 1, 7, 6], 0),
                new([1, 2, 8, 7], 0),
                new([2, 3, 9, 8], 0),
                new([3, 4, 10, 9], 0),
                new([4, 5, 11, 10], 0),
                new([5, 0, 6, 11], 0),
                new([0, 5, 4, 3, 2, 1], 1),
                new([6, 7, 8, 9, 10, 11], 2)
            ]
        };
        return geo;
    }

    public static RoomGeometry CreateCathedral(float w, float h, float d, float wallAbs, float floorAbs, float ceilAbs)
    {
        float hc = h * 1.5f;
        float hw = w * 0.5f;
        var geo = new RoomGeometry
        {
            Name = "Cathedral",
            ShapeId = "cathedral",
            Vertices =
            [
                new(0, 0, 0), new(w, 0, 0), new(w, h, 0), new(hw, hc, 0), new(0, h, 0),
                new(0, 0, d), new(w, 0, d), new(w, h, d), new(hw, hc, d), new(0, h, d)
            ],
            Materials =
            [
                new("wall", "Wall", wallAbs, true),
                new("floor", "Floor", floorAbs, true),
                new("ceil", "Ceiling", ceilAbs, true)
            ],
            Faces =
            [
                new([0, 1, 2, 3, 4], 0),
                new([6, 5, 9, 8, 7], 0),
                new([5, 0, 4, 9], 0),
                new([1, 6, 7, 2], 0),
                new([0, 5, 6, 1], 1),
                new([4, 3, 8, 9], 2),
                new([2, 7, 8, 3], 2)
            ]
        };
        return geo;
    }

    public static RoomGeometry CreateStudio(float w, float h, float d, float wallAbs, float floorAbs, float ceilAbs)
    {
        float hs = h * 0.8f;
        var geo = new RoomGeometry
        {
            Name = "Studio",
            ShapeId = "studio",
            Vertices =
            [
                new(0, 0, 0), new(w, 0, 0), new(w, h, 0), new(0, h, 0),
                new(0, 0, d), new(w, 0, d), new(w, hs, d), new(0, hs, d)
            ],
            Materials =
            [
                new("wall", "Wall", wallAbs, true),
                new("floor", "Floor", floorAbs, true),
                new("ceil", "Ceiling", ceilAbs, true)
            ],
            Faces =
            [
                new([0, 1, 2, 3], 0),
                new([5, 4, 7, 6], 0),
                new([4, 0, 3, 7], 0),
                new([1, 5, 6, 2], 0),
                new([4, 5, 1, 0], 1),
                new([3, 2, 6, 7], 2)
            ]
        };
        return geo;
    }

    public static RoomGeometry FromShape(RoomShape shape, float w, float h, float d, float wallAbs, float floorAbs, float ceilAbs) =>
        shape switch
        {
            RoomShape.LShaped => CreateLShaped(w, h, d, wallAbs, floorAbs, ceilAbs),
            RoomShape.Cathedral => CreateCathedral(w, h, d, wallAbs, floorAbs, ceilAbs),
            RoomShape.Studio => CreateStudio(w, h, d, wallAbs, floorAbs, ceilAbs),
            _ => CreateBox(w, h, d, wallAbs, floorAbs, ceilAbs)
        };
}
