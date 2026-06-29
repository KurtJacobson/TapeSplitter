using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace TapeSplitterWpf;

/// <summary>
/// Custom WPF element that renders a waveform envelope with interactive split markers.
/// Left-click to add a marker, right-click a marker to remove, drag to move.
/// </summary>
public class WaveformControl : FrameworkElement
{
    // ── Data ──────────────────────────────────────────────────────────────────
    private float[]  _envelope  = [];
    private double   _totalSecs = 0;
    private readonly List<double> _markers  = [];  // seconds
    private List<(double s, double e)> _excluded = [];

    // Cached x-positions of markers from last render (for hit testing)
    private readonly List<double> _markerX = [];
    private int _dragging = -1;   // index of marker being dragged

    public WaveformControl()
    {
        ThemeManager.ThemeChanged += () => Dispatcher.Invoke(InvalidateVisual);
    }

    // ── Events ────────────────────────────────────────────────────────────────
    public event Action<IReadOnlyList<double>>? MarkersChanged;

    private static readonly Typeface RulerFont = new("Segoe UI");

    // Resolved each render from the active theme dictionary
    private Brush  BgBrush      => Res("Wave.Background");
    private Brush  WaveBrushHi  => Res("Wave.Bar.Hi");
    private Brush  WaveBrushLo  => Res("Wave.Bar.Lo");
    private Brush  GridBrush    => Res("Wave.Grid");
    private Brush  RulerBgBrush => Res("Wave.Ruler.Background");
    private Brush  RulerFgBrush => Res("Wave.Ruler.Foreground");
    private Brush  MarkerBrush  => Res("Wave.Marker");
    private Pen    MarkerPen    => new(MarkerBrush, 1.5);
    private Pen    GridPen      => new(GridBrush, 1);
    private Brush  ExcludeBrush
    {
        get
        {
            if (TryFindResource("Wave.Exclude.Color") is Color c)
                return new SolidColorBrush(c);
            return new SolidColorBrush(Color.FromArgb(150, 10, 10, 10));
        }
    }

    private Brush Res(string key) =>
        TryFindResource(key) as Brush
        ?? Application.Current.TryFindResource(key) as Brush
        ?? Brushes.Magenta; // fallback makes missing keys obvious

    // ── Public API ────────────────────────────────────────────────────────────

    public void LoadEnvelope(float[] envelope, double totalSecs)
    {
        _envelope  = envelope;
        _totalSecs = totalSecs;
        _markers.Clear();
        _excluded.Clear();
        InvalidateVisual();
    }

    public void SetMarkers(IEnumerable<double> markerSecs)
    {
        _markers.Clear();
        _markers.AddRange(markerSecs);
        _markers.Sort();
        InvalidateVisual();
    }

    public void ClearMarkers()
    {
        _markers.Clear();
        InvalidateVisual();
        MarkersChanged?.Invoke(_markers);
    }

    public void SetExcluded(IEnumerable<(double s, double e)> ranges)
    {
        _excluded = ranges.ToList();
        InvalidateVisual();
    }

    // ── Layout ────────────────────────────────────────────────────────────────

    private const double RulerH = 18;
    private double WaveH => ActualHeight - RulerH;

    private double SecsToX(double s) => _totalSecs > 0 ? s / _totalSecs * ActualWidth : 0;
    private double XToSecs(double x) => _totalSecs > 0 ? x / ActualWidth * _totalSecs : 0;

    // ── Rendering ─────────────────────────────────────────────────────────────

