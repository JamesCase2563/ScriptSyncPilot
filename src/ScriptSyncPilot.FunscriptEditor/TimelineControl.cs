using ScriptSyncPilot.Common;

namespace ScriptSyncPilot.FunscriptEditor;

/// <summary>
/// A zoomable, scrollable track showing a funscript's actions as a draggable
/// point-and-line graph (time on X, position 0-100 on Y, 0 at the bottom).
/// Dragging a point vertically changes its position; dragging it horizontally
/// changes its time, clamped so it cannot cross its neighbors. Double-click on
/// empty track space adds a point; Delete removes the selected one. Clicking
/// or dragging on empty track space scrubs the playhead and raises
/// <see cref="SeekRequested"/>.
/// </summary>
public sealed class TimelineControl : Control
{
    private const int PointRadius = 5;
    private const int HitRadius = 8;
    private const int TopMargin = 10;
    private const int BottomMargin = 24; // room for the scrollbar
    private const double MinPixelsPerSecond = 5;
    private const double MaxPixelsPerSecond = 2000;

    private readonly HScrollBar _scrollBar = new() { Dock = DockStyle.Bottom, Height = 17 };

    private Funscript _script = new();
    private double _durationSeconds = 60;
    private double _pixelsPerSecond = 100;
    private double _viewStartSeconds;
    private double _playheadSeconds;

    private int? _draggingIndex;
    private int? _selectedIndex;
    private bool _scrubbing;
    private bool _suppressScrollEvent;

    public event Action<double>? SeekRequested;
    public event EventHandler? ScriptChanged;

    [System.ComponentModel.Browsable(false)]
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public double PixelsPerSecond
    {
        get => _pixelsPerSecond;
        set => SetZoom(value, CenterSeconds());
    }

