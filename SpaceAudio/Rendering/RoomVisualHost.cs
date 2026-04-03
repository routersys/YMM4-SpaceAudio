using SpaceAudio.Audio;
using SpaceAudio.Enums;
using SpaceAudio.Models;
using System.Collections.Concurrent;
using System.Globalization;
using System.Numerics;
using System.Windows;
using System.Windows.Media;

namespace SpaceAudio.Rendering;

internal sealed class RoomVisualHost : FrameworkElement
{
    private readonly VisualCollection _children;
    private readonly DrawingVisual _floorVisual = new();
    private readonly DrawingVisual _wallVisual = new();
    private readonly DrawingVisual _objectVisual = new();
    private readonly DrawingVisual _infoVisual = new();
    private readonly DrawingVisual _gridVisual = new();
    private readonly DrawingVisual _vertexVisual = new();

    private static readonly Typeface InfoTypeface = new("Segoe UI");
    private const double Dpi = 96.0;

    public RoomVisualHost()
    {
        _children = new VisualCollection(this)
        {
            _floorVisual,
            _wallVisual,
            _objectVisual,
            _infoVisual,
            _gridVisual,
            _vertexVisual
        };
    }

    protected override int VisualChildrenCount => _children.Count;
    protected override Visual GetVisualChild(int index) => _children[index];

    public void Render(Camera3D camera, ThemePalette palette, RoomSnapshot snapshot, double viewWidth, double viewHeight,
        bool isSourceSelected, bool isListenerSelected, bool showObjects = true,
        bool showGrid = false, float gridSize = 1.0f, bool showDimensions = false)
    {
        if (viewWidth <= 0 || viewHeight <= 0) return;

        var viewMat = ProjectionMatrix.CreateViewMatrix(camera);

        RenderFloor(viewMat, palette, snapshot, viewWidth, viewHeight);
        RenderWalls(camera, viewMat, palette, snapshot, viewWidth, viewHeight);

        if (showObjects)
            RenderObjects(viewMat, palette, snapshot, viewWidth, viewHeight, isSourceSelected, isListenerSelected);
        else
            ClearVisual(_objectVisual);

        RenderInfo(palette, snapshot, viewWidth, viewHeight);

        if (showGrid || showDimensions)
            RenderGridOverlay(camera, viewMat, palette, snapshot, viewWidth, viewHeight, showGrid, gridSize, showDimensions);
        else
            ClearVisual(_gridVisual);
    }

    public void RenderVertexOverlay(Camera3D camera, GeometryVertex[] vertices,
        int[] faceHighlightedIndices, int selectedIndex, IEnumerable<int> selectedIndices, double viewWidth, double viewHeight,
        RoomSnapshot? snap = null)
    {
        var selectedSet = new HashSet<int>(selectedIndices ?? Array.Empty<int>());
        using var dc = _vertexVisual.RenderOpen();
        if (vertices.Length == 0 || viewWidth <= 0 || viewHeight <= 0) return;

        var viewMat = ProjectionMatrix.CreateViewMatrix(camera);

        var normalBrush = MakeFrozenBrush(Color.FromArgb(210, 70, 130, 220));
        var highlightBrush = MakeFrozenBrush(Color.FromArgb(230, 255, 195, 30));
        var selectedBrush = MakeFrozenBrush(Color.FromRgb(240, 60, 60));

        var outlinePen = new Pen(MakeFrozenBrush(Color.FromArgb(180, 0, 0, 0)), 1.0);
        outlinePen.Freeze();
        var selectedOutlinePen = new Pen(Brushes.White, 1.5);
        selectedOutlinePen.Freeze();

        if (selectedIndex >= 0 && selectedIndex < vertices.Length && snap.HasValue)
        {
            var sv = vertices[selectedIndex];
            float rng = Math.Max(Math.Max(snap.Value.Width, snap.Value.Height), snap.Value.Depth) * 1.5f;
            var xPen = FrozenPen(new SolidColorBrush(Color.FromArgb(160, 220, 60, 60)), 1.0);
            var yPen = FrozenPen(new SolidColorBrush(Color.FromArgb(160, 68, 204, 136)), 1.0);
            var zPen = FrozenPen(new SolidColorBrush(Color.FromArgb(160, 68, 136, 255)), 1.0);
            DrawClippedLine(dc, xPen, new Vector3(-rng, sv.Y, sv.Z), new Vector3(rng, sv.Y, sv.Z), viewMat, viewWidth, viewHeight);
            DrawClippedLine(dc, yPen, new Vector3(sv.X, -rng, sv.Z), new Vector3(sv.X, rng, sv.Z), viewMat, viewWidth, viewHeight);
            DrawClippedLine(dc, zPen, new Vector3(sv.X, sv.Y, -rng), new Vector3(sv.X, sv.Y, rng), viewMat, viewWidth, viewHeight);
        }

        for (int i = 0; i < vertices.Length; i++)
        {
            ref readonly var v = ref vertices[i];
            var cam = Vector3.Transform(new Vector3(v.X, v.Y, v.Z), viewMat);
            if (cam.Z > -ProjectionMatrix.NearPlane) continue;

            var screen = ProjectionMatrix.ProjectToScreen(cam, viewWidth, viewHeight);

            bool isSelected = selectedSet.Contains(i) || i == selectedIndex;
            bool isHighlighted = ArrayContains(faceHighlightedIndices, i);

            var brush = isSelected ? selectedBrush : (isHighlighted ? highlightBrush : normalBrush);
            var pen = isSelected ? selectedOutlinePen : outlinePen;
            double radius = isSelected ? 7.0 : (isHighlighted ? 6.0 : 4.5);

            dc.DrawEllipse(brush, pen, screen, radius, radius);

            if (isSelected || isHighlighted)
            {
                var labelBrush = isSelected ? Brushes.White : MakeFrozenBrush(Color.FromArgb(220, 40, 40, 40));
                dc.DrawText(
                    MakeText(i.ToString(), 8.5, labelBrush),
                    new Point(screen.X + radius + 3, screen.Y - 5));
            }
        }
    }