    protected override void OnRender(DrawingContext dc)
    {
        double w  = ActualWidth;
        double wh = WaveH;
        double mid = wh / 2;

        // Background
        dc.DrawRectangle(BgBrush, null, new Rect(0, 0, w, ActualHeight));

        if (_envelope.Length == 0 || _totalSecs <= 0)
        {
            var ft = new FormattedText("Load an MP3 to see the waveform",
                System.Globalization.CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
                RulerFont, 12, RulerFgBrush, 96);
            dc.DrawText(ft, new Point(16, mid - ft.Height / 2));
            DrawRuler(dc, w, wh);
            return;
        }

        // Grid lines every 30 s
        for (int s = 0; s <= (int)_totalSecs; s += 30)
            dc.DrawLine(GridPen, new Point(SecsToX(s), 0), new Point(SecsToX(s), wh));

        // Waveform bars
        double colsPerPixel = _envelope.Length / w;
        for (int px = 0; px < (int)w; px++)
        {
            int c0 = (int)(px * colsPerPixel);
            int c1 = Math.Min(_envelope.Length, (int)((px + 1) * colsPerPixel) + 1);
            if (c0 >= _envelope.Length) break;
            float peak = 0;
            for (int c = c0; c < c1; c++) peak = Math.Max(peak, _envelope[c]);
            double barH = Math.Max(1, peak * mid);
            dc.DrawRectangle(WaveBrushHi, null, new Rect(px, mid - barH, 1, barH));
            dc.DrawRectangle(WaveBrushLo, null, new Rect(px, mid, 1, barH));
        }

        // Excluded (unchecked) overlays
        foreach (var (s, e) in _excluded)
        {
            double x1 = Math.Max(0, SecsToX(s));
            double x2 = Math.Min(w, SecsToX(e));
            if (x2 > x1) dc.DrawRectangle(ExcludeBrush, null, new Rect(x1, 0, x2 - x1, wh));
        }

        DrawRuler(dc, w, wh);

        // Markers — rebuild x cache
        _markerX.Clear();
        for (int i = 0; i < _markers.Count; i++)
        {
            double x = SecsToX(_markers[i]);
            _markerX.Add(x);
            dc.DrawLine(MarkerPen, new Point(x, 0), new Point(x, wh));
            // Triangle handle
            var tri = new StreamGeometry();
            using (var ctx = tri.Open())
            {
                ctx.BeginFigure(new Point(x - 6, 0), true, true);
                ctx.LineTo(new Point(x + 6, 0), true, false);
                ctx.LineTo(new Point(x, 9), true, false);
            }
            tri.Freeze();
            dc.DrawGeometry(MarkerBrush, null, tri);
            // Number
            var num = new FormattedText($"{i + 1}",
                System.Globalization.CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
                RulerFont, 9, MarkerBrush, 96);
            dc.DrawText(num, new Point(x + 4, 2));
        }
    }

    private void DrawRuler(DrawingContext dc, double w, double waveH)
    {
        dc.DrawRectangle(RulerBgBrush, null, new Rect(0, waveH, w, RulerH));
        if (_totalSecs <= 0) return;
        for (int s = 0; s <= (int)_totalSecs; s += 15)
        {
            double x = SecsToX(s);
            if (x < 0 || x > w) continue;
            dc.DrawLine(new Pen(RulerFgBrush, 1), new Point(x, waveH), new Point(x, waveH + 4));
            string label = $"{s / 60}:{s % 60:D2}";
            var ft = new FormattedText(label,
                System.Globalization.CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
                RulerFont, 9, RulerFgBrush, 96);
            dc.DrawText(ft, new Point(x + 2, waveH + 3));
        }
    }

    // ── Mouse interaction ─────────────────────────────────────────────────────

    private int HitMarker(double x)
    {
        for (int i = 0; i < _markerX.Count; i++)
            if (Math.Abs(_markerX[i] - x) <= 8) return i;
        return -1;
    }

    protected override void OnMouseDown(MouseButtonEventArgs e)
    {
        if (_envelope.Length == 0) return;
        var pos = e.GetPosition(this);
        if (pos.Y > WaveH) return;

        if (e.ChangedButton == MouseButton.Left)
        {
            int hit = HitMarker(pos.X);
            if (hit >= 0)
            {
                _dragging = hit;
                CaptureMouse();
            }
            else
            {
                double secs = XToSecs(pos.X);
                if (secs > 0.5 && secs < _totalSecs - 0.5)
                {
                    _markers.Add(secs);
                    _markers.Sort();
                    InvalidateVisual();
                    MarkersChanged?.Invoke(_markers);
                }
            }
        }
        else if (e.ChangedButton == MouseButton.Right)
        {
            int hit = HitMarker(pos.X);
            if (hit >= 0)
            {
                _markers.RemoveAt(hit);
                InvalidateVisual();
                MarkersChanged?.Invoke(_markers);
            }
        }
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        if (_dragging < 0) { Cursor = HitMarker(e.GetPosition(this).X) >= 0 ? Cursors.SizeWE : Cursors.Hand; return; }
        double secs = Math.Clamp(XToSecs(e.GetPosition(this).X), 0.5, _totalSecs - 0.5);
        _markers[_dragging] = secs;
        InvalidateVisual();
    }

    protected override void OnMouseUp(MouseButtonEventArgs e)
    {
        if (_dragging < 0) return;
        _markers.Sort();
        _dragging = -1;
        ReleaseMouseCapture();
        InvalidateVisual();
        MarkersChanged?.Invoke(_markers);
    }

    protected override void OnMouseLeave(MouseEventArgs e) { Cursor = Cursors.Arrow; }
}
