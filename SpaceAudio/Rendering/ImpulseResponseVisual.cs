using System.Windows;
using System.Windows.Media;

namespace SpaceAudio.Rendering;

internal sealed class ImpulseResponseVisual : FrameworkElement
{
    private readonly VisualCollection _children;
    private readonly DrawingVisual _waveformVisual = new();

    public ImpulseResponseVisual()
    {
        _children = new VisualCollection(this) { _waveformVisual };
    }

    protected override int VisualChildrenCount => _children.Count;
    protected override Visual GetVisualChild(int index) => _children[index];

    public void Render(float[] stereoIR, double width, double height, ThemePalette palette)
    {
        if (stereoIR is null || stereoIR.Length == 0 || width <= 0 || height <= 0) return;

        using var dc = _waveformVisual.RenderOpen();
        int frames = stereoIR.Length / 2;
        double halfH = height * 0.5;
        int step = Math.Max(1, frames / (int)width);

        var pen = new Pen(palette.WallStroke, 1.0);
        pen.Freeze();

        double prevX = 0, prevY = halfH;
        for (int px = 0; px < (int)width; px++)
        {
            int sampleIdx = px * frames / (int)width;
            float mono = (stereoIR[sampleIdx * 2] + stereoIR[sampleIdx * 2 + 1]) * 0.5f;
            double y = halfH - mono * halfH * 0.9;
            dc.DrawLine(pen, new Point(prevX, prevY), new Point(px, y));
            prevX = px;
            prevY = y;
        }
    }
}