    public void ClearVertexOverlay()
    {
        ClearVisual(_vertexVisual);
    }

    private static void ClearVisual(DrawingVisual visual)
    {
        using var dc = visual.RenderOpen();
    }

    private static bool ArrayContains(int[] arr, int value)
    {
        foreach (var x in arr)
            if (x == value) return true;
        return false;
    }

    private void RenderFloor(Matrix4x4 viewMat, ThemePalette palette, in RoomSnapshot snap, double w, double h)
    {
        using var dc = _floorVisual.RenderOpen();
        if (snap.Width <= 0 || snap.Depth <= 0) return;

        var gridPen = GetMaterialPen(snap.FloorMaterialId, 100, 0.8);

        if (snap.Shape == SpaceAudio.Enums.RoomShape.Custom && snap.Geometry is { } cg && cg.Vertices.Length > 0)
        {
            float minX = cg.Vertices.Min(v => v.X), maxX2 = cg.Vertices.Max(v => v.X);
            float minZ = cg.Vertices.Min(v => v.Z), maxZ2 = cg.Vertices.Max(v => v.Z);
            float stepX = CalculateGridStep(maxX2 - minX);
            float stepZ = CalculateGridStep(maxZ2 - minZ);
            for (float t = minX; t <= maxX2 + 0.001f; t += stepX)
            {
                float x = Math.Min(t, maxX2);
                DrawClippedLine(dc, gridPen, new Vector3(x, 0, minZ), new Vector3(x, 0, maxZ2), viewMat, w, h);
            }
            for (float t = minZ; t <= maxZ2 + 0.001f; t += stepZ)
            {
                float z = Math.Min(t, maxZ2);
                DrawClippedLine(dc, gridPen, new Vector3(minX, 0, z), new Vector3(maxX2, 0, z), viewMat, w, h);
            }
            return;
        }

        float maxW = snap.Width;
        float maxD = snap.Depth;
        float stepW = CalculateGridStep(maxW);
        float stepD = CalculateGridStep(maxD);
        bool isL = snap.Shape == SpaceAudio.Enums.RoomShape.LShaped;
        float hw = maxW * 0.5f;
        float hd = maxD * 0.5f;

        for (float t = 0; t <= maxW + 0.001f; t += stepW)
        {
            float x = Math.Min(t, maxW);
            float zEnd = isL && x > hw ? hd : maxD;
            DrawClippedLine(dc, gridPen, new Vector3(x, 0, 0), new Vector3(x, 0, zEnd), viewMat, w, h);
        }

        for (float t = 0; t <= maxD + 0.001f; t += stepD)
        {
            float z = Math.Min(t, maxD);
            float xEnd = isL && z > hd ? hw : maxW;
            DrawClippedLine(dc, gridPen, new Vector3(0, 0, z), new Vector3(xEnd, 0, z), viewMat, w, h);
        }
    }


