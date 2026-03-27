using SpaceAudio.Audio;
using SpaceAudio.Enums;
using SpaceAudio.Models;
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

    private static readonly Typeface InfoTypeface = new("Segoe UI");
    private const double Dpi = 96.0;

    public RoomVisualHost()
    {
        _children = new VisualCollection(this)
        {
            _floorVisual,
            _wallVisual,
            _objectVisual,
            _infoVisual
        };
    }

    protected override int VisualChildrenCount => _children.Count;
    protected override Visual GetVisualChild(int index) => _children[index];

    public void Render(Camera3D camera, ThemePalette palette, RoomSnapshot snapshot, double viewWidth, double viewHeight, bool isSourceSelected, bool isListenerSelected)
    {
        if (viewWidth <= 0 || viewHeight <= 0) return;

        var viewMat = ProjectionMatrix.CreateViewMatrix(camera);

        RenderFloor(viewMat, palette, snapshot, viewWidth, viewHeight);
        RenderWalls(camera, viewMat, palette, snapshot, viewWidth, viewHeight);
        RenderObjects(viewMat, palette, snapshot, viewWidth, viewHeight, isSourceSelected, isListenerSelected);
        RenderInfo(palette, snapshot, viewWidth, viewHeight);
    }

    private void RenderFloor(Matrix4x4 viewMat, ThemePalette palette, in RoomSnapshot snap, double w, double h)
    {
        using var dc = _floorVisual.RenderOpen();
        if (snap.Width <= 0 || snap.Depth <= 0) return;

        var gridPen = GetMaterialPen(snap.FloorMat, 100, 0.8);
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

        var ceilMat = snap.CeilMat;
        var floorMat = snap.FloorMat;
        var wallMat = snap.WallMat;

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
                {
                    ctx.LineTo(ProjectionMatrix.ProjectToScreen(clippedVertices[i], w, h), true, false);
                }
            }
            geo.Freeze();
            dc.DrawGeometry(wallBrush, edgePen, geo);
        }

        var cornerPen = GetMaterialPen(snap.WallMat, 150, 1.0);
        for (int i = 0; i < lCount; i++)
        {
            DrawClippedLine(dc, cornerPen, lines[i].Item1, lines[i].Item2, viewMat, w, h);
        }
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
        var brush = new SolidColorBrush(Color.FromRgb((byte)Math.Min(255, c.R + 80), (byte)Math.Min(255, c.G + 80), (byte)Math.Min(255, c.B + 80)));
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

    private static FormattedText MakeText(string text, double size, Brush brush)
    {
        var ft = new FormattedText(text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, InfoTypeface, size, brush, Dpi);
        return ft;
    }

    private static Pen FrozenPen(Brush brush, double thickness)
    {
        if (!brush.IsFrozen) brush.Freeze();
        var pen = new Pen(brush, thickness);
        pen.Freeze();
        return pen;
    }

    private static Color GetMaterialColor(WallMaterial m) => m switch
    {
        WallMaterial.Concrete => Color.FromRgb(150, 155, 160),
        WallMaterial.Wood => Color.FromRgb(170, 100, 50),
        WallMaterial.Glass => Color.FromRgb(150, 220, 255),
        WallMaterial.Carpet => Color.FromRgb(180, 60, 60),
        WallMaterial.AcousticPanel => Color.FromRgb(80, 85, 95),
        WallMaterial.Brick => Color.FromRgb(190, 70, 50),
        WallMaterial.Drywall => Color.FromRgb(220, 225, 230),
        WallMaterial.Tile => Color.FromRgb(210, 230, 235),
        _ => Color.FromRgb(120, 120, 120)
    };

    private static SolidColorBrush GetMaterialBrush(WallMaterial m, byte alpha)
    {
        var c = GetMaterialColor(m);
        var b = new SolidColorBrush(Color.FromArgb(alpha, c.R, c.G, c.B));
        b.Freeze();
        return b;
    }

    private static Pen GetMaterialPen(WallMaterial m, byte alpha, double t)
    {
        var p = new Pen(GetMaterialBrush(m, alpha), t);
        p.Freeze();
        return p;
    }

    private readonly record struct FaceData(Vector3 P0, Vector3 P1, Vector3 P2, Vector3 P3, float Depth, WallMaterial Material)
    {
        public FaceData(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, Vector3 eye, WallMaterial mat) : this(
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
