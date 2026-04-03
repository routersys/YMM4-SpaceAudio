using SpaceAudio.Rendering;
using System.IO;
using System.Text.Json;
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
            BlueprintRenderer.RenderToContext(dc, geo, A4Width, A4Height, Margin);
            BlueprintSerializer.DrawVisualBarcode(dc, geo, A4Width, Margin);
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
            return BlueprintSerializer.ExtractVisualBarcode(filepath, A4Width, A4Height, Margin);
        }
        catch
        {
            return BlueprintSerializer.ExtractVisualBarcode(filepath, A4Width, A4Height, Margin);
        }
    }
}