    private void RenderWalls(Camera3D camera, Matrix4x4 viewMat, ThemePalette palette, in RoomSnapshot snap, double w, double h)
    {
        using var dc = _wallVisual.RenderOpen();
        if (snap.Width <= 0 || snap.Height <= 0 || snap.Depth <= 0) return;

        var (ex, ey, ez) = camera.GetEyePosition();
        Vector3 eye = new(ex, ey, ez);

        float ww = snap.Width, hh = snap.Height, dd = snap.Depth;
        float hw = ww * 0.5f, hd = dd * 0.5f;

        var faces = new FaceData[12];
        int fCount = 0;

        var lines = new (Vector3, Vector3)[24];
        int lCount = 0;

        var ceilMat = snap.CeilingMaterialId;
        var floorMat = snap.FloorMaterialId;
        var wallMat = snap.WallMaterialId;

        void AddFace(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, Vector3 normal)
        {
            Vector3 center = (p0 + p1 + p2 + p3) * 0.25f;
            if (Vector3.Dot(center - eye, normal) > 0)
            {
                var mat = normal.Y > 0 ? ceilMat : (normal.Y < 0 ? floorMat : wallMat);
                faces[fCount++] = new FaceData(p0, p1, p2, p3, eye, mat);
            }
        }

        void AddLine(Vector3 p0, Vector3 p1)
        {
            lines[lCount++] = (p0, p1);
        }

        if (snap.Shape == SpaceAudio.Enums.RoomShape.LShaped)
        {
            AddFace(new(0, 0, 0), new(ww, 0, 0), new(ww, hh, 0), new(0, hh, 0), new(0, 0, -1));
            AddFace(new(hw, 0, hd), new(hw, 0, dd), new(hw, hh, dd), new(hw, hh, hd), new(1, 0, 0));
            AddFace(new(hw, 0, hd), new(ww, 0, hd), new(ww, hh, hd), new(hw, hh, hd), new(0, 0, 1));
            AddFace(new(hw, 0, dd), new(0, 0, dd), new(0, hh, dd), new(hw, hh, dd), new(0, 0, 1));
            AddFace(new(0, 0, dd), new(0, 0, 0), new(0, hh, 0), new(0, hh, dd), new(-1, 0, 0));
            AddFace(new(ww, 0, 0), new(ww, 0, hd), new(ww, hh, hd), new(ww, hh, 0), new(1, 0, 0));
            AddFace(new(0, hh, 0), new(ww, hh, 0), new(ww, hh, hd), new(0, hh, hd), new(0, 1, 0));
            AddFace(new(0, hh, hd), new(hw, hh, hd), new(hw, hh, dd), new(0, hh, dd), new(0, 1, 0));

            AddLine(new(0, 0, 0), new(ww, 0, 0));
            AddLine(new(ww, 0, 0), new(ww, 0, hd));
            AddLine(new(ww, 0, hd), new(hw, 0, hd));
            AddLine(new(hw, 0, hd), new(hw, 0, dd));
            AddLine(new(hw, 0, dd), new(0, 0, dd));
            AddLine(new(0, 0, dd), new(0, 0, 0));
            AddLine(new(0, hh, 0), new(ww, hh, 0));
            AddLine(new(ww, hh, 0), new(ww, hh, hd));
            AddLine(new(ww, hh, hd), new(hw, hh, hd));
            AddLine(new(hw, hh, hd), new(hw, hh, dd));
            AddLine(new(hw, hh, dd), new(0, hh, dd));
            AddLine(new(0, hh, dd), new(0, hh, 0));
            AddLine(new(0, 0, 0), new(0, hh, 0));
            AddLine(new(ww, 0, 0), new(ww, hh, 0));
            AddLine(new(ww, 0, hd), new(ww, hh, hd));
            AddLine(new(hw, 0, hd), new(hw, hh, hd));
            AddLine(new(hw, 0, dd), new(hw, hh, dd));
            AddLine(new(0, 0, dd), new(0, hh, dd));
        }
        else if (snap.Shape == SpaceAudio.Enums.RoomShape.Cathedral)
        {
            float hc = hh * 1.5f;
            AddFace(new(0, 0, 0), new(hw, 0, 0), new(hw, hc, 0), new(0, hh, 0), new(0, 0, -1));
            AddFace(new(hw, 0, 0), new(ww, 0, 0), new(ww, hh, 0), new(hw, hc, 0), new(0, 0, -1));
            AddFace(new(ww, 0, dd), new(hw, 0, dd), new(hw, hc, dd), new(ww, hh, dd), new(0, 0, 1));
            AddFace(new(hw, 0, dd), new(0, 0, dd), new(0, hh, dd), new(hw, hc, dd), new(0, 0, 1));
            AddFace(new(0, 0, dd), new(0, 0, 0), new(0, hh, 0), new(0, hh, dd), new(-1, 0, 0));
            AddFace(new(ww, 0, 0), new(ww, 0, dd), new(ww, hh, dd), new(ww, hh, 0), new(1, 0, 0));
            AddFace(new(0, hh, 0), new(hw, hc, 0), new(hw, hc, dd), new(0, hh, dd), new(-0.5f, 1, 0));
            AddFace(new(hw, hc, 0), new(ww, hh, 0), new(ww, hh, dd), new(hw, hc, dd), new(0.5f, 1, 0));

            AddLine(new(0, 0, 0), new(ww, 0, 0));
            AddLine(new(0, 0, dd), new(ww, 0, dd));
            AddLine(new(0, 0, 0), new(0, 0, dd));
            AddLine(new(ww, 0, 0), new(ww, 0, dd));
            AddLine(new(0, hh, 0), new(hw, hc, 0));
            AddLine(new(hw, hc, 0), new(ww, hh, 0));
            AddLine(new(0, hh, dd), new(hw, hc, dd));
            AddLine(new(hw, hc, dd), new(ww, hh, dd));
            AddLine(new(0, 0, 0), new(0, hh, 0));
            AddLine(new(ww, 0, 0), new(ww, hh, 0));
            AddLine(new(hw, hc, 0), new(hw, hc, dd));
            AddLine(new(0, 0, dd), new(0, hh, dd));
            AddLine(new(ww, 0, dd), new(ww, hh, dd));
            AddLine(new(0, hh, 0), new(0, hh, dd));
            AddLine(new(ww, hh, 0), new(ww, hh, dd));
        }
        else if (snap.Shape == SpaceAudio.Enums.RoomShape.Studio)
        {
            float hs = hh * 0.8f;
            AddFace(new(0, 0, 0), new(ww, 0, 0), new(ww, hh, 0), new(0, hh, 0), new(0, 0, -1));
            AddFace(new(ww, 0, dd), new(0, 0, dd), new(0, hs, dd), new(ww, hs, dd), new(0, 0, 1));
            AddFace(new(0, 0, dd), new(0, 0, 0), new(0, hh, 0), new(0, hs, dd), new(-1, 0, 0));
            AddFace(new(ww, 0, 0), new(ww, 0, dd), new(ww, hs, dd), new(ww, hh, 0), new(1, 0, 0));
            AddFace(new(0, hh, 0), new(ww, hh, 0), new(ww, hs, dd), new(0, hs, dd), new(0, 1, 0));

            AddLine(new(0, 0, 0), new(ww, 0, 0));
            AddLine(new(ww, 0, 0), new(ww, 0, dd));
            AddLine(new(ww, 0, dd), new(0, 0, dd));
            AddLine(new(0, 0, dd), new(0, 0, 0));
            AddLine(new(0, hh, 0), new(ww, hh, 0));
            AddLine(new(ww, hh, 0), new(ww, hs, dd));
            AddLine(new(ww, hs, dd), new(0, hs, dd));
            AddLine(new(0, hs, dd), new(0, hh, 0));
            AddLine(new(0, 0, 0), new(0, hh, 0));
            AddLine(new(ww, 0, 0), new(ww, hh, 0));
            AddLine(new(0, 0, dd), new(0, hs, dd));
            AddLine(new(ww, 0, dd), new(ww, hs, dd));
        }
        else if (snap.Shape == SpaceAudio.Enums.RoomShape.Custom && snap.Geometry is { } customGeo
                 && customGeo.Vertices.Length >= 3 && customGeo.Faces.Length > 0)
        {
            RenderCustomGeometry(dc, viewMat, customGeo, snap, w, h);
            return;
        }
        else
        {
            AddFace(new(0, 0, 0), new(ww, 0, 0), new(ww, hh, 0), new(0, hh, 0), new(0, 0, -1));
            AddFace(new(ww, 0, dd), new(0, 0, dd), new(0, hh, dd), new(ww, hh, dd), new(0, 0, 1));
            AddFace(new(0, 0, dd), new(0, 0, 0), new(0, hh, 0), new(0, hh, dd), new(-1, 0, 0));
            AddFace(new(ww, 0, 0), new(ww, 0, dd), new(ww, hh, dd), new(ww, hh, 0), new(1, 0, 0));
            AddFace(new(0, hh, 0), new(ww, hh, 0), new(ww, hh, dd), new(0, hh, dd), new(0, 1, 0));

            AddLine(new(0, 0, 0), new(ww, 0, 0));
            AddLine(new(ww, 0, 0), new(ww, 0, dd));
            AddLine(new(ww, 0, dd), new(0, 0, dd));
            AddLine(new(0, 0, dd), new(0, 0, 0));
            AddLine(new(0, hh, 0), new(ww, hh, 0));
            AddLine(new(ww, hh, 0), new(ww, hh, dd));
            AddLine(new(ww, hh, dd), new(0, hh, dd));
            AddLine(new(0, hh, dd), new(0, hh, 0));
            AddLine(new(0, 0, 0), new(0, hh, 0));
            AddLine(new(ww, 0, 0), new(ww, hh, 0));
            AddLine(new(0, 0, dd), new(0, hh, dd));
            AddLine(new(ww, 0, dd), new(ww, hh, dd));
        }

        var sortedFaces = faces[..fCount];
        SortByDepthDescending(sortedFaces);

        Span<Vector3> camSpaceVertices = stackalloc Vector3[4];
        Span<Vector3> clippedVertices = stackalloc Vector3[8];

        for (int fi = 0; fi < sortedFaces.Length; fi++)
        {
            ref var face = ref sortedFaces[fi];
            var wallBrush = GetMaterialBrush(face.Material, 40);
            var edgePen = GetMaterialPen(face.Material, 120, 1.2);

            camSpaceVertices[0] = Vector3.Transform(face.P0, viewMat);
            camSpaceVertices[1] = Vector3.Transform(face.P1, viewMat);
            camSpaceVertices[2] = Vector3.Transform(face.P2, viewMat);
            camSpaceVertices[3] = Vector3.Transform(face.P3, viewMat);

            int nClipped = ProjectionMatrix.ClipPolygonZ(camSpaceVertices, clippedVertices);
            if (nClipped < 3) continue;

            var geo = new StreamGeometry();
            using (var ctx = geo.Open())
            {
                var pt0 = ProjectionMatrix.ProjectToScreen(clippedVertices[0], w, h);
                ctx.BeginFigure(pt0, true, true);
                for (int i = 1; i < nClipped; i++)
                    ctx.LineTo(ProjectionMatrix.ProjectToScreen(clippedVertices[i], w, h), true, false);
            }
            geo.Freeze();
            dc.DrawGeometry(wallBrush, edgePen, geo);
        }

        var cornerPen = GetMaterialPen(snap.WallMaterialId, 150, 1.0);
        for (int i = 0; i < lCount; i++)
            DrawClippedLine(dc, cornerPen, lines[i].Item1, lines[i].Item2, viewMat, w, h);
    }

