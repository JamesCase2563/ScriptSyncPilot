using ScriptSyncPilot.Common;

namespace ScriptSyncPilot.FunscriptEditor;

/// <summary>
/// A zoomable, scrollable track showing a funscript's actions as a draggable
/// point-and-line graph (time on X, position 0-100 on Y, 0 at the bottom).
/// Dragging a point vertically changes its position; dragging it horizontally
/// changes its time, clamped so it cannot cross its neighbors. Double-click on
/// empty track space adds a point; Delete removes the selected one. Clicking
/// or dragging on empty track space scrubs the playhead and raises
/// <see cref="SeekRequested"/>. Clicking the value bar to the right inserts a
/// point at the current playhead time, at the clicked value.
/// </summary>
public sealed class TimelineControl : Control
{
    private const int PointRadius = 5;
    private const int HitRadius = 8;
    private const int TopMargin = 10;
    private const int BottomMargin = 24; // room for the scrollbar
    private const int ValueBarWidth = 40;
    private const double AbsoluteMaxPixelsPerSecond = 2000;
    private const double FallbackDurationSeconds = 60;

    private readonly HScrollBar _scrollBar = new() { Dock = DockStyle.Bottom, Height = 17 };

    private Funscript _script = new();
    private double _mediaDurationSeconds;
    private double _pixelsPerSecond = 100;
    private double _viewStartSeconds;
    private double _playheadSeconds;

    private int? _draggingIndex;
    private int? _selectedIndex;
    private bool _scrubbing;
    private bool _suppressScrollEvent;
    private int? _hoverBarPos;

    public event Action<double>? SeekRequested;
    public event EventHandler? ScriptChanged;
    public event EventHandler? ZoomChanged;

    [System.ComponentModel.Browsable(false)]
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public double PixelsPerSecond
    {
        get => _pixelsPerSecond;
        set => SetZoom(value, _playheadSeconds);
    }

    /// <summary>
    /// A script normally ends before its video does, so the axis spans whichever is longer.
    /// Taking the max rather than the media duration also keeps a script that outruns its
    /// video (or has no video at all) fully reachable instead of clipped off the end.
    /// </summary>
    [System.ComponentModel.Browsable(false)]
    public double DurationSeconds
    {
        get
        {
            var scriptEnd = _script.Actions.Count > 0 ? _script.Actions[^1].At / 1000.0 : 0;
            var longest = Math.Max(scriptEnd, _mediaDurationSeconds);
            return longest > 0 ? longest : FallbackDurationSeconds;
        }
    }

    /// <summary>Zoom at which the whole duration fits the track area; zooming out further would only add empty space.</summary>
    [System.ComponentModel.Browsable(false)]
    public double MinPixelsPerSecond => Math.Max(0.001, TrackAreaWidth / DurationSeconds);

    [System.ComponentModel.Browsable(false)]
    public double MaxPixelsPerSecond => Math.Max(MinPixelsPerSecond, AbsoluteMaxPixelsPerSecond);

    public void ZoomToFit() => ZoomAt(MinPixelsPerSecond, 0, 0);

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

    public void LoadScript(Funscript script)
    {
        _script = script;
        _selectedIndex = null;
        _draggingIndex = null;
        ReclampZoom();
    }

    public void SetPlayhead(double seconds)
    {
        _playheadSeconds = seconds;
        Invalidate();
    }

    public void UpdateDuration(double mediaDurationSeconds)
    {
        _mediaDurationSeconds = mediaDurationSeconds > 0 ? mediaDurationSeconds : 0;
        ReclampZoom();
    }

    /// <summary>Re-applies the zoom limits after anything that changes the duration or the track width.</summary>
    private void ReclampZoom()
    {
        _pixelsPerSecond = Math.Clamp(_pixelsPerSecond, MinPixelsPerSecond, MaxPixelsPerSecond);
        ClampViewStart();
        UpdateScrollBar();
        ZoomChanged?.Invoke(this, EventArgs.Empty);
        Invalidate();
    }

    private double CenterSeconds() => _viewStartSeconds + ViewportSeconds() / 2;

    private double ViewportSeconds() => Math.Max(1, TrackAreaWidth) / _pixelsPerSecond;

