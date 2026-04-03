using SpaceAudio.Models;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace SpaceAudio.Rendering;

public static class BlueprintSerializer
{
    public static uint ComputeFnv1a(byte[] data)
    {
        uint hash = 2166136261;
        foreach (var b in data)
        {
            hash ^= b;
            hash *= 16777619;
        }
        return hash;
    }

    public static void DrawVisualBarcode(DrawingContext dc, RoomGeometry geo, double a4Width, double margin)
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
        int startX = (int)(margin * 2 + (a4Width - margin * 3) / 2.0) + 10;
        int startY = 80;
        int cols = (int)((a4Width - 80 - startX) / cellSize);

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

    public static RoomGeometry? ExtractVisualBarcode(string filepath, int a4Width, int a4Height, double margin)
    {
        try
        {
            var bmp = new BitmapImage();
            bmp.BeginInit();
            bmp.UriSource = new Uri(filepath);
            bmp.CacheOption = BitmapCacheOption.OnLoad;
            bmp.EndInit();

            if (bmp.PixelWidth != a4Width || bmp.PixelHeight != a4Height) return null;

            int cellSize = 4;
            int startX = (int)(margin * 2 + (a4Width - margin * 3) / 2.0) + 10;
            int startY = 80;
            int cols = (a4Width - 80 - startX) / cellSize;

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
            if (startX + cols * cellSize > a4Width || startY + (totalBitsToRead / cols + 1) * cellSize > a4Height)
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
}