    private void RenderObjects(Matrix4x4 viewMat, ThemePalette palette, in RoomSnapshot snap, double w, double h, bool isSourceSelected, bool isListenerSelected)
    {
        using var dc = _objectVisual.RenderOpen();

        var pathPen = new Pen(palette.ReflectionPath, 1.0) { DashStyle = DashStyles.Dash };
        pathPen.Freeze();

        var sWorld = new Vector3(snap.SourceX, snap.SourceY, snap.SourceZ);
        var lWorld = new Vector3(snap.ListenerX, snap.ListenerY, snap.ListenerZ);

        DrawClippedLine(dc, pathPen, sWorld, lWorld, viewMat, w, h);

        var sCam = Vector3.Transform(sWorld, viewMat);
        var lCam = Vector3.Transform(lWorld, viewMat);

        var sourceBrush = isSourceSelected ? BrightenBrush(palette.SourceMarker) : palette.SourceMarker;
        var listenerBrush = isListenerSelected ? BrightenBrush(palette.ListenerMarker) : palette.ListenerMarker;

        if (sCam.Z <= -ProjectionMatrix.NearPlane && lCam.Z <= -ProjectionMatrix.NearPlane)
        {
            var pSrc = ProjectionMatrix.ProjectToScreen(sCam, w, h);
            var pLis = ProjectionMatrix.ProjectToScreen(lCam, w, h);
            float sScale = Math.Clamp(8.0f / -sCam.Z * 5.0f, 4.0f, 12.0f);
            float lScale = Math.Clamp(8.0f / -lCam.Z * 5.0f, 4.0f, 12.0f);

            if (sCam.Z <= lCam.Z)
            {
                DrawMarker(dc, sourceBrush, "S", pSrc, sScale);
                DrawMarker(dc, listenerBrush, "L", pLis, lScale);
            }
            else
            {
                DrawMarker(dc, listenerBrush, "L", pLis, lScale);
                DrawMarker(dc, sourceBrush, "S", pSrc, sScale);
            }
        }
        else if (sCam.Z <= -ProjectionMatrix.NearPlane)
        {
            var pSrc = ProjectionMatrix.ProjectToScreen(sCam, w, h);
            float sScale = Math.Clamp(8.0f / -sCam.Z * 5.0f, 4.0f, 12.0f);
            DrawMarker(dc, sourceBrush, "S", pSrc, sScale);
        }
        else if (lCam.Z <= -ProjectionMatrix.NearPlane)
        {
            var pLis = ProjectionMatrix.ProjectToScreen(lCam, w, h);
            float lScale = Math.Clamp(8.0f / -lCam.Z * 5.0f, 4.0f, 12.0f);
            DrawMarker(dc, listenerBrush, "L", pLis, lScale);
        }

        RenderAxisIndicator(dc, viewMat, palette, w, h);
    }

