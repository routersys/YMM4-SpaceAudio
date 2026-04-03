using SpaceAudio.Localization;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Numerics;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace SpaceAudio.Models;

public static class BlueprintManager
{
    private const int A4Width = 3508;
    private const int A4Height = 2480;
    private const double Margin = 250;

    public static void ExportBlueprint(RoomGeometry geo, string filepath)
    {
        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            dc.DrawRectangle(Brushes.White, null, new Rect(0, 0, A4Width, A4Height));

            var solidPen = new Pen(Brushes.Black, 6.0) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round, LineJoin = PenLineJoin.Round };
            var dashedPen = new Pen(Brushes.Black, 3.5) { DashStyle = new DashStyle(new double[] { 4, 3 }, 0), StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
            var dashDotPen = new Pen(Brushes.Black, 2.0) { DashStyle = new DashStyle(new double[] { 10, 4, 2, 4 }, 0) };
            var dimPen = new Pen(Brushes.Black, 1.5);

            var framePen = new Pen(Brushes.Black, 10.0) { LineJoin = PenLineJoin.Miter };
            dc.DrawRectangle(null, framePen, new Rect(50, 50, A4Width - 100, A4Height - 100));
            dc.DrawRectangle(null, new Pen(Brushes.Black, 3.0), new Rect(70, 70, A4Width - 140, A4Height - 140));

            DrawTitleBlock(dc, geo);

            float minX = float.MaxValue, minY = float.MaxValue, minZ = float.MaxValue;
            float maxX = float.MinValue, maxY = float.MinValue, maxZ = float.MinValue;

            foreach (var v in geo.Vertices)
            {
                if (v.X < minX) minX = v.X; if (v.X > maxX) maxX = v.X;
                if (v.Y < minY) minY = v.Y; if (v.Y > maxY) maxY = v.Y;
                if (v.Z < minZ) minZ = v.Z; if (v.Z > maxZ) maxZ = v.Z;
            }

            float width = maxX - minX;
            float height = maxY - minY;
            float depth = maxZ - minZ;

            double availW = (A4Width - Margin * 3) / 2.0;
            double availH = (A4Height - 150 - Margin * 3) / 2.0;

            double scaleTop = Math.Min(availW / Math.Max(width, 0.1), availH / Math.Max(depth, 0.1));
            double scaleFront = Math.Min(availW / Math.Max(width, 0.1), availH / Math.Max(height, 0.1));
            double scaleSide = Math.Min(availW / Math.Max(depth, 0.1), availH / Math.Max(height, 0.1));

            double scale = Math.Min(scaleTop, Math.Min(scaleFront, scaleSide)) * 0.70;

            Point topCenter = new Point(Margin + availW / 2, Margin + availH / 2 - 50);
            Point frontCenter = new Point(Margin + availW / 2, Margin * 2 + availH * 1.5 - 50);
            Point sideCenter = new Point(Margin * 2 + availW * 1.5, Margin * 2 + availH * 1.5 - 50);

            Vector3 camTop = new Vector3(0, 1, 0);
            Vector3 camFront = new Vector3(0, 0, -1);
            Vector3 camSide = new Vector3(-1, 0, 0);

            DrawView(dc, geo, topCenter, scale, minX, maxX, minZ, maxZ,
                v => new Point(v.X, v.Z), camTop, Texts.BlueprintViewPlan, solidPen, dashedPen, dimPen);
            DrawView(dc, geo, frontCenter, scale, minX, maxX, -maxY, -minY,
                v => new Point(v.X, -v.Y), camFront, Texts.BlueprintViewFront, solidPen, dashedPen, dimPen);
            DrawView(dc, geo, sideCenter, scale, minZ, maxZ, -maxY, -minY,
                v => new Point(v.Z, -v.Y), camSide, Texts.BlueprintViewSide, solidPen, dashedPen, dimPen);

            DrawVisualBarcode(dc, geo);
        }

