using SpaceAudio.Models;
using System.Runtime.CompilerServices;

namespace SpaceAudio.Audio.Bvh;

internal sealed class FaceBvhTree
{
    private BvhNode? _root;
    private RoomGeometry? _geometry;

    public void Build(RoomGeometry geometry)
    {
        _geometry = geometry;
        var faces = geometry.Faces;
        var vertices = geometry.Vertices;

        if (faces.Length == 0 || vertices.Length == 0)
        {
            _root = null;
            return;
        }

        int[] indices = new int[faces.Length];
        for (int i = 0; i < indices.Length; i++) indices[i] = i;

        _root = BuildNode(indices, 0, indices.Length, geometry);
    }

    private static BvhNode BuildNode(int[] indices, int start, int end, RoomGeometry geometry)
    {
        var node = new BvhNode();
        var bounds = AabbBox.Empty;

        for (int i = start; i < end; i++)
            bounds = bounds.Merge(ComputeFaceAabb(indices[i], geometry));

        node.Bounds = bounds.Padded();

        int count = end - start;
        if (count == 1)
        {
            node.FaceIndex = indices[start];
            return node;
        }

        float dx = bounds.MaxX - bounds.MinX;
        float dy = bounds.MaxY - bounds.MinY;
        float dz = bounds.MaxZ - bounds.MinZ;

        int axis = dx >= dy && dx >= dz ? 0 : dy >= dz ? 1 : 2;

        Array.Sort(indices, start, count, Comparer<int>.Create((a, b) =>
        {
            float ca = GetFaceCentroid(a, geometry, axis);
            float cb = GetFaceCentroid(b, geometry, axis);
            return ca.CompareTo(cb);
        }));

        int mid = start + count / 2;
        node.Left = BuildNode(indices, start, mid, geometry);
        node.Right = BuildNode(indices, mid, end, geometry);

        return node;
    }

    private static float GetFaceCentroid(int faceIndex, RoomGeometry geometry, int axis)
    {
        var face = geometry.Faces[faceIndex];
        float sum = 0.0f;
        int count = 0;
        foreach (int vi in face.VertexIndices)
        {
            if (vi < 0 || vi >= geometry.Vertices.Length) continue;
            ref var v = ref geometry.Vertices[vi];
            sum += axis == 0 ? v.X : axis == 1 ? v.Y : v.Z;
            count++;
        }
        return count > 0 ? sum / count : 0.0f;
    }

    private static AabbBox ComputeFaceAabb(int faceIndex, RoomGeometry geometry)
    {
        var face = geometry.Faces[faceIndex];
        var box = AabbBox.Empty;
        foreach (int vi in face.VertexIndices)
        {
            if (vi < 0 || vi >= geometry.Vertices.Length) continue;
            box = box.Expand(geometry.Vertices[vi]);
        }
        return box;
    }

    public int RayCast(in GeometryVertex origin, in GeometryVertex target, int excludeFaceIndex = -1)
    {
        if (_root is null || _geometry is null) return -1;

        float dx = target.X - origin.X;
        float dy = target.Y - origin.Y;
        float dz = target.Z - origin.Z;
        float length = MathF.Sqrt(dx * dx + dy * dy + dz * dz);
        if (length < 1e-6f) return -1;

        float invDx = dx != 0 ? 1.0f / dx : float.MaxValue;
        float invDy = dy != 0 ? 1.0f / dy : float.MaxValue;
        float invDz = dz != 0 ? 1.0f / dz : float.MaxValue;

        float closestT = length;
        int hitFace = -1;

        Traverse(_root, origin, dx / length, dy / length, dz / length,
            invDx / length, invDy / length, invDz / length,
            length, excludeFaceIndex, ref closestT, ref hitFace);

        return hitFace;
    }

    public bool IsOccluded(in GeometryVertex origin, in GeometryVertex target, int excludeFaceIndex = -1)
        => RayCast(in origin, in target, excludeFaceIndex) >= 0;

