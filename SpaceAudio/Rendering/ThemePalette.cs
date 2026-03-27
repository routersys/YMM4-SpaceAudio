using System.Windows.Media;

namespace SpaceAudio.Rendering;

internal sealed class ThemePalette
{
    public SolidColorBrush GridLine { get; }
    public SolidColorBrush GridText { get; }
    public SolidColorBrush WallFill { get; }
    public SolidColorBrush WallStroke { get; }
    public SolidColorBrush SourceMarker { get; }
    public SolidColorBrush ListenerMarker { get; }
    public SolidColorBrush ReflectionPath { get; }
    public SolidColorBrush InfoText { get; }
    public SolidColorBrush FloorFill { get; }

    private ThemePalette(
        Color gridLine, Color gridText, Color wallFill, Color wallStroke,
        Color source, Color listener, Color reflection, Color info, Color floor)
    {
        GridLine = Freeze(gridLine);
        GridText = Freeze(gridText);
        WallFill = Freeze(wallFill);
        WallStroke = Freeze(wallStroke);
        SourceMarker = Freeze(source);
        ListenerMarker = Freeze(listener);
        ReflectionPath = Freeze(reflection);
        InfoText = Freeze(info);
        FloorFill = Freeze(floor);
    }

    public static ThemePalette Detect(Color background)
    {
        bool isDark = (background.R * 0.299 + background.G * 0.587 + background.B * 0.114) / 255.0 < 0.5;
        return isDark ? CreateDark() : CreateLight();
    }

    private static ThemePalette CreateDark() => new(
        gridLine: Color.FromArgb(40, 255, 255, 255),
        gridText: Color.FromArgb(100, 255, 255, 255),
        wallFill: Color.FromArgb(35, 100, 180, 255),
        wallStroke: Color.FromArgb(120, 100, 200, 255),
        source: Color.FromRgb(255, 100, 50),
        listener: Color.FromRgb(50, 200, 100),
        reflection: Color.FromArgb(60, 255, 200, 50),
        info: Color.FromArgb(180, 255, 255, 255),
        floor: Color.FromArgb(20, 150, 150, 150));

    private static ThemePalette CreateLight() => new(
        gridLine: Color.FromArgb(40, 0, 0, 0),
        gridText: Color.FromArgb(100, 0, 0, 0),
        wallFill: Color.FromArgb(25, 0, 80, 160),
        wallStroke: Color.FromArgb(100, 0, 80, 160),
        source: Color.FromRgb(220, 60, 20),
        listener: Color.FromRgb(20, 150, 60),
        reflection: Color.FromArgb(50, 200, 150, 20),
        info: Color.FromArgb(180, 0, 0, 0),
        floor: Color.FromArgb(15, 100, 100, 100));

    private static SolidColorBrush Freeze(Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }
}