    private void SetZoom(double pixelsPerSecond, double centerSeconds)
    {
        _pixelsPerSecond = Math.Clamp(pixelsPerSecond, MinPixelsPerSecond, MaxPixelsPerSecond);
        _viewStartSeconds = centerSeconds - ViewportSeconds() / 2;
        ClampViewStart();
        UpdateScrollBar();
        ZoomChanged?.Invoke(this, EventArgs.Empty);
        Invalidate();
    }

    // Keeps anchorSeconds under anchorX, so whatever is beneath the cursor stays beneath it while zooming.
    private void ZoomAt(double pixelsPerSecond, double anchorSeconds, double anchorX)
    {
        _pixelsPerSecond = Math.Clamp(pixelsPerSecond, MinPixelsPerSecond, MaxPixelsPerSecond);
        _viewStartSeconds = anchorSeconds - anchorX / _pixelsPerSecond;
        ClampViewStart();
        UpdateScrollBar();
        ZoomChanged?.Invoke(this, EventArgs.Empty);
        Invalidate();
    }

    /// <summary>Wheel zoom anchored at a client-space X. Public because WinForms sends wheel input to the focused control, not the hovered one.</summary>
    public void ZoomByWheel(int delta, int clientX)
    {
        var anchorX = Math.Clamp(clientX, 0, BarLeft);
        ZoomAt(_pixelsPerSecond * Math.Pow(1.15, delta / 120.0), XToTime(anchorX), anchorX);
    }

    private void ClampViewStart()
    {
        var maxStart = Math.Max(0, DurationSeconds - ViewportSeconds());
        _viewStartSeconds = Math.Clamp(_viewStartSeconds, 0, maxStart);
    }

    private void UpdateScrollBar()
    {
        _suppressScrollEvent = true;
        var viewport = ViewportSeconds();
        _scrollBar.Minimum = 0;
        _scrollBar.Maximum = (int)Math.Ceiling(DurationSeconds);
        _scrollBar.LargeChange = Math.Max(1, (int)Math.Ceiling(viewport));
        _scrollBar.SmallChange = Math.Max(1, (int)Math.Ceiling(viewport / 20));
        _scrollBar.Value = Math.Clamp((int)_viewStartSeconds, 0, Math.Max(0, _scrollBar.Maximum - _scrollBar.LargeChange));
        _suppressScrollEvent = false;
    }