        var rtb = new RenderTargetBitmap(A4Width, A4Height, 96, 96, PixelFormats.Pbgra32);
        rtb.Render(visual);

        var encoder = new PngBitmapEncoder();
        string json = JsonSerializer.Serialize(geo);
        var metadata = new BitmapMetadata("png");
        metadata.SetQuery("/tEXt/{str=Description}", "SpaceAudioBlueprint:" + json);

        var frame = BitmapFrame.Create(rtb, null, metadata, null);
        encoder.Frames.Add(frame);

        using var fs = new FileStream(filepath, FileMode.Create, FileAccess.Write);
        encoder.Save(fs);
    }

    public static RoomGeometry? ImportBlueprint(string filepath)
    {
        try
        {
            using var fs = new FileStream(filepath, FileMode.Open, FileAccess.Read);
            var decoder = new PngBitmapDecoder(fs, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
            var frame = decoder.Frames[0];
            var metadata = frame.Metadata as BitmapMetadata;
            if (metadata != null && metadata.ContainsQuery("/tEXt/{str=Description}"))
            {
                string desc = metadata.GetQuery("/tEXt/{str=Description}") as string ?? "";
                if (desc.StartsWith("SpaceAudioBlueprint:"))
                {
                    string json = desc.Substring("SpaceAudioBlueprint:".Length);
                    return JsonSerializer.Deserialize<RoomGeometry>(json);
                }
            }
            return ExtractVisualBarcode(filepath);
        }
        catch
        {
            return ExtractVisualBarcode(filepath);
        }
    }

    private static uint ComputeFnv1a(byte[] data)
    {
        uint hash = 2166136261;
        foreach (var b in data)
        {
            hash ^= b;
            hash *= 16777619;
        }
        return hash;
    }

    private static void DrawVisualBarcode(DrawingContext dc, RoomGeometry geo)
    {
        string json = JsonSerializer.Serialize(geo);
        byte[] jsonBytes = Encoding.UTF8.GetBytes(json);

        byte[] compressed;
        using (var ms = new MemoryStream())
        {
            using (var gz = new GZipStream(ms, CompressionLevel.Optimal, true))
            {
                gz.Write(jsonBytes, 0, jsonBytes.Length);
            }
            compressed = ms.ToArray();
        }

        uint crc = ComputeFnv1a(compressed);
        int len = compressed.Length;

        byte[] payload = new byte[8 + len];
        payload[0] = (byte)(crc & 0xFF);
        payload[1] = (byte)((crc >> 8) & 0xFF);
        payload[2] = (byte)((crc >> 16) & 0xFF);
        payload[3] = (byte)((crc >> 24) & 0xFF);

        payload[4] = (byte)(len & 0xFF);
        payload[5] = (byte)((len >> 8) & 0xFF);
        payload[6] = (byte)((len >> 16) & 0xFF);
        payload[7] = (byte)((len >> 24) & 0xFF);

        Array.Copy(compressed, 0, payload, 8, len);

        int cellSize = 4;
        int startX = (int)(Margin * 2 + (A4Width - Margin * 3) / 2.0) + 10;
        int startY = 80;
        int cols = (A4Width - 80 - startX) / cellSize;

        var black = Brushes.Black;
        var drawPen = new Pen(Brushes.White, 0);

        int totalBits = payload.Length * 8;

        for (int i = 0; i < totalBits; i++)
        {
            int byteIdx = i / 8;
            int bitOffset = i % 8;
            bool isSet = (payload[byteIdx] & (1 << bitOffset)) != 0;

            if (isSet)
            {
                int r = i / cols;
                int c = i % cols;
                dc.DrawRectangle(black, drawPen, new Rect(startX + c * cellSize, startY + r * cellSize, cellSize, cellSize));
            }
        }

        dc.DrawRectangle(null, new Pen(Brushes.Gray, 1.0), new Rect(startX - 2, startY - 2, cols * cellSize + 4, (totalBits / cols + 1) * cellSize + 4));
    }

    private static RoomGeometry? ExtractVisualBarcode(string filepath)
    {
        try
        {
            var bmp = new BitmapImage();
            bmp.BeginInit();
            bmp.UriSource = new Uri(filepath);
            bmp.CacheOption = BitmapCacheOption.OnLoad;
            bmp.EndInit();

            if (bmp.PixelWidth != A4Width || bmp.PixelHeight != A4Height) return null;

            int cellSize = 4;
            int startX = (int)(Margin * 2 + (A4Width - Margin * 3) / 2.0) + 10;
            int startY = 80;
            int cols = (A4Width - 80 - startX) / cellSize;

            int formatBpp = bmp.Format.BitsPerPixel;
            if (formatBpp < 24) return null;
            int bytesPerPixel = formatBpp / 8;

            int stride = (bmp.PixelWidth * formatBpp + 7) / 8;
            byte[] pixels = new byte[bmp.PixelHeight * stride];
            bmp.CopyPixels(pixels, stride, 0);

            bool ReadBit(int bitIndex)
            {
                int r = bitIndex / cols;
                int c = bitIndex % cols;
                int px = startX + c * cellSize + cellSize / 2;
                int py = startY + r * cellSize + cellSize / 2;

                int offset = py * stride + px * bytesPerPixel;
                byte b = pixels[offset];
                byte g = pixels[offset + 1];
                byte rd = pixels[offset + 2];
                int luminance = (rd * 299 + g * 587 + b * 114) / 1000;
                return luminance < 128;
            }

            byte[] header = new byte[8];
            for (int i = 0; i < 64; i++)
            {
                if (ReadBit(i))
                {
                    header[i / 8] |= (byte)(1 << (i % 8));
                }
            }

            uint crc = (uint)(header[0] | (header[1] << 8) | (header[2] << 16) | (header[3] << 24));
            int len = header[4] | (header[5] << 8) | (header[6] << 16) | (header[7] << 24);

            if (len <= 0 || len > 1024 * 1024) return null;

            int totalBitsToRead = (len + 8) * 8;
            if (startX + cols * cellSize > A4Width || startY + (totalBitsToRead / cols + 1) * cellSize > A4Height)
                return null;

            byte[] payload = new byte[len];
            for (int i = 64; i < totalBitsToRead; i++)
            {
                if (ReadBit(i))
                {
                    int pIdx = (i - 64) / 8;
                    int bOffset = (i - 64) % 8;
                    payload[pIdx] |= (byte)(1 << bOffset);
                }
            }

            if (ComputeFnv1a(payload) != crc) return null;

            using (var ms = new MemoryStream(payload))
            {
                using (var gz = new GZipStream(ms, CompressionMode.Decompress))
                using (var reader = new StreamReader(gz, Encoding.UTF8))
                {
                    string json = reader.ReadToEnd();
                    return JsonSerializer.Deserialize<RoomGeometry>(json);
                }
            }
        }
        catch
        {
            return null;
        }
    }

    private static void DrawTitleBlock(DrawingContext dc, RoomGeometry geo)
    {
        double bw = 800;
        double bh = 160;
        Rect blockRect = new Rect(A4Width - 70 - bw, A4Height - 70 - bh, bw, bh);

        var pen = new Pen(Brushes.Black, 4.0);
        var thinPen = new Pen(Brushes.Black, 2.0);
        dc.DrawRectangle(Brushes.White, pen, blockRect);

        dc.DrawLine(thinPen, new Point(blockRect.Left, blockRect.Top + 60), new Point(blockRect.Right, blockRect.Top + 60));
        dc.DrawLine(thinPen, new Point(blockRect.Left + 200, blockRect.Top), new Point(blockRect.Left + 200, blockRect.Bottom));

        var tfSmall = new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);
        var tfBold = new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal);

        var ftTitleLabel = new FormattedText(Texts.BlueprintProjectName, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, tfSmall, 24, Brushes.Black, 1.25);
        dc.DrawText(ftTitleLabel, new Point(blockRect.Left + 20, blockRect.Top + 15));

        string name = string.IsNullOrWhiteSpace(geo.Name) ? Texts.BlueprintUnnamed : geo.Name;
        var ftName = new FormattedText(name, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, tfBold, 42, Brushes.Black, 1.25);
        if (ftName.Width > bw - 240) ftName.SetFontSize(32);
        dc.DrawText(ftName, new Point(blockRect.Left + 220, blockRect.Top + 8));

        var ftSys = new FormattedText(Texts.BlueprintSystemName, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, tfSmall, 24, Brushes.Gray, 1.25);
        dc.DrawText(ftSys, new Point(blockRect.Right - ftSys.Width - 20, blockRect.Bottom - 40));

        var date = DateTime.Now.ToString("yyyy-MM-dd");
        var ftDateLabel = new FormattedText(Texts.BlueprintDate, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, tfSmall, 20, Brushes.Black, 1.25);
        dc.DrawText(ftDateLabel, new Point(blockRect.Left + 20, blockRect.Top + 75));
        var ftDate = new FormattedText(date, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, tfBold, 30, Brushes.Black, 1.25);
        dc.DrawText(ftDate, new Point(blockRect.Left + 20, blockRect.Top + 105));
    }

    private static void DrawView(DrawingContext dc, RoomGeometry geo, Point center, double scale, float minH, float maxH, float minV, float maxV, Func<GeometryVertex, Point> project, Vector3 cameraDir, string title, Pen solid, Pen dashed, Pen dim)
    {
        double cx = (minH + maxH) / 2.0;
        double cy = (minV + maxV) / 2.0;
        double boxW = (maxH - minH) * scale;
        double boxH = (maxV - minV) * scale;

        Dictionary<Tuple<int, int>, List<Vector3>> edges = new();
        foreach (var face in geo.Faces)
        {
            if (face.VertexIndices.Length < 3) continue;

            var v0 = new Vector3(geo.Vertices[face.VertexIndices[0]].X, geo.Vertices[face.VertexIndices[0]].Y, geo.Vertices[face.VertexIndices[0]].Z);
            var v1 = new Vector3(geo.Vertices[face.VertexIndices[1]].X, geo.Vertices[face.VertexIndices[1]].Y, geo.Vertices[face.VertexIndices[1]].Z);
            var v2 = new Vector3(geo.Vertices[face.VertexIndices[2]].X, geo.Vertices[face.VertexIndices[2]].Y, geo.Vertices[face.VertexIndices[2]].Z);

            var normal = Vector3.Normalize(Vector3.Cross(v1 - v0, v2 - v0));
            for (int i = 0; i < face.VertexIndices.Length; i++)
            {
                int p1 = face.VertexIndices[i];
                int p2 = face.VertexIndices[(i + 1) % face.VertexIndices.Length];
                int min = Math.Min(p1, p2);
                int max = Math.Max(p1, p2);
                var key = new Tuple<int, int>(min, max);

                if (!edges.ContainsKey(key))
                    edges[key] = new List<Vector3>();
                edges[key].Add(normal);
            }
        }

        List<Tuple<Point, Point>> visibleEdges = new();
        List<Tuple<Point, Point>> hiddenEdges = new();
        List<double> coordsH = new();
        List<double> coordsV = new();

        foreach (var kvp in edges)
        {
            var proj1 = project(geo.Vertices[kvp.Key.Item1]);
            var proj2 = project(geo.Vertices[kvp.Key.Item2]);

            coordsH.Add(proj1.X); coordsH.Add(proj2.X);
            coordsV.Add(proj1.Y); coordsV.Add(proj2.Y);

            Point sp1 = new Point(center.X + (proj1.X - cx) * scale, center.Y + (proj1.Y - cy) * scale);
            Point sp2 = new Point(center.X + (proj2.X - cx) * scale, center.Y + (proj2.Y - cy) * scale);

            bool isVisible = false;
            foreach (var n in kvp.Value)
            {
                if (Vector3.Dot(n, cameraDir) > -0.001f)
                {
                    isVisible = true;
                    break;
                }
            }
            if (isVisible) visibleEdges.Add(new Tuple<Point, Point>(sp1, sp2));
            else hiddenEdges.Add(new Tuple<Point, Point>(sp1, sp2));
        }

        foreach (var e in hiddenEdges) dc.DrawLine(dashed, e.Item1, e.Item2);
        foreach (var e in visibleEdges) dc.DrawLine(solid, e.Item1, e.Item2);

        var ticksH = coordsH.GroupBy(x => Math.Round(x, 2)).Select(g => g.First()).OrderBy(x => x).ToList();
        var ticksV = coordsV.GroupBy(y => Math.Round(y, 2)).Select(g => g.First()).OrderBy(y => y).ToList();

        double dimOffsetBase = 60;

        int leaderCountH = CountLeaders(ticksH, cx, scale);
        int leaderCountV = CountLeaders(ticksV, cy, scale);
        var placedLeaders = new List<Rect>();

        double chainY = center.Y + boxH / 2 + dimOffsetBase;
        DrawDimensionChain(dc, dim, ticksH, cx, center.X, scale, chainY, true, leaderCountH, placedLeaders);
        double totalDimOffsetH = 50;
        if (ticksH.Count > 2)
        {
            totalDimOffsetH = ComputeTotalDimOffset(ticksH, cx, scale, true, leaderCountH);
            DrawDimensionLabel(dc, dim,
                new Point(center.X + (ticksH.First() - cx) * scale, chainY + totalDimOffsetH),
                new Point(center.X + (ticksH.Last() - cx) * scale, chainY + totalDimOffsetH),
                $"{ticksH.Last() - ticksH.First():F2} m", false);
        }

        double chainX = center.X - boxW / 2 - dimOffsetBase;
        DrawDimensionChain(dc, dim, ticksV, cy, center.Y, scale, chainX, false, leaderCountV, placedLeaders);
        double totalDimOffsetV = 50;
        if (ticksV.Count > 2)
        {
            totalDimOffsetV = ComputeTotalDimOffset(ticksV, cy, scale, false, leaderCountV);
            DrawDimensionLabel(dc, dim,
                new Point(chainX - totalDimOffsetV, center.Y + (ticksV.First() - cy) * scale),
                new Point(chainX - totalDimOffsetV, center.Y + (ticksV.Last() - cy) * scale),
                $"{ticksV.Last() - ticksV.First():F2} m", true);
        }

        double titleY = chainY + (ticksH.Count > 2 ? totalDimOffsetH + 60 : 80);
        var tf = new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal);
        var ft = new FormattedText(title, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, tf, 48, Brushes.Black, 1.25);
        dc.DrawText(ft, new Point(center.X - ft.Width / 2, titleY));
    }

    private static int CountLeaders(List<double> ticks, double centerRaw, double scale)
    {
        var tfMeasure = new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.SemiBold, FontStretches.Normal);
        int count = 0;
        for (int i = 0; i < ticks.Count - 1; i++)
        {
            double segLen = Math.Abs((ticks[i + 1] - ticks[i]) * scale);
            var ftM = new FormattedText($"{ticks[i + 1] - ticks[i]:F2} m", CultureInfo.InvariantCulture, FlowDirection.LeftToRight, tfMeasure, 28, Brushes.Black, 1.25);
            if (segLen < ftM.Width + 20) count++;
        }
        return count;
    }

    private static double ComputeTotalDimOffset(List<double> ticks, double centerRaw, double scale, bool isHorizontal, int totalLeaders)
    {
        var tfMeasure = new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.SemiBold, FontStretches.Normal);
        double maxLeader = 50;
        int leaderIdx = 0;
        for (int i = 0; i < ticks.Count - 1; i++)
        {
            double segLen = Math.Abs((ticks[i + 1] - ticks[i]) * scale);
            var ftM = new FormattedText($"{ticks[i + 1] - ticks[i]:F2} m", CultureInfo.InvariantCulture, FlowDirection.LeftToRight, tfMeasure, 28, Brushes.Black, 1.25);
            if (segLen < ftM.Width + 20)
            {
                double diagLen = 40 + leaderIdx * 30;
                double needed = diagLen + ftM.Height + 15;
                if (needed > maxLeader) maxLeader = needed;
                leaderIdx++;
            }
        }
        return maxLeader + 30;
    }

    private static void DrawDimensionChain(DrawingContext dc, Pen pen, List<double> ticks, double centerRaw, double centerScreen, double scale, double offsetPos, bool isHorizontal, int totalLeaders, List<Rect> placedLeaders)
    {
        int leaderIdx = 0;
        for (int i = 0; i < ticks.Count - 1; i++)
        {
            double fromRaw = ticks[i];
            double toRaw = ticks[i + 1];
            double val = toRaw - fromRaw;
            if (val < 0.005) continue;

            Point p1 = isHorizontal
                ? new Point(centerScreen + (fromRaw - centerRaw) * scale, offsetPos)
                : new Point(offsetPos, centerScreen + (fromRaw - centerRaw) * scale);
            Point p2 = isHorizontal
                ? new Point(centerScreen + (toRaw - centerRaw) * scale, offsetPos)
                : new Point(offsetPos, centerScreen + (toRaw - centerRaw) * scale);

            var tfCheck = new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.SemiBold, FontStretches.Normal);
            var ftCheck = new FormattedText($"{val:F2} m", CultureInfo.InvariantCulture, FlowDirection.LeftToRight, tfCheck, 28, Brushes.Black, 1.25);
            double segLen = (p2 - p1).Length;
            bool needsLeader = segLen < ftCheck.Width + 20;

            DrawDimensionWithLeader(dc, pen, p1, p2, $"{val:F2} m", needsLeader ? leaderIdx : -1, isHorizontal, totalLeaders, placedLeaders);

            if (needsLeader) leaderIdx++;
        }
    }

    private static void DrawDimensionWithLeader(DrawingContext dc, Pen pen, Point start, Point end, string text, int leaderIndex, bool isHorizontal, int totalLeaders, List<Rect> placedLeaders)
    {
        dc.DrawLine(pen, start, end);

        System.Windows.Vector v = end - start;
        double len = v.Length;
        if (len < 1) return;
        v.Normalize();

        System.Windows.Vector n = new System.Windows.Vector(-v.Y, v.X);
        dc.DrawLine(pen, start - n * 10, start + n * 10);
        dc.DrawLine(pen, end - n * 10, end + n * 10);

        Point mid = new Point((start.X + end.X) / 2, (start.Y + end.Y) / 2);
        var tf = new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.SemiBold, FontStretches.Normal);
        var ft = new FormattedText(text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, tf, 28, Brushes.Black, 1.25);

        bool needsLeader = leaderIndex >= 0;

        if (needsLeader)
        {
            double diagLen = 40 + leaderIndex * 28;
            double horizLen = ft.Width + 10;
            int direction = (leaderIndex % 2 == 0) ? 1 : -1;

            Point elbowPt, endPt;
            Rect textRect;

            for (int attempt = 0; attempt < 6; attempt++)
            {
                if (isHorizontal)
                {
                    elbowPt = new Point(mid.X + diagLen * 0.7 * direction, mid.Y + diagLen);
                    endPt = new Point(elbowPt.X + horizLen * direction, elbowPt.Y);
                    double textX = direction > 0 ? elbowPt.X : endPt.X;
                    textRect = new Rect(textX, elbowPt.Y - ft.Height - 2, ft.Width, ft.Height);
                }
                else
                {
                    elbowPt = new Point(mid.X - diagLen, mid.Y + diagLen * 0.7 * direction);
                    endPt = new Point(elbowPt.X - horizLen, elbowPt.Y);
                    textRect = new Rect(endPt.X, elbowPt.Y - ft.Height - 2, ft.Width, ft.Height);
                }

                bool overlaps = false;
                foreach (var placed in placedLeaders)
                {
                    if (textRect.IntersectsWith(placed))
                    {
                        overlaps = true;
                        break;
                    }
                }

                if (!overlaps)
                {
                    placedLeaders.Add(textRect);
                    if (isHorizontal)
                    {
                        dc.DrawLine(pen, mid, elbowPt);
                        dc.DrawLine(pen, elbowPt, endPt);
                        double textX = direction > 0 ? elbowPt.X : endPt.X;
                        dc.DrawText(ft, new Point(textX, elbowPt.Y - ft.Height - 2));
                    }
                    else
                    {
                        dc.DrawLine(pen, mid, elbowPt);
                        dc.DrawLine(pen, elbowPt, endPt);
                        dc.DrawText(ft, new Point(endPt.X, elbowPt.Y - ft.Height - 2));
                    }
                    return;
                }

                if (attempt % 2 == 0)
                    direction = -direction;
                else
                    diagLen += 25;
            }

            if (isHorizontal)
            {
                elbowPt = new Point(mid.X + diagLen * 0.7 * direction, mid.Y + diagLen);
                endPt = new Point(elbowPt.X + horizLen * direction, elbowPt.Y);
                dc.DrawLine(pen, mid, elbowPt);
                dc.DrawLine(pen, elbowPt, endPt);
                double textX = direction > 0 ? elbowPt.X : endPt.X;
                textRect = new Rect(textX, elbowPt.Y - ft.Height - 2, ft.Width, ft.Height);
                placedLeaders.Add(textRect);
                dc.DrawText(ft, new Point(textX, elbowPt.Y - ft.Height - 2));
            }
            else
            {
                elbowPt = new Point(mid.X - diagLen, mid.Y + diagLen * 0.7 * direction);
                endPt = new Point(elbowPt.X - horizLen, elbowPt.Y);
                dc.DrawLine(pen, mid, elbowPt);
                dc.DrawLine(pen, elbowPt, endPt);
                textRect = new Rect(endPt.X, elbowPt.Y - ft.Height - 2, ft.Width, ft.Height);
                placedLeaders.Add(textRect);
                dc.DrawText(ft, new Point(endPt.X, elbowPt.Y - ft.Height - 2));
            }
        }
        else
        {
            if (!isHorizontal)
            {
                dc.PushTransform(new RotateTransform(-90, mid.X, mid.Y));
                dc.DrawText(ft, new Point(mid.X - ft.Width / 2, mid.Y - ft.Height / 2 - 15));
                dc.Pop();
            }
            else
            {
                dc.DrawText(ft, new Point(mid.X - ft.Width / 2, mid.Y - ft.Height / 2 - 15));
            }
        }
    }

    private static void DrawDimensionLabel(DrawingContext dc, Pen pen, Point start, Point end, string text, bool isVertical)
    {
        dc.DrawLine(pen, start, end);

        System.Windows.Vector v = end - start;
        double len = v.Length;
        if (len < 1) return;
        v.Normalize();

        System.Windows.Vector n = new System.Windows.Vector(-v.Y, v.X);
        dc.DrawLine(pen, start - n * 10, start + n * 10);
        dc.DrawLine(pen, end - n * 10, end + n * 10);

        Point mid = new Point((start.X + end.X) / 2, (start.Y + end.Y) / 2);
        var tf = new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.SemiBold, FontStretches.Normal);
        var ft = new FormattedText(text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, tf, 28, Brushes.Black, 1.25);

        if (isVertical)
        {
            dc.PushTransform(new RotateTransform(-90, mid.X, mid.Y));
            dc.DrawText(ft, new Point(mid.X - ft.Width / 2, mid.Y - ft.Height / 2 - 15));
            dc.Pop();
        }
        else
        {
            dc.DrawText(ft, new Point(mid.X - ft.Width / 2, mid.Y - ft.Height / 2 - 15));
        }
    }
}