    private void Traverse(BvhNode node,
        in GeometryVertex origin, float ndx, float ndy, float ndz,
        float invNdx, float invNdy, float invNdz,
        float maxT, int excludeFaceIndex, ref float closestT, ref int hitFace)
    {
        if (!node.Bounds.IntersectsRay(origin.X, origin.Y, origin.Z, invNdx, invNdy, invNdz, closestT))
            return;

        if (node.IsLeaf)
        {
            if (node.FaceIndex == excludeFaceIndex) return;
            float t = IntersectFace(node.FaceIndex, in origin, ndx, ndy, ndz);
            if (t > 1e-4f && t < closestT)
            {
                closestT = t;
                hitFace = node.FaceIndex;
            }
            return;
        }

        Traverse(node.Left!, in origin, ndx, ndy, ndz, invNdx, invNdy, invNdz, maxT, excludeFaceIndex, ref closestT, ref hitFace);
        Traverse(node.Right!, in origin, ndx, ndy, ndz, invNdx, invNdy, invNdz, maxT, excludeFaceIndex, ref closestT, ref hitFace);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private float IntersectFace(int faceIndex, in GeometryVertex origin, float ndx, float ndy, float ndz)
    {
        var geometry = _geometry!;
        var face = geometry.Faces[faceIndex];
        if (face.VertexIndices.Length < 3) return -1;

        int i0 = face.VertexIndices[0];
        if (i0 < 0 || i0 >= geometry.Vertices.Length) return -1;

        ref var v0 = ref geometry.Vertices[i0];
        float ex, ey, ez;

        {
            int i1 = face.VertexIndices[1];
            if (i1 < 0 || i1 >= geometry.Vertices.Length) return -1;
            ref var v1 = ref geometry.Vertices[i1];
            int i2 = face.VertexIndices[2];
            if (i2 < 0 || i2 >= geometry.Vertices.Length) return -1;
            ref var v2 = ref geometry.Vertices[i2];

            float e1x = v1.X - v0.X, e1y = v1.Y - v0.Y, e1z = v1.Z - v0.Z;
            float e2x = v2.X - v0.X, e2y = v2.Y - v0.Y, e2z = v2.Z - v0.Z;
            ex = e1y * e2z - e1z * e2y;
            ey = e1z * e2x - e1x * e2z;
            ez = e1x * e2y - e1y * e2x;
        }

        float denom = ndx * ex + ndy * ey + ndz * ez;
        if (MathF.Abs(denom) < 1e-8f) return -1;

        float px = v0.X - origin.X, py = v0.Y - origin.Y, pz = v0.Z - origin.Z;
        float t = (px * ex + py * ey + pz * ez) / denom;
        if (t <= 1e-4f) return -1;

        float hx = origin.X + ndx * t;
        float hy = origin.Y + ndy * t;
        float hz = origin.Z + ndz * t;

        return IsPointInFacePolygon(faceIndex, hx, hy, hz) ? t : -1;
    }

    private bool IsPointInFacePolygon(int faceIndex, float hx, float hy, float hz)
    {
        var geometry = _geometry!;
        var face = geometry.Faces[faceIndex];
        var indices = face.VertexIndices;
        int n = indices.Length;
        if (n < 3) return false;

        float absNx = 0, absNy = 0, absNz = 0;
        {
            int i0 = indices[0], i1 = indices[1], i2 = indices[2];
            if (i0 < 0 || i0 >= geometry.Vertices.Length) return false;
            if (i1 < 0 || i1 >= geometry.Vertices.Length) return false;
            if (i2 < 0 || i2 >= geometry.Vertices.Length) return false;
            ref var v0 = ref geometry.Vertices[i0];
            ref var v1 = ref geometry.Vertices[i1];
            ref var v2 = ref geometry.Vertices[i2];
            float e1x = v1.X - v0.X, e1y = v1.Y - v0.Y, e1z = v1.Z - v0.Z;
            float e2x = v2.X - v0.X, e2y = v2.Y - v0.Y, e2z = v2.Z - v0.Z;
            absNx = MathF.Abs(e1y * e2z - e1z * e2y);
            absNy = MathF.Abs(e1z * e2x - e1x * e2z);
            absNz = MathF.Abs(e1x * e2y - e1y * e2x);
        }

        bool inside = false;
        int j = n - 1;

        if (absNz >= absNx && absNz >= absNy)
        {
            for (int i = 0; i < n; i++)
            {
                int idx_i = indices[i], idx_j = indices[j];
                if (idx_i < 0 || idx_i >= geometry.Vertices.Length) { j = i; continue; }
                if (idx_j < 0 || idx_j >= geometry.Vertices.Length) { j = i; continue; }
                ref var vi = ref geometry.Vertices[idx_i];
                ref var vj = ref geometry.Vertices[idx_j];
                if (((vi.Y > hy) != (vj.Y > hy)) &&
                    (hx < (vj.X - vi.X) * (hy - vi.Y) / (vj.Y - vi.Y) + vi.X))
                    inside = !inside;
                j = i;
            }
        }
        else if (absNx >= absNy)
        {
            for (int i = 0; i < n; i++)
            {
                int idx_i = indices[i], idx_j = indices[j];
                if (idx_i < 0 || idx_i >= geometry.Vertices.Length) { j = i; continue; }
                if (idx_j < 0 || idx_j >= geometry.Vertices.Length) { j = i; continue; }
                ref var vi = ref geometry.Vertices[idx_i];
                ref var vj = ref geometry.Vertices[idx_j];
                if (((vi.Y > hy) != (vj.Y > hy)) &&
                    (hz < (vj.Z - vi.Z) * (hy - vi.Y) / (vj.Y - vi.Y) + vi.Z))
                    inside = !inside;
                j = i;
            }
        }
        else
        {
            for (int i = 0; i < n; i++)
            {
                int idx_i = indices[i], idx_j = indices[j];
                if (idx_i < 0 || idx_i >= geometry.Vertices.Length) { j = i; continue; }
                if (idx_j < 0 || idx_j >= geometry.Vertices.Length) { j = i; continue; }
                ref var vi = ref geometry.Vertices[idx_i];
                ref var vj = ref geometry.Vertices[idx_j];
                if (((vi.Z > hz) != (vj.Z > hz)) &&
                    (hx < (vj.X - vi.X) * (hz - vi.Z) / (vj.Z - vi.Z) + vi.X))
                    inside = !inside;
                j = i;
            }
        }

        return inside;
    }
}