    private static void DrawClippedLine(DrawingContext dc, Pen pen, Vector3 pw1, Vector3 pw2, Matrix4x4 viewMat, double w, double h)
    {
        var pc1 = Vector3.Transform(pw1, viewMat);
        var pc2 = Vector3.Transform(pw2, viewMat);

        if (ProjectionMatrix.ClipLineZ(ref pc1, ref pc2))
        {
            var p1 = ProjectionMatrix.ProjectToScreen(pc1, w, h);
            var p2 = ProjectionMatrix.ProjectToScreen(pc2, w, h);
            dc.DrawLine(pen, p1, p2);
        }
    }

    private static void DrawMarker(DrawingContext dc, SolidColorBrush fill, string label, Point p, float radius)
    {
        var outlinePen = new Pen(Brushes.Black, 1.2);
        outlinePen.Freeze();
        dc.DrawEllipse(fill, outlinePen, p, radius, radius);
        dc.DrawText(MakeText(label, 10, fill), new Point(p.X + radius + 2, p.Y - 7));
    }

    private static SolidColorBrush BrightenBrush(SolidColorBrush b)
    {
        var c = b.Color;
        var brush = new SolidColorBrush(Color.FromRgb(
            (byte)Math.Min(255, c.R + 80),
            (byte)Math.Min(255, c.G + 80),
            (byte)Math.Min(255, c.B + 80)));
        brush.Freeze();
        return brush;
    }

