using SpaceAudio.Enums;
using SpaceAudio.Localization;
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
    private volatile bool _planesDirty = true;
    private readonly Lock _planesLock = new();

    public void Invalidate() => _planesDirty = true;

    public FacePlane[] GetPlanes()
    {
        if (!_planesDirty && _cachedPlanes is not null) return _cachedPlanes;
        lock (_planesLock)
        {
            if (!_planesDirty && _cachedPlanes is not null) return _cachedPlanes;
            _cachedPlanes = BuildPlanes();
            _planesDirty = false;
            return _cachedPlanes;
        }
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

            float abs;
            float spectralDamping;

            if (face.MaterialIndex >= 0 && face.MaterialIndex < Materials.Length)
            {
                var mat = Materials[face.MaterialIndex];
                abs = mat.Absorption;
                spectralDamping = mat.SpectralDamping;
            }
            else
            {
                abs = 0.1f;
                spectralDamping = 0.3f;
            }

            planes[i] = FacePlane.FromVertices(in Vertices[i0], in Vertices[i1], in Vertices[i2], abs, spectralDamping);
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
            ref var v0 = ref Vertices[face.VertexIndices[0]];
            for (int i = 1; i < face.VertexIndices.Length - 1; i++)
            {
                ref var v1 = ref Vertices[face.VertexIndices[i]];
                ref var v2 = ref Vertices[face.VertexIndices[i + 1]];
                volume += GeometryVertex.Dot(in v0, GeometryVertex.Cross(in v1, in v2));
            }
        }
        return MathF.Abs(volume) / 6.0f;
    }

    public RoomGeometry CloneAndScale(float targetWidth, float targetHeight, float targetDepth)
    {
        var (_, maxX, _, maxY, _, maxZ) = GetBounds();
        float curW = Vertices.Length > 0 ? maxX : 1f;
        float curH = Vertices.Length > 0 ? maxY : 1f;
        float curD = Vertices.Length > 0 ? maxZ : 1f;
        float sx = curW > 0.001f ? targetWidth / curW : 1f;
        float sy = curH > 0.001f ? targetHeight / curH : 1f;
        float sz = curD > 0.001f ? targetDepth / curD : 1f;
        var verts = new GeometryVertex[Vertices.Length];
        for (int i = 0; i < Vertices.Length; i++)
        {
            ref var v = ref Vertices[i];
            verts[i] = new GeometryVertex(v.X * sx, v.Y * sy, v.Z * sz);
        }
        return new RoomGeometry
        {
            Name = Name,
            ShapeId = ShapeId,
            Vertices = verts,
            Faces = [.. Faces.Select(f => f.DeepClone())],
            Materials = [.. Materials.Select(m => m.Clone())]
        };
    }

    public float CalculateSurfaceArea()
    {
        float area = 0;
        foreach (var face in Faces)
        {
            if (face.VertexIndices.Length < 3) continue;
            ref var v0 = ref Vertices[face.VertexIndices[0]];
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
        Vertices = [.. Vertices],
        Faces = [.. Faces.Select(f => f.DeepClone())],
        Materials = [.. Materials.Select(m => m.Clone())]
    };

    public (float MinX, float MaxX, float MinY, float MaxY, float MinZ, float MaxZ) GetBounds()
    {
        if (Vertices.Length == 0) return (0, 0, 0, 0, 0, 0);
        float minX = float.MaxValue, maxX = float.MinValue;
        float minY = float.MaxValue, maxY = float.MinValue;
        float minZ = float.MaxValue, maxZ = float.MinValue;
        foreach (var v in Vertices)
        {
            if (v.X < minX) minX = v.X; if (v.X > maxX) maxX = v.X;
            if (v.Y < minY) minY = v.Y; if (v.Y > maxY) maxY = v.Y;
            if (v.Z < minZ) minZ = v.Z; if (v.Z > maxZ) maxZ = v.Z;
        }
        return (minX, maxX, minY, maxY, minZ, maxZ);
    }

    private bool IsPointInFaceXZProjection(RoomFace face, float x, float z)
    {
        var indices = face.VertexIndices.AsSpan();
        int n = indices.Length;
        if (n < 3) return false;

        Span<int> validIndices = stackalloc int[n];
        int count = 0;
        foreach (int i in indices)
            if (i >= 0 && i < Vertices.Length) validIndices[count++] = i;

        if (count < 3) return false;

        if (count == 4)
        {
            ref var v0 = ref Vertices[validIndices[0]];
            for (int i = 1; i < count - 1; i++)
            {
                ref var v1 = ref Vertices[validIndices[i]];
                ref var v2 = ref Vertices[validIndices[i + 1]];
                float x1 = v0.X, z1 = v0.Z;
                float x2 = v1.X, z2 = v1.Z;
                float x3 = v2.X, z3 = v2.Z;
                float denom = (z2 - z3) * (x1 - x3) + (x3 - x2) * (z1 - z3);
                if (MathF.Abs(denom) >= 1e-6f)
                {
                    float a = ((z2 - z3) * (x - x3) + (x3 - x2) * (z - z3)) / denom;
                    float b = ((z3 - z1) * (x - x3) + (x1 - x3) * (z - z3)) / denom;
                    float c = 1 - a - b;
                    if (a >= -0.001f && b >= -0.001f && c >= -0.001f) return true;
                }
            }
            return false;
        }

        bool inside = false;
        int j = count - 1;
        for (int i = 0; i < count; i++)
        {
            ref var vi = ref Vertices[validIndices[i]];
            ref var vj = ref Vertices[validIndices[j]];
            float xi = vi.X, zi = vi.Z;
            float xj = vj.X, zj = vj.Z;
            if (((zi > z) != (zj > z)) && (x < (xj - xi) * (z - zi) / (zj - zi) + xi))
                inside = !inside;
            j = i;
        }
        return inside;
    }

    private static (float Y, float NormalY)? GetFaceYAtXZ(RoomFace face, GeometryVertex[] vertices, float x, float z)
    {
        if (face.VertexIndices.Length < 3) return null;
        int n = face.VertexIndices.Length;
        int i0 = face.VertexIndices[0];
        if (i0 >= vertices.Length) return null;
        ref var v0 = ref vertices[i0];
        float bestScore = float.MaxValue;
        float bestY = 0;
        float bestNY = 0;
        bool found = false;
        for (int i = 1; i < n - 1; i++)
        {
            int i1 = face.VertexIndices[i];
            int i2 = face.VertexIndices[i + 1];
            if (i1 >= vertices.Length || i2 >= vertices.Length) continue;
            ref var v1 = ref vertices[i1];
            ref var v2 = ref vertices[i2];
            float x1 = v0.X, z1 = v0.Z;
            float x2 = v1.X, z2 = v1.Z;
            float x3 = v2.X, z3 = v2.Z;
            float denom = (z2 - z3) * (x1 - x3) + (x3 - x2) * (z1 - z3);
            if (MathF.Abs(denom) < 1e-6f) continue;
            float a = ((z2 - z3) * (x - x3) + (x3 - x2) * (z - z3)) / denom;
            float b = ((z3 - z1) * (x - x3) + (x1 - x3) * (z - z3)) / denom;
            float c = 1 - a - b;
            float score = 0;
            if (a < 0) score -= a;
            if (b < 0) score -= b;
            if (c < 0) score -= c;
            if (!found || score < bestScore)
            {
                var e1 = v1.Subtract(in v0);
                var e2 = v2.Subtract(in v0);
                var normal = GeometryVertex.Cross(in e1, in e2);
                if (MathF.Abs(normal.Y) >= 1e-6f)
                {
                    float d = -(normal.X * v0.X + normal.Y * v0.Y + normal.Z * v0.Z);
                    bestY = -(normal.X * x + normal.Z * z + d) / normal.Y;
                    bestNY = normal.Y;
                    bestScore = score;
                    found = true;
                }
            }
        }
        if (found) return (bestY, bestNY);
        return null;
    }

    public (float MinY, float MaxY) GetYBoundsAtXZ(float x, float z, float fallbackMin, float fallbackMax)
    {
        if (Faces.Length == 0 || Vertices.Length == 0) return (fallbackMin, fallbackMax);
        float minY = fallbackMin;
        float maxY = fallbackMax;
        bool hasMin = false;
        bool hasMax = false;
        float preciseMinY = float.MinValue;
        float preciseMaxY = float.MaxValue;
        foreach (var face in Faces)
        {
            if (!IsPointInFaceXZProjection(face, x, z)) continue;
            var fyNormal = GetFaceYAtXZ(face, Vertices, x, z);
            if (fyNormal is null) continue;
            float yv = fyNormal.Value.Y;
            float ny = fyNormal.Value.NormalY;
            if (ny > 0)
            {
                if (!hasMin || yv > preciseMinY) preciseMinY = yv;
                hasMin = true;
            }
            else
            {
                if (!hasMax || yv < preciseMaxY) preciseMaxY = yv;
                hasMax = true;
            }
        }
        if (hasMin) minY = preciseMinY;
        if (hasMax) maxY = preciseMaxY;
        if (minY > maxY)
        {
            float t = minY;
            minY = maxY;
            maxY = t;
        }
        return (minY, maxY);
    }

    private RoomFace? FindFloorFace()
    {
        RoomFace? floorFace = null;
        float maxNormalY = -float.MaxValue;
        foreach (var face in Faces)
        {
            if (face.VertexIndices.Length >= 3)
            {
                int i0 = face.VertexIndices[0];
                int i1 = face.VertexIndices[1];
                int i2 = face.VertexIndices[2];
                if (i0 < Vertices.Length && i1 < Vertices.Length && i2 < Vertices.Length)
                {
                    ref var v0 = ref Vertices[i0];
                    ref var v1 = ref Vertices[i1];
                    ref var v2 = ref Vertices[i2];
                    var e1 = v1.Subtract(in v0);
                    var e2 = v2.Subtract(in v0);
                    var normal = GeometryVertex.Cross(in e1, in e2);
                    if (normal.Y > maxNormalY)
                    {
                        maxNormalY = normal.Y;
                        floorFace = face;
                    }
                }
            }
        }
        return floorFace;
    }

    public bool IsPointInsideXZ(float x, float z)
    {
        if (Faces.Length == 0 || Vertices.Length == 0) return true;
        foreach (var face in Faces)
        {
            if (IsPointInFaceXZProjection(face, x, z))
                return true;
        }
        return false;
    }

    public (float X, float Z) ClampToPolygonXZ(float x, float z)
    {
        if (Faces.Length == 0 || Vertices.Length == 0) return (x, z);
        if (IsPointInsideXZ(x, z)) return (x, z);

        float minDistSq = float.MaxValue;
        float bestX = x, bestZ = z;

        foreach (var face in Faces)
        {
            var indices = face.VertexIndices.AsSpan();
            int n = indices.Length;
            if (n < 3) continue;

            Span<int> validIndices = stackalloc int[n];
            int count = 0;
            foreach (int i in indices)
                if (i >= 0 && i < Vertices.Length) validIndices[count++] = i;

            if (count < 3) continue;

            for (int i = 0; i < count; i++)
            {
                int j = (i + 1) % count;
                ref var v1 = ref Vertices[validIndices[i]];
                ref var v2 = ref Vertices[validIndices[j]];
                float x1 = v1.X, z1 = v1.Z;
                float x2 = v2.X, z2 = v2.Z;
                float dx = x2 - x1, dz = z2 - z1;
                float lenSq = dx * dx + dz * dz;
                float px = x1, pz = z1;
                if (lenSq > 0.000001f)
                {
                    float t = ((x - x1) * dx + (z - z1) * dz) / lenSq;
                    if (t >= 1f) { px = x2; pz = z2; }
                    else if (t > 0f) { px = x1 + t * dx; pz = z1 + t * dz; }
                }
                float distSq = (x - px) * (x - px) + (z - pz) * (z - pz);
                if (distSq < minDistSq)
                {
                    minDistSq = distSq;
                    bestX = px;
                    bestZ = pz;
                }
            }
        }
        return (bestX, bestZ);
    }

    public (float X, float Z) RayCastXZ(float startX, float startZ, float endX, float endZ)
    {
        if (Faces.Length == 0 || Vertices.Length == 0) return (endX, endZ);

        float dx = endX - startX;
        float dz = endZ - startZ;
        if (Math.Abs(dx) < 0.0001f && Math.Abs(dz) < 0.0001f) return (endX, endZ);

        float closestU = 1.0f;
        bool hit = false;

        foreach (var face in Faces)
        {
            var indices = face.VertexIndices.AsSpan();
            int n = indices.Length;
            if (n < 3) continue;

            Span<int> validIndices = stackalloc int[n];
            int count = 0;
            foreach (int i in indices)
                if (i >= 0 && i < Vertices.Length) validIndices[count++] = i;

            if (count < 3) continue;

            for (int i = 0; i < count; i++)
            {
                int j = (i + 1) % count;
                ref var p1 = ref Vertices[validIndices[i]];
                ref var p2 = ref Vertices[validIndices[j]];
                float x1 = p1.X, z1 = p1.Z;
                float x2 = p2.X, z2 = p2.Z;

                float ex = x2 - x1;
                float ez = z2 - z1;
                float cx = x1 - startX;
                float cz = z1 - startZ;

                float V = -dx * ez + dz * ex;
                if (Math.Abs(V) < 0.000001f) continue;

                float uRay = (-cx * ez + cz * ex) / V;
                float tWall = (dx * cz - dz * cx) / V;

                if (uRay >= 0.0f && uRay <= closestU && tWall >= -0.0001f && tWall <= 1.0001f)
                {
                    float testDist = 0.01f;
                    float rLen = (float)Math.Sqrt(dx * dx + dz * dz);
                    if (rLen > 0.0001f)
                    {
                        float stepX = (dx / rLen) * testDist;
                        float stepZ = (dz / rLen) * testDist;
                        float testX = startX + dx * uRay + stepX;
                        float testZ = startZ + dz * uRay + stepZ;

                        bool isValid = IsPointInsideXZ(testX, testZ);
                        if (!isValid)
                        {
                            foreach (var chkFace in Faces)
                            {
                                var cIndices = chkFace.VertexIndices.AsSpan();
                                int cn = cIndices.Length;
                                if (cn < 3) continue;
                                Span<int> cValid = stackalloc int[cn];
                                int cCount = 0;
                                foreach (int ci in cIndices)
                                    if (ci >= 0 && ci < Vertices.Length) cValid[cCount++] = ci;
                                if (cCount < 3) continue;
                                for (int k = 0; k < cCount; k++)
                                {
                                    int l = (k + 1) % cCount;
                                    ref var b1 = ref Vertices[cValid[k]];
                                    ref var b2 = ref Vertices[cValid[l]];
                                    float bx1 = b1.X, bz1 = b1.Z;
                                    float bx2 = b2.X, bz2 = b2.Z;
                                    float bdx = bx2 - bx1;
                                    float bdz = bz2 - bz1;
                                    float lenSq = bdx * bdx + bdz * bdz;
                                    if (lenSq < 0.0001f) continue;
                                    float t = ((testX - bx1) * bdx + (testZ - bz1) * bdz) / lenSq;
                                    t = Math.Max(0, Math.Min(1, t));
                                    float px = bx1 + t * bdx;
                                    float pz = bz1 + t * bdz;
                                    float distSq = (testX - px) * (testX - px) + (testZ - pz) * (testZ - pz);
                                    if (distSq < 0.000001f)
                                    {
                                        isValid = true;
                                        break;
                                    }
                                }
                                if (isValid) break;
                            }
                        }
                        if (isValid) continue;
                    }

                    closestU = uRay;
                    hit = true;
                }
            }
        }

        if (hit)
        {
            float hitX = startX + dx * closestU;
            float hitZ = startZ + dz * closestU;
            if (closestU > 0.001f)
            {
                float pushDist = 0.001f;
                return (startX + dx * (closestU - pushDist), startZ + dz * (closestU - pushDist));
            }
            return (hitX, hitZ);
        }

        if (!IsPointInsideXZ(endX, endZ))
            return ClampToPolygonXZ(endX, endZ);

        return (endX, endZ);
    }

    public static RoomGeometry CreateBox(float w, float h, float d, float wallAbs, float floorAbs, float ceilAbs)
    {
        return new RoomGeometry
        {
            Name = Texts.PresetBox,
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
    }

    public static RoomGeometry CreateLShaped(float w, float h, float d, float wallAbs, float floorAbs, float ceilAbs)
    {
        float hw = w * 0.5f, hd = d * 0.5f;
        return new RoomGeometry
        {
            Name = Texts.PresetLShape,
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
    }

    public static RoomGeometry CreateCathedral(float w, float h, float d, float wallAbs, float floorAbs, float ceilAbs)
    {
        float hs = h * 0.6f;
        float hw = w * 0.5f;
        return new RoomGeometry
        {
            Name = Texts.PresetCathedral,
            ShapeId = "cathedral",
            Vertices =
            [
                new(0, 0, 0), new(w, 0, 0), new(w, hs, 0), new(hw, h, 0), new(0, hs, 0),
                new(0, 0, d), new(w, 0, d), new(w, hs, d), new(hw, h, d), new(0, hs, d)
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
    }

    public static RoomGeometry CreateStudio(float w, float h, float d, float wallAbs, float floorAbs, float ceilAbs)
    {
        float hs = h * 0.8f;
        return new RoomGeometry
        {
            Name = Texts.PresetStudio,
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
