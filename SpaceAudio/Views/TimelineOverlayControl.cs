using System.Globalization;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace SpaceAudio.Views;

internal sealed class TimelineOverlayControl : FrameworkElement
{
    private static readonly SolidColorBrush s_trackBrush = Frozen(Color.FromArgb(55, 200, 30, 30));
    private static readonly SolidColorBrush s_progressBrush = Frozen(Color.FromRgb(210, 35, 35));
    private static readonly SolidColorBrush s_thumbBrush = Frozen(Color.FromRgb(255, 75, 75));
    private static readonly SolidColorBrush s_tooltipBg = Frozen(Color.FromArgb(215, 12, 12, 12));
    private static readonly SolidColorBrush s_tooltipFg = Frozen(Colors.White);
    private static readonly Typeface s_typeface = new(
        new FontFamily("Consolas"), FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);

    private const double TrackHeight = 2.0;
    private const double HoverTrackHeight = 4.0;
    private const double HoverZoneHeight = 24.0;
    private const double ThumbRadius = 5.5;
    private const double TooltipH = 20.0;
    private const double TooltipPad = 7.0;
    private const double DpiScale = 96.0;
    private const double TooltipFontSize = 10.5;

    private long _currentFrame;
    private long _totalFrames;
    private int _fps;
    private bool _isHovering;
    private bool _isDragging;
    private double _dragX;
    private long _lastTooltipFrame = -1;
    private string _tooltipText = string.Empty;
    private FormattedText? _tooltipFt;

    public event Action<long>? SeekStarted;
    public event Action<long>? SeekChanged;
    public event Action<long>? SeekEnded;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Update(long currentFrame, long totalFrames, int fps, bool isPlaying)
    {
        bool changed = _currentFrame != currentFrame || _totalFrames != totalFrames || _fps != fps;
        _currentFrame = currentFrame;
        _totalFrames = totalFrames;
        _fps = fps;
        if ((changed || isPlaying) && !_isDragging)
            InvalidateVisual();
        return changed;
    }

    protected override void OnRender(DrawingContext dc)
    {
        double w = ActualWidth;
        double h = ActualHeight;
        if (w <= 0.0 || h <= 0.0) return;

        bool interactive = _isHovering || _isDragging;
        long displayFrame = _isDragging ? FrameAt(_dragX, w) : _currentFrame;
        long total = _totalFrames > 0L ? _totalFrames : 1L;
        double progress = Math.Clamp((double)displayFrame / total, 0.0, 1.0);
        double progressX = progress * w;

        double trackH = interactive ? HoverTrackHeight : TrackHeight;
        double trackY = h - trackH;

        if (interactive)
            dc.DrawRectangle(s_trackBrush, null, new Rect(0.0, h - HoverZoneHeight, w, HoverZoneHeight));

        if (progressX > 0.0)
            dc.DrawRectangle(s_progressBrush, null, new Rect(0.0, trackY, progressX, trackH));

        if (interactive)
        {
            double thumbX = _isDragging ? Math.Clamp(_dragX, 0.0, w) : progressX;
            dc.DrawEllipse(s_thumbBrush, null, new Point(thumbX, trackY), ThumbRadius, ThumbRadius);

            if (_isDragging)
                RenderTooltip(dc, displayFrame, thumbX, trackY, w);
        }
    }

    private void RenderTooltip(DrawingContext dc, long frame, double thumbX, double trackY, double totalW)
    {
        var ft = GetTooltipText(frame);
        double ttW = ft.Width + TooltipPad * 2.0;
        double ttX = Math.Clamp(thumbX - ttW * 0.5, 0.0, totalW - ttW);
        double ttY = trackY - TooltipH - 5.0;
        dc.DrawRoundedRectangle(s_tooltipBg, null, new Rect(ttX, ttY, ttW, TooltipH), 3.0, 3.0);
        dc.DrawText(ft, new Point(ttX + TooltipPad, ttY + (TooltipH - ft.Height) * 0.5));
    }

    private FormattedText GetTooltipText(long frame)
    {
        if (frame != _lastTooltipFrame)
        {
            _lastTooltipFrame = frame;
            _tooltipFt = null;
            _tooltipText = FormatTimecode(frame, Math.Max(_fps, 1));
        }

        return _tooltipFt ??= new FormattedText(
            _tooltipText,
            CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight,
            s_typeface,
            TooltipFontSize,
            s_tooltipFg,
            DpiScale);
    }

    private static string FormatTimecode(long frame, int fps)
    {
        long totalSec = frame / fps;
        int subF = (int)(frame % fps);
        int sec = (int)(totalSec % 60);
        int min = (int)(totalSec / 60 % 60);
        int hour = (int)Math.Min(totalSec / 3600, 99);

        Span<char> buf = stackalloc char[11];
        buf[0] = (char)('0' + hour / 10);
        buf[1] = (char)('0' + hour % 10);
        buf[2] = ':';
        buf[3] = (char)('0' + min / 10);
        buf[4] = (char)('0' + min % 10);
        buf[5] = ':';
        buf[6] = (char)('0' + sec / 10);
        buf[7] = (char)('0' + sec % 10);
        buf[8] = ':';
        int sf = Math.Min(subF, 99);
        buf[9] = (char)('0' + sf / 10);
        buf[10] = (char)('0' + sf % 10);

        return new string(buf);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private long FrameAt(double x, double width)
    {
        if (width <= 0.0 || _totalFrames <= 0L) return 0L;
        return (long)(Math.Clamp(x / width, 0.0, 1.0) * _totalFrames);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool InHoverZone(Point p) => p.Y >= ActualHeight - HoverZoneHeight;

    protected override HitTestResult? HitTestCore(PointHitTestParameters p) =>
        InHoverZone(p.HitPoint) ? new PointHitTestResult(this, p.HitPoint) : null;

    protected override void OnMouseEnter(MouseEventArgs e)
    {
        _isHovering = true;
        InvalidateVisual();
    }

    protected override void OnMouseLeave(MouseEventArgs e)
    {
        _isHovering = false;
        if (!_isDragging)
            InvalidateVisual();
    }

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        var pos = e.GetPosition(this);
        if (!InHoverZone(pos)) return;

        _isDragging = true;
        _dragX = pos.X;
        CaptureMouse();
        SeekStarted?.Invoke(FrameAt(_dragX, ActualWidth));
        InvalidateVisual();
        e.Handled = true;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        if (!_isDragging) return;
        _dragX = e.GetPosition(this).X;
        SeekChanged?.Invoke(FrameAt(_dragX, ActualWidth));
        InvalidateVisual();
        e.Handled = true;
    }

    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        if (!_isDragging) return;
        _isDragging = false;
        ReleaseMouseCapture();
        SeekEnded?.Invoke(FrameAt(_dragX, ActualWidth));
        _isHovering = InHoverZone(e.GetPosition(this));
        InvalidateVisual();
        e.Handled = true;
    }

    protected override Size MeasureOverride(Size s) => s;

    private static SolidColorBrush Frozen(Color c)
    {
        var b = new SolidColorBrush(c);
        b.Freeze();
        return b;
    }
}