    private static void RenderAxisIndicator(DrawingContext dc, Matrix4x4 viewMat, ThemePalette palette, double w, double h)
    {
        double ax = 32, ay = h - 32;
        float len = 18.0f;

        var o = Vector3.Transform(Vector3.Zero, viewMat);
        var xE = Vector3.Transform(new Vector3(1, 0, 0), viewMat);
        var yE = Vector3.Transform(new Vector3(0, 1, 0), viewMat);
        var zE = Vector3.Transform(new Vector3(0, 0, 1), viewMat);

        if (o.Z > -ProjectionMatrix.NearPlane) return;

        var po = ProjectionMatrix.ProjectToScreen(o, w, h);
        var px = ProjectionMatrix.ProjectToScreen(xE, w, h);
        var py = ProjectionMatrix.ProjectToScreen(yE, w, h);
        var pz = ProjectionMatrix.ProjectToScreen(zE, w, h);

        double dxX = px.X - po.X, dyX = px.Y - po.Y;
        double dxY = py.X - po.X, dyY = py.Y - po.Y;
        double dxZ = pz.X - po.X, dyZ = pz.Y - po.Y;

        NormalizeDir(ref dxX, ref dyX, len);
        NormalizeDir(ref dxY, ref dyY, len);
        NormalizeDir(ref dxZ, ref dyZ, len);

        var xPen = FrozenPen(new SolidColorBrush(Color.FromRgb(220, 60, 60)), 1.5);
        var yPen = FrozenPen(new SolidColorBrush(Color.FromRgb(60, 180, 60)), 1.5);
        var zPen = FrozenPen(new SolidColorBrush(Color.FromRgb(60, 100, 220)), 1.5);

        dc.DrawLine(xPen, new Point(ax, ay), new Point(ax + dxX, ay + dyX));
        dc.DrawLine(yPen, new Point(ax, ay), new Point(ax + dxY, ay + dyY));
        dc.DrawLine(zPen, new Point(ax, ay), new Point(ax + dxZ, ay + dyZ));

        dc.DrawText(MakeText("X", 8, xPen.Brush), new Point(ax + dxX + 2, ay + dyX - 5));
        dc.DrawText(MakeText("Y", 8, yPen.Brush), new Point(ax + dxY + 2, ay + dyY - 5));
        dc.DrawText(MakeText("Z", 8, zPen.Brush), new Point(ax + dxZ + 2, ay + dyZ - 5));
    }

    private static void NormalizeDir(ref double dx, ref double dy, float length)
    {
        double mag = Math.Sqrt(dx * dx + dy * dy);
        if (mag < 0.001) return;
        dx = dx / mag * length;
        dy = dy / mag * length;
    }

    private void RenderInfo(ThemePalette palette, in RoomSnapshot snap, double w, double h)
    {
        using var dc = _infoVisual.RenderOpen();

        float volume = RoomAcousticsCalculator.CalculateRoomVolume(in snap);
        float distance = RoomAcousticsCalculator.CalculateDirectDistance(in snap);

        Span<string> lines =
        [
            $"{snap.Width:F1}\u00D7{snap.Height:F1}\u00D7{snap.Depth:F1} m",
            $"Vol: {volume:F1} m\u00B3",
            $"Dist: {distance:F2} m",
            $"RT60: {snap.DecayTime:F2} s",
            $"Mix: {snap.DryWetMix * 100:F0}%"
        ];

        double y = 6;
        foreach (var line in lines)
        {
            dc.DrawText(MakeText(line, 9, palette.InfoText), new Point(6, y));
            y += 14;
        }
    }

    private static float CalculateGridStep(float dimension)
    {
        if (dimension <= 5) return 1.0f;
        if (dimension <= 15) return 2.0f;
        if (dimension <= 30) return 5.0f;
        return 10.0f;
    }