    public TimelineControl()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        TabStop = true;
        BackColor = Color.FromArgb(30, 30, 30);
        Controls.Add(_scrollBar);
        _scrollBar.Scroll += (_, _) =>
        {
            if (_suppressScrollEvent) return;
            _viewStartSeconds = _scrollBar.Value;
            Invalidate();
        };
    }

    public void LoadScript(Funscript script, double durationSeconds)
    {
        _script = script;
        _durationSeconds = Math.Max(1, durationSeconds);
        _selectedIndex = null;
        _draggingIndex = null;
        UpdateScrollBar();
        Invalidate();
    }

    public void SetPlayhead(double seconds)
    {
        _playheadSeconds = seconds;
        Invalidate();
    }

    public void UpdateDuration(double durationSeconds)
    {
        _durationSeconds = Math.Max(1, durationSeconds);
        ClampViewStart();
        UpdateScrollBar();
        Invalidate();
    }

    private double CenterSeconds() => _viewStartSeconds + ViewportSeconds() / 2;

    private double ViewportSeconds() => Math.Max(1, ClientSize.Width) / _pixelsPerSecond;

    private void SetZoom(double pixelsPerSecond, double centerSeconds)
    {
        _pixelsPerSecond = Math.Clamp(pixelsPerSecond, MinPixelsPerSecond, MaxPixelsPerSecond);
        _viewStartSeconds = centerSeconds - ViewportSeconds() / 2;
        ClampViewStart();
        UpdateScrollBar();
        Invalidate();
    }

    private void ClampViewStart()
    {
        var maxStart = Math.Max(0, _durationSeconds - ViewportSeconds());
        _viewStartSeconds = Math.Clamp(_viewStartSeconds, 0, maxStart);
    }

    private void UpdateScrollBar()
    {
        _suppressScrollEvent = true;
        var viewport = ViewportSeconds();
        _scrollBar.Minimum = 0;
        _scrollBar.Maximum = (int)Math.Ceiling(_durationSeconds);
        _scrollBar.LargeChange = Math.Max(1, (int)Math.Ceiling(viewport));
        _scrollBar.SmallChange = Math.Max(1, (int)Math.Ceiling(viewport / 20));
        _scrollBar.Value = Math.Clamp((int)_viewStartSeconds, 0, Math.Max(0, _scrollBar.Maximum - _scrollBar.LargeChange));
        _suppressScrollEvent = false;
    }

    private int TrackTop => TopMargin;
    private int TrackBottom => ClientSize.Height - BottomMargin - TopMargin;
    private int TrackHeight => Math.Max(1, TrackBottom - TrackTop);

    private double TimeToX(double seconds) => (seconds - _viewStartSeconds) * _pixelsPerSecond;
    private double XToTime(double x) => _viewStartSeconds + x / _pixelsPerSecond;
    private double PosToY(int pos) => TrackBottom - pos / 100.0 * TrackHeight;
    private int YToPos(double y) => (int)Math.Round(Math.Clamp((TrackBottom - y) / TrackHeight * 100.0, 0, 100));

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

        using (var gridPen = new Pen(Color.FromArgb(55, 55, 55)))
        {
            for (var p = 0; p <= 100; p += 25)
            {
                var y = (float)PosToY(p);
                g.DrawLine(gridPen, 0, y, ClientSize.Width, y);
            }
        }

        var actions = _script.Actions;
        if (actions.Count > 0)
        {
            using var linePen = new Pen(Color.FromArgb(90, 170, 255), 2);
            PointF? prev = null;
            for (var i = 0; i < actions.Count; i++)
            {
                var x = (float)TimeToX(actions[i].At / 1000.0);
                if (x < -50 || x > ClientSize.Width + 50)
                {
                    prev = new PointF(x, (float)PosToY(actions[i].Pos));
                    continue;
                }
                var pt = new PointF(x, (float)PosToY(actions[i].Pos));
                if (prev is { } p0) g.DrawLine(linePen, p0, pt);
                prev = pt;
            }

            for (var i = 0; i < actions.Count; i++)
            {
                var x = TimeToX(actions[i].At / 1000.0);
                if (x < -PointRadius || x > ClientSize.Width + PointRadius) continue;
                var y = PosToY(actions[i].Pos);
                var selected = i == _selectedIndex;
                using var brush = new SolidBrush(selected ? Color.Orange : Color.White);
                var r = selected ? PointRadius + 1 : PointRadius;
                g.FillEllipse(brush, (float)(x - r), (float)(y - r), r * 2, r * 2);
            }
        }

        using (var playPen = new Pen(Color.Red, 1.5f))
        {
            var x = (float)TimeToX(_playheadSeconds);
            if (x >= 0 && x <= ClientSize.Width)
                g.DrawLine(playPen, x, 0, x, TrackBottom);
        }

        base.OnPaint(e);
    }

    protected override void OnMouseWheel(MouseEventArgs e)
    {
        var factor = Math.Pow(1.15, e.Delta / 120.0);
        SetZoom(_pixelsPerSecond * factor, XToTime(e.X));
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        Focus();
        if (e.Button != MouseButtons.Left) return;

        var hit = HitTest(e.Location);
        if (hit is { } index)
        {
            _selectedIndex = index;
            _draggingIndex = index;
        }
        else
        {
            _selectedIndex = null;
            _scrubbing = true;
            Seek(e.X);
        }
        Invalidate();
    }

    protected override void OnMouseDoubleClick(MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Left) return;
        if (HitTest(e.Location) is not null) return;

        var at = (int)Math.Round(Math.Clamp(XToTime(e.X), 0, _durationSeconds) * 1000);
        var pos = YToPos(e.Y);
        _script.AddOrReplace(new FunscriptAction(at, pos));
        ScriptChanged?.Invoke(this, EventArgs.Empty);
        Invalidate();
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        if (_draggingIndex is { } index)
        {
            var actions = _script.Actions;
            var minAt = index > 0 ? actions[index - 1].At + 1 : 0;
            var maxAt = index < actions.Count - 1 ? actions[index + 1].At - 1 : (int)(_durationSeconds * 1000);
            var at = Math.Clamp((int)Math.Round(XToTime(e.X) * 1000), Math.Min(minAt, maxAt), Math.Max(minAt, maxAt));
            var pos = YToPos(e.Y);
            var newIndex = _script.UpdateAt(index, new FunscriptAction(at, pos));
            _draggingIndex = newIndex;
            _selectedIndex = newIndex;
            Invalidate();
        }
        else if (_scrubbing)
        {
            Seek(e.X);
        }
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        if (_draggingIndex is not null) ScriptChanged?.Invoke(this, EventArgs.Empty);
        _draggingIndex = null;
        _scrubbing = false;
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Delete && _selectedIndex is { } index)
        {
            _script.RemoveAt(index);
            _selectedIndex = null;
            ScriptChanged?.Invoke(this, EventArgs.Empty);
            Invalidate();
            e.Handled = true;
        }
    }

    protected override bool IsInputKey(Keys keyData) => keyData == Keys.Delete || base.IsInputKey(keyData);

    private void Seek(int x)
    {
        var seconds = Math.Clamp(XToTime(x), 0, _durationSeconds);
        _playheadSeconds = seconds;
        SeekRequested?.Invoke(seconds);
        Invalidate();
    }

    private int? HitTest(Point location)
    {
        var actions = _script.Actions;
        for (var i = 0; i < actions.Count; i++)
        {
            var x = TimeToX(actions[i].At / 1000.0);
            if (x < -HitRadius || x > ClientSize.Width + HitRadius) continue;
            var y = PosToY(actions[i].Pos);
            var dx = x - location.X;
            var dy = y - location.Y;
            if (dx * dx + dy * dy <= HitRadius * HitRadius) return i;
        }
        return null;
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        ClampViewStart();
        UpdateScrollBar();
    }
}