    private int TrackTop => TopMargin;
    private int TrackBottom => ClientSize.Height - BottomMargin - TopMargin;
    private int TrackHeight => Math.Max(1, TrackBottom - TrackTop);
    private int TrackAreaWidth => Math.Max(1, ClientSize.Width - ValueBarWidth);
    private int BarLeft => TrackAreaWidth;

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
                g.DrawLine(gridPen, 0, y, BarLeft, y);
            }
        }

        var actions = _script.Actions;
        if (actions.Count > 0)
        {
            using var linePen = new Pen(Color.FromArgb(90, 170, 255), 2);
            var clipLeft = -50f;
            var clipRight = BarLeft + 50f;
            PointF? prev = null;
            for (var i = 0; i < actions.Count; i++)
            {
                var pt = new PointF((float)TimeToX(actions[i].At / 1000.0), (float)PosToY(actions[i].Pos));
                // Draw whenever any part of the segment can be visible, otherwise a segment running to an
                // off-screen neighbour is dropped and the line appears to stop at the window edge.
                if (prev is { } p0 && Math.Max(p0.X, pt.X) >= clipLeft && Math.Min(p0.X, pt.X) <= clipRight)
                    g.DrawLine(linePen, ClipX(p0, pt, clipLeft, clipRight), ClipX(pt, p0, clipLeft, clipRight));
                prev = pt;
            }

            for (var i = 0; i < actions.Count; i++)
            {
                var x = TimeToX(actions[i].At / 1000.0);
                if (x < -PointRadius || x > BarLeft + PointRadius) continue;
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
            if (x >= 0 && x <= BarLeft)
                g.DrawLine(playPen, x, 0, x, TrackBottom);
        }

        PaintValueBar(g);

        base.OnPaint(e);
    }

    // GDI+ degrades on extreme coordinates, which a far-off neighbour produces at high zoom, so trim to the visible span.
    private static PointF ClipX(PointF p, PointF other, float left, float right)
    {
        if (p.X >= left && p.X <= right) return p;
        var targetX = p.X < left ? left : right;
        var dx = other.X - p.X;
        if (Math.Abs(dx) < 0.0001f) return new PointF(targetX, p.Y);
        var t = Math.Clamp((targetX - p.X) / dx, 0f, 1f);
        return new PointF(targetX, p.Y + (other.Y - p.Y) * t);
    }

    private void PaintValueBar(Graphics g)
    {
        var barRect = new Rectangle(BarLeft, TrackTop, ValueBarWidth, TrackHeight);
        using (var gradient = new System.Drawing.Drawing2D.LinearGradientBrush(
                   barRect, Color.FromArgb(230, 230, 230), Color.FromArgb(40, 40, 40), System.Drawing.Drawing2D.LinearGradientMode.Vertical))
            g.FillRectangle(gradient, barRect);

        using (var tickPen = new Pen(Color.FromArgb(90, 90, 90)))
        using (var font = new Font(Font.FontFamily, 7f))
        using (var textBrush = new SolidBrush(Color.Black))
        {
            for (var p = 0; p <= 100; p += 25)
            {
                var y = (float)PosToY(p);
                g.DrawLine(tickPen, barRect.Left, y, barRect.Left + 6, y);
                g.DrawString(p.ToString(), font, textBrush, barRect.Left + 7, y - 6);
            }
        }

        if (_hoverBarPos is { } hoverPos)
        {
            var y = (float)PosToY(hoverPos);
            using var hoverPen = new Pen(Color.Orange, 2);
            g.DrawLine(hoverPen, barRect.Left, y, barRect.Right, y);
        }
    }

    protected override void OnMouseWheel(MouseEventArgs e) => ZoomByWheel(e.Delta, e.X);

    protected override void OnMouseDown(MouseEventArgs e)
    {
        Focus();
        if (e.Button != MouseButtons.Left) return;

        if (e.X >= BarLeft)
        {
            InsertAtPlayhead(e.Y);
            return;
        }

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
        if (e.X >= BarLeft) return;
        if (HitTest(e.Location) is not null) return;

        var at = (int)Math.Round(Math.Clamp(XToTime(e.X), 0, DurationSeconds) * 1000);
        var pos = YToPos(e.Y);
        _script.AddOrReplace(new FunscriptAction(at, pos));
        ScriptChanged?.Invoke(this, EventArgs.Empty);
        Invalidate();
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        if (e.X >= BarLeft && _draggingIndex is null && !_scrubbing)
        {
            _hoverBarPos = YToPos(e.Y);
            Cursor = Cursors.Hand;
            Invalidate();
            return;
        }
        if (_hoverBarPos is not null) { _hoverBarPos = null; Cursor = Cursors.Default; Invalidate(); }

        if (_draggingIndex is { } index)
        {
            var actions = _script.Actions;
            var minAt = index > 0 ? actions[index - 1].At + 1 : 0;
            var maxAt = index < actions.Count - 1 ? actions[index + 1].At - 1 : (int)(DurationSeconds * 1000);
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

    protected override void OnMouseLeave(EventArgs e)
    {
        if (_hoverBarPos is not null)
        {
            _hoverBarPos = null;
            Cursor = Cursors.Default;
            Invalidate();
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
        var seconds = Math.Clamp(XToTime(x), 0, DurationSeconds);
        _playheadSeconds = seconds;
        SeekRequested?.Invoke(seconds);
        Invalidate();
    }

    private void InsertAtPlayhead(int y)
    {
        var pos = YToPos(y);
        var at = (int)Math.Round(Math.Clamp(_playheadSeconds, 0, DurationSeconds) * 1000);
        _script.AddOrReplace(new FunscriptAction(at, pos));
        var actions = _script.Actions;
        for (var i = 0; i < actions.Count; i++)
        {
            if (actions[i].At != at) continue;
            _selectedIndex = i;
            break;
        }
        ScriptChanged?.Invoke(this, EventArgs.Empty);
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
        ReclampZoom();
    }
}