    private void RenderGridOverlay(Camera3D camera, Matrix4x4 viewMat, ThemePalette palette,
        in RoomSnapshot snap, double w, double h, bool showGrid, float gridSize, bool showDimensions)
    {
        using var dc = _gridVisual.RenderOpen();

        float w3 = snap.Width > 0 ? snap.Width : (snap.Geometry?.Vertices.Length > 0
            ? snap.Geometry.Vertices.Max(v => v.X) : 8f);
        float h3 = snap.Height > 0 ? snap.Height : (snap.Geometry?.Vertices.Length > 0
            ? snap.Geometry.Vertices.Max(v => v.Y) : 3f);
        float d3 = snap.Depth > 0 ? snap.Depth : (snap.Geometry?.Vertices.Length > 0
            ? snap.Geometry.Vertices.Max(v => v.Z) : 6f);

        var gridPen = new Pen(new SolidColorBrush(palette.InfoText.Color) { Opacity = 0.18 }, 0.7);
        gridPen.DashStyle = DashStyles.Dot;
        gridPen.Freeze();

        var dimBrush = new SolidColorBrush(palette.InfoText.Color);
        dimBrush.Freeze();

        if (showGrid)
        {
            float gs = Math.Max(0.1f, gridSize);
            for (float gx = 0; gx <= w3 + 0.001f; gx += gs)
            {
                float x = Math.Min(gx, w3);
                for (float gy = 0; gy <= h3 + 0.001f; gy += gs)
                {
                    float y = Math.Min(gy, h3);
                    DrawGridPoint(dc, new Vector3(x, y, 0), new Vector3(x, y, d3), viewMat, w, h, gridPen);
                }
            }
            for (float gz = 0; gz <= d3 + 0.001f; gz += gs)
            {
                float z = Math.Min(gz, d3);
                DrawClippedLine(dc, gridPen, new Vector3(0, 0, z), new Vector3(w3, 0, z), viewMat, w, h);
                DrawClippedLine(dc, gridPen, new Vector3(0, h3, z), new Vector3(w3, h3, z), viewMat, w, h);
            }
        }

        if (showDimensions)
        {
            IEnumerable<(Vector3 A, Vector3 B, string Label)> edgesToDraw;

            if (snap.Shape == RoomShape.Custom && snap.Geometry is { } g && g.Vertices.Length > 0)
            {
                var edgeSet = new HashSet<(int, int)>();
                var customEdges = new List<(Vector3 A, Vector3 B, string Label)>();
                foreach (var face in g.Faces)
                {
                    if (face.VertexIndices.Length < 2) continue;
                    for (int i = 0; i < face.VertexIndices.Length; i++)
                    {
                        int a = face.VertexIndices[i];
                        int b = face.VertexIndices[(i + 1) % face.VertexIndices.Length];
                        if (a < 0 || a >= g.Vertices.Length || b < 0 || b >= g.Vertices.Length) continue;
                        var key = a < b ? (a, b) : (b, a);
                        if (edgeSet.Add(key))
                        {
                            var va = g.Vertices[a];
                            var vb = g.Vertices[b];
                            float dist = Vector3.Distance(new Vector3(va.X, va.Y, va.Z), new Vector3(vb.X, vb.Y, vb.Z));
                            if (dist > 0.001f)
                            {
                                customEdges.Add((new Vector3(va.X, va.Y, va.Z), new Vector3(vb.X, vb.Y, vb.Z), $"{dist:F3}m"));
                            }
                        }
                    }
                }
                edgesToDraw = customEdges;
            }
            else
            {
                edgesToDraw = new (Vector3 A, Vector3 B, string Label)[]
                {
                    (new(0,0,0),   new(w3,0,0),  $"{w3:F3}m"),
                    (new(0,h3,0),  new(w3,h3,0), $"{w3:F3}m"),
                    (new(0,0,d3),  new(w3,0,d3), $"{w3:F3}m"),
                    (new(0,h3,d3), new(w3,h3,d3),$"{w3:F3}m"),
                    (new(0,0,0),   new(0,0,d3),  $"{d3:F3}m"),
                    (new(w3,0,0),  new(w3,0,d3), $"{d3:F3}m"),
                    (new(0,h3,0),  new(0,h3,d3), $"{d3:F3}m"),
                    (new(w3,h3,0), new(w3,h3,d3),$"{d3:F3}m"),
                    (new(0,0,0),   new(0,h3,0),  $"{h3:F3}m"),
                    (new(w3,0,0),  new(w3,h3,0), $"{h3:F3}m"),
                    (new(0,0,d3),  new(0,h3,d3), $"{h3:F3}m"),
                    (new(w3,0,d3), new(w3,h3,d3),$"{h3:F3}m")
                };
            }

            foreach (var (a, b, label) in edgesToDraw)
            {
                var ac = Vector3.Transform(a, viewMat);
                var bc = Vector3.Transform(b, viewMat);
                if (ac.Z > -ProjectionMatrix.NearPlane || bc.Z > -ProjectionMatrix.NearPlane) continue;
                var pa = ProjectionMatrix.ProjectToScreen(ac, w, h);
                var pb = ProjectionMatrix.ProjectToScreen(bc, w, h);
                double mx = (pa.X + pb.X) * 0.5;
                double my = (pa.Y + pb.Y) * 0.5;
                dc.DrawText(MakeText(label, 9, dimBrush), new Point(mx + 2, my - 8));
            }
        }
    }

    private static void DrawGridPoint(DrawingContext dc, Vector3 a, Vector3 b, Matrix4x4 viewMat, double w, double h, Pen pen)
    {
        DrawClippedLine(dc, pen, a, b, viewMat, w, h);
    }

    private void RenderCustomGeometry(DrawingContext dc, Matrix4x4 viewMat, RoomGeometry geo, in RoomSnapshot snap, double w, double h)
    {
        string wallMat = snap.WallMaterialId;
        var faceDataList = new List<(float depth, int faceIdx)>();
        for (int fi = 0; fi < geo.Faces.Length; fi++)
        {
            var face = geo.Faces[fi];
            if (face.VertexIndices.Length < 3) continue;
            float cx = 0, cy = 0, cz = 0;
            foreach (var vi in face.VertexIndices)
            {
                if (vi < 0 || vi >= geo.Vertices.Length) continue;
                var sv = geo.Vertices[vi];
                cx += sv.X; cy += sv.Y; cz += sv.Z;
            }
            float n = face.VertexIndices.Length;
            var center = Vector3.Transform(new Vector3(cx / n, cy / n, cz / n), viewMat);
            faceDataList.Add((-center.Z, fi));
        }
        faceDataList.Sort((a, b) => b.depth.CompareTo(a.depth));

        Span<Vector3> camSpan = stackalloc Vector3[16];
        Span<Vector3> clippedSpan = stackalloc Vector3[24];

        foreach (var (_, fi) in faceDataList)
        {
            var face = geo.Faces[fi];
            if (face.VertexIndices.Length < 3) continue;

            string mat = wallMat;
            if (face.MaterialIndex >= 0 && face.MaterialIndex < geo.Materials.Length)
            {
                var mName = geo.Materials[face.MaterialIndex].Name.ToLowerInvariant();
                if (mName.Contains("floor")) mat = "wood";
                else if (mName.Contains("ceil") || mName.Contains("ceiling")) mat = "drywall";
            }

            int vCount = Math.Min(face.VertexIndices.Length, 16);
            bool valid = true;
            for (int i = 0; i < vCount; i++)
            {
                int vi = face.VertexIndices[i];
                if (vi < 0 || vi >= geo.Vertices.Length) { valid = false; break; }
                var sv = geo.Vertices[vi];
                camSpan[i] = Vector3.Transform(new Vector3(sv.X, sv.Y, sv.Z), viewMat);
            }
            if (!valid) continue;

            int nClipped = ProjectionMatrix.ClipPolygonZ(camSpan[..vCount], clippedSpan);
            if (nClipped < 3) continue;

            var wallBrush = GetMaterialBrush(mat, 40);
            var edgePen = GetMaterialPen(mat, 160, 1.0);

            var faceGeo = new StreamGeometry();
            using (var ctx = faceGeo.Open())
            {
                ctx.BeginFigure(ProjectionMatrix.ProjectToScreen(clippedSpan[0], w, h), true, true);
                for (int i = 1; i < nClipped; i++)
                    ctx.LineTo(ProjectionMatrix.ProjectToScreen(clippedSpan[i], w, h), true, false);
            }
            faceGeo.Freeze();
            dc.DrawGeometry(wallBrush, edgePen, faceGeo);
        }

        var edgeSet = new HashSet<(int, int)>();
        var linePen = GetMaterialPen(wallMat, 180, 1.0);
        foreach (var face in geo.Faces)
        {
            if (face.VertexIndices.Length < 2) continue;
            for (int i = 0; i < face.VertexIndices.Length; i++)
            {
                int a = face.VertexIndices[i];
                int b = face.VertexIndices[(i + 1) % face.VertexIndices.Length];
                if (a < 0 || a >= geo.Vertices.Length || b < 0 || b >= geo.Vertices.Length) continue;
                var key = a < b ? (a, b) : (b, a);
                if (!edgeSet.Add(key)) continue;
                var va = geo.Vertices[a];
                var vb = geo.Vertices[b];
                DrawClippedLine(dc, linePen, new Vector3(va.X, va.Y, va.Z), new Vector3(vb.X, vb.Y, vb.Z), viewMat, w, h);
            }
        }
    }



    private readonly record struct TextKey(string Text, double Size, Brush Brush);
    private static readonly ConcurrentDictionary<TextKey, FormattedText> _textCache = new();

    private static FormattedText MakeText(string text, double size, Brush brush)
    {
        var key = new TextKey(text, size, brush);
        if (_textCache.TryGetValue(key, out var ft)) return ft;
        ft = new FormattedText(text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
            InfoTypeface, size, brush, Dpi);
        _textCache.TryAdd(key, ft);
        return ft;
    }

    private static Pen FrozenPen(Brush brush, double thickness)
    {
        if (!brush.IsFrozen) brush.Freeze();
        var pen = new Pen(brush, thickness);
        pen.Freeze();
        return pen;
    }

    private static SolidColorBrush MakeFrozenBrush(Color color)
    {
        var b = new SolidColorBrush(color);
        b.Freeze();
        return b;
    }

    private static Color GetMaterialColor(string m)
    {
        var mat = SpaceAudio.Services.ServiceLocator.MaterialService.GetById(m);
        return mat?.MaterialColor ?? Color.FromRgb(120, 120, 120);
    }

    private readonly record struct BrushKey(string Mat, byte Alpha);
    private static readonly ConcurrentDictionary<BrushKey, SolidColorBrush> _brushCache = new();

    private static SolidColorBrush GetMaterialBrush(string m, byte alpha)
    {
        var key = new BrushKey(m, alpha);
        if (_brushCache.TryGetValue(key, out var cached)) return cached;
        var c = GetMaterialColor(m);
        var b = new SolidColorBrush(Color.FromArgb(alpha, c.R, c.G, c.B));
        b.Freeze();
        _brushCache.TryAdd(key, b);
        return b;
    }

    private readonly record struct PenKey(string Mat, byte Alpha, double Thickness);
    private static readonly ConcurrentDictionary<PenKey, Pen> _penCache = new();

    private static Pen GetMaterialPen(string m, byte alpha, double t)
    {
        var key = new PenKey(m, alpha, t);
        if (_penCache.TryGetValue(key, out var cached)) return cached;
        var p = new Pen(GetMaterialBrush(m, alpha), t);
        p.Freeze();
        _penCache.TryAdd(key, p);
        return p;
    }

    private readonly record struct FaceData(Vector3 P0, Vector3 P1, Vector3 P2, Vector3 P3, float Depth, string Material)
    {
        public FaceData(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, Vector3 eye, string mat) : this(
            p0, p1, p2, p3,
            ((p0 + p1 + p2 + p3) * 0.25f - eye).LengthSquared(), mat)
        { }
    }

    private static void SortByDepthDescending(Span<FaceData> faces)
    {
        for (int i = 1; i < faces.Length; i++)
        {
            var key = faces[i];
            float keyDepth = key.Depth;
            int j = i - 1;
            while (j >= 0 && faces[j].Depth < keyDepth)
            {
                faces[j + 1] = faces[j];
                j--;
            }
            faces[j + 1] = key;
        }
    }

    protected override Size MeasureOverride(Size availableSize) =>
        new(
            double.IsInfinity(availableSize.Width) ? 400 : availableSize.Width,
            double.IsInfinity(availableSize.Height) ? 300 : availableSize.Height);
}
