using ScriptSyncPilot.Common;
using System.Diagnostics;

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
    private const int FocusBorderPadding = 3;
    private const int ScrollbarUnitsPerSecond = 1000;
    private const double AbsoluteMaxPixelsPerSecond = 2000;
    private const double FallbackDurationSeconds = 60;

    private readonly HScrollBar _scrollBar = new() { Height = 17 };

    private Funscript _script = new();
    private GeneratedPattern? _patternPreview;
    private int? _patternRangeStartMs;
    private int? _patternRangeEndMs;
    private bool _patternRangeInvalid;
    private double _mediaDurationSeconds;
    private double _pixelsPerSecond = 100;
    private double _viewStartSeconds;
    private double _playheadSeconds;

    private int? _draggingIndex;
    private FunscriptAction[]? _dragBefore;
    private int? _selectedIndex;
    private bool _scrubbing;
    private int? _panStartX;
    private double _panStartSeconds;
    private MouseButtons? _panButton;
    private bool _suppressPlaybackFollowUntilVisible;
    private bool _suppressScrollEvent;
    private int? _hoverBarPos;

    public event Action<double>? SeekRequested;
    public event EventHandler? PlayPauseRequested;
    public event EventHandler? ScriptChanged;
    public event EventHandler? ZoomChanged;

    [System.ComponentModel.Browsable(false)]
    public double PlayheadSeconds => _playheadSeconds;

    /// <summary>Undo/redo history of the loaded script; reset by <see cref="LoadScript"/>.</summary>
    [System.ComponentModel.Browsable(false)]
    public ActionHistory History { get; } = new();

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
            var previewEnd = _patternPreview?.EndMs / 1000.0 ?? 0;
            var rangeEnd = Math.Max(_patternRangeStartMs ?? 0, _patternRangeEndMs ?? 0) / 1000.0;
            var longest = Math.Max(Math.Max(Math.Max(scriptEnd, _mediaDurationSeconds), previewEnd), rangeEnd);
            var minimumDuration = _mediaDurationSeconds > 0 ? 0 : FallbackDurationSeconds;
            return Math.Max(longest, minimumDuration);
        }
    }

    /// <summary>Zoom at which the whole duration fits the track area; zooming out further would only add empty space.</summary>
    [System.ComponentModel.Browsable(false)]
    public double MinPixelsPerSecond => Math.Max(0.001, TrackAreaWidth / DurationSeconds);

    [System.ComponentModel.Browsable(false)]
    public double MaxPixelsPerSecond => Math.Max(MinPixelsPerSecond, AbsoluteMaxPixelsPerSecond);

    public void ZoomToFit() => ZoomAt(MinPixelsPerSecond, 0, ContentLeft);

    public TimelineControl()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        TabStop = true;
        BackColor = Color.FromArgb(30, 30, 30);
        Padding = new Padding(FocusBorderPadding);
        ScriptChanged += (_, _) => ReclampZoom();
        Controls.Add(_scrollBar);
        LayoutScrollBar();
        _scrollBar.Scroll += (_, _) =>
        {
            if (_suppressScrollEvent) return;
            _viewStartSeconds = _scrollBar.Value / (double)ScrollbarUnitsPerSecond;
            Invalidate();
        };
    }

    public void LoadScript(Funscript script)
    {
        PanEnd(suppressFollow: false);
        _suppressPlaybackFollowUntilVisible = false;
        _script = script;
        _selectedIndex = null;
        _draggingIndex = null;
        _dragBefore = null;
        History.Reset();
        ReclampZoom();
    }

    /// <summary>Replaces all actions as one history entry. Returns false for a no-op.</summary>
    public bool ReplaceActions(IEnumerable<FunscriptAction> actions, int preferredSelectionAt)
    {
        FinishDrag();
        var before = _script.Actions.ToArray();
        if (!_script.ReplaceActionsAtomically(actions)) return false;
        var edit = ActionEdit.Between(before, _script.Actions);
        History.Record(edit);

        _scrubbing = false;
        _selectedIndex = edit is { Inserted.Count: > 0 } ? edit.Index : null;

        ScriptChanged?.Invoke(this, EventArgs.Empty);
        Invalidate();
        return true;
    }

    public bool Undo() => ApplyHistory(History.Undo);

    public bool Redo() => ApplyHistory(History.Redo);

    private bool ApplyHistory(Func<Funscript, ActionEdit?> step)
    {
        FinishDrag();
        if (step(_script) is not { } applied) return false;
        _scrubbing = false;
        _selectedIndex = applied.Inserted.Count > 0 ? applied.Index : null;
        ScriptChanged?.Invoke(this, EventArgs.Empty);
        Invalidate();
        return true;
    }

    /// <summary>Applies a small edit and records the resulting delta; raises ScriptChanged only on a real change.</summary>
    private bool Commit(Action mutate)
    {
        FinishDrag();
        var before = _script.Actions.ToArray();
        mutate();
        if (!History.Record(ActionEdit.Between(before, _script.Actions))) return false;
        ScriptChanged?.Invoke(this, EventArgs.Empty);
        return true;
    }

    /// <summary>Records an in-progress drag as one history entry.</summary>
    private void FinishDrag()
    {
        _draggingIndex = null;
        if (_dragBefore is not { } before) return;
        _dragBefore = null;
        if (History.Record(ActionEdit.Between(before, _script.Actions)))
            ScriptChanged?.Invoke(this, EventArgs.Empty);
    }

    public void SetPlayhead(double seconds) => SetPlayhead(seconds, followPlayback: false);

    public void SetPlayhead(double seconds, bool followPlayback)
    {
        _playheadSeconds = seconds;
        if (_suppressPlaybackFollowUntilVisible && IsPlayheadInViewport())
            _suppressPlaybackFollowUntilVisible = false;

        if (followPlayback && !_suppressPlaybackFollowUntilVisible
            && _panStartX is null && _draggingIndex is null && !_scrubbing
            && (seconds < _viewStartSeconds || seconds >= _viewStartSeconds + ViewportSeconds()))
        {
            _viewStartSeconds = seconds - ViewportSeconds() / 2;
            ClampViewStart();
            UpdateScrollBar();
        }
        Invalidate();
    }

    /// <summary>Moves to the closest action strictly before the playhead and requests a seek.</summary>
    public bool NavigateToPreviousAction()
    {
        FunscriptAction? target = null;
        foreach (var action in _script.Actions)
        {
            if (action.At / 1000.0 >= _playheadSeconds) break;
            target = action;
        }

        return target is { } previous && NavigateToAction(previous);
    }

    /// <summary>Moves to the closest action strictly after the playhead and requests a seek.</summary>
    public bool NavigateToNextAction()
    {
        foreach (var action in _script.Actions)
        {
            if (action.At / 1000.0 > _playheadSeconds)
                return NavigateToAction(action);
        }

        return false;
    }

    /// <summary>Moves to the selected action and requests a seek, or returns false when none is selected.</summary>
    public bool NavigateToSelectedAction()
    {
        if (_selectedIndex is not { } index || index < 0 || index >= _script.Actions.Count)
            return false;

        return NavigateToAction(_script.Actions[index]);
    }

    private bool NavigateToAction(FunscriptAction action)
    {
        var seconds = action.At / 1000.0;
        SetPlayhead(seconds);
        SeekRequested?.Invoke(seconds);
        return true;
    }

    public void SetPatternPreview(GeneratedPattern? pattern)
    {
        var durationBefore = DurationSeconds;
        _patternPreview = pattern;
        if (DurationSeconds != durationBefore)
            ReclampZoom();
        else
            Invalidate();
    }

    public void SetPatternPreviewRange(int? startMs, int? endMs, bool invalid)
    {
        var durationBefore = DurationSeconds;
        _patternRangeStartMs = startMs;
        _patternRangeEndMs = endMs;
        _patternRangeInvalid = invalid;
        if (DurationSeconds != durationBefore)
            ReclampZoom();
        else
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

    private bool IsPlayheadInViewport() =>
        _playheadSeconds >= _viewStartSeconds
        && _playheadSeconds < _viewStartSeconds + ViewportSeconds();

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
        _viewStartSeconds = anchorSeconds - (anchorX - ContentLeft) / _pixelsPerSecond;
        ClampViewStart();
        UpdateScrollBar();
        ZoomChanged?.Invoke(this, EventArgs.Empty);
        Invalidate();
    }

    /// <summary>Wheel zoom anchored at a client-space X. Public because WinForms sends wheel input to the focused control, not the hovered one.</summary>
    public void ZoomByWheel(int delta, int clientX)
    {
        var anchorX = Math.Clamp(clientX, ContentLeft, BarLeft);
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
        _scrollBar.Maximum = (int)Math.Ceiling(DurationSeconds * ScrollbarUnitsPerSecond);
        _scrollBar.LargeChange = Math.Max(1, (int)Math.Ceiling(viewport * ScrollbarUnitsPerSecond));
        _scrollBar.SmallChange = Math.Max(1, (int)Math.Ceiling(viewport / 20 * ScrollbarUnitsPerSecond));
        _scrollBar.Value = Math.Clamp(
            (int)Math.Round(_viewStartSeconds * ScrollbarUnitsPerSecond),
            0,
            Math.Max(0, _scrollBar.Maximum - _scrollBar.LargeChange));
        _suppressScrollEvent = false;
    }

    private int TrackTop => Padding.Top + TopMargin;
    private int TrackBottom => ClientSize.Height - Padding.Bottom - BottomMargin - TopMargin;
    private int TrackHeight => Math.Max(1, TrackBottom - TrackTop);
    private int ContentLeft => Padding.Left;
    private int ContentRight => ClientSize.Width - Padding.Right;
    private int TrackAreaWidth => Math.Max(1, ClientSize.Width - Padding.Horizontal - ValueBarWidth);
    private int BarLeft => ContentLeft + TrackAreaWidth;
    private Rectangle TimelineContentBounds => new(
        ContentLeft,
        Padding.Top,
        Math.Max(0, ClientSize.Width - Padding.Horizontal),
        Math.Max(0, ClientSize.Height - Padding.Vertical - _scrollBar.Height));

    private void LayoutScrollBar()
    {
        _scrollBar.SetBounds(
            Padding.Left,
            ClientSize.Height - Padding.Bottom - _scrollBar.Height,
            Math.Max(0, ClientSize.Width - Padding.Horizontal),
            _scrollBar.Height);
    }

    private double TimeToX(double seconds) => ContentLeft + (seconds - _viewStartSeconds) * _pixelsPerSecond;
    private double XToTime(double x) => _viewStartSeconds + (x - ContentLeft) / _pixelsPerSecond;
    private double PosToY(int pos) => TrackBottom - pos / 100.0 * TrackHeight;
    private int YToPos(double y) => (int)Math.Round(Math.Clamp((TrackBottom - y) / TrackHeight * 100.0, 0, 100));

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        var paintState = g.Save();
        g.SetClip(TimelineContentBounds);

        PaintPatternRange(g);

        using (var gridPen = new Pen(Color.FromArgb(55, 55, 55)))
        {
            for (var p = 0; p <= 100; p += 25)
            {
                var y = (float)PosToY(p);
                g.DrawLine(gridPen, ContentLeft, y, BarLeft, y);
            }
        }

        var actions = _script.Actions;
        if (actions.Count > 0)
        {
            using var linePen = new Pen(Color.FromArgb(90, 170, 255), 2);
            var clipLeft = ContentLeft - 50f;
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
                if (x < ContentLeft - PointRadius || x > BarLeft + PointRadius) continue;
                var y = PosToY(actions[i].Pos);
                var selected = i == _selectedIndex;
                using var brush = new SolidBrush(selected ? Color.Orange : Color.White);
                var r = selected ? PointRadius + 1 : PointRadius;
                g.FillEllipse(brush, (float)(x - r), (float)(y - r), r * 2, r * 2);
            }
        }

        PaintPatternActions(g);

        using (var playPen = new Pen(Color.Red, 1.5f))
        {
            var x = (float)TimeToX(_playheadSeconds);
            if (x >= ContentLeft && x <= BarLeft)
                g.DrawLine(playPen, x, Padding.Top, x, TrackBottom);
        }

        PaintValueBar(g);

        g.Restore(paintState);
        base.OnPaint(e);
        PaintFocusIndicator(g);
    }

    private void PaintFocusIndicator(Graphics graphics)
    {
        if (ClientSize.Width <= 0 || ClientSize.Height <= 0) return;

        var borderColor = Focused
            ? SystemInformation.HighContrast ? SystemColors.Highlight : Color.FromArgb(255, 190, 65)
            : SystemInformation.HighContrast ? SystemColors.WindowText : Color.FromArgb(90, 90, 90);
        const int thickness = 1;
        using var borderBrush = new SolidBrush(borderColor);
        graphics.FillRectangle(borderBrush, 0, 0, ClientSize.Width, thickness);
        graphics.FillRectangle(borderBrush, 0, 0, thickness, ClientSize.Height);
        graphics.FillRectangle(borderBrush, ClientSize.Width - thickness, 0, thickness, ClientSize.Height);
        graphics.FillRectangle(borderBrush, 0, ClientSize.Height - thickness, ClientSize.Width, thickness);
    }

    protected override void OnGotFocus(EventArgs e)
    {
        base.OnGotFocus(e);
        Invalidate();
    }

    protected override void OnLostFocus(EventArgs e)
    {
        base.OnLostFocus(e);
        Invalidate();
    }

    protected override void OnSystemColorsChanged(EventArgs e)
    {
        base.OnSystemColorsChanged(e);
        Invalidate();
    }

    private void PaintPatternRange(Graphics graphics)
    {
        if (_patternRangeStartMs is not { } startMs || _patternRangeEndMs is not { } endMs) return;
        var startX = TimeToX(startMs / 1000.0);
        var endX = TimeToX(endMs / 1000.0);
        var left = Math.Clamp(Math.Min(startX, endX), ContentLeft, BarLeft);
        var right = Math.Clamp(Math.Max(startX, endX), ContentLeft, BarLeft);
        var color = _patternRangeInvalid ? Color.FromArgb(54, 225, 70, 70) : Color.FromArgb(42, 255, 190, 60);
        using var rangeBrush = new SolidBrush(color);
        if (_patternRangeInvalid && right - left < 4 && startX >= ContentLeft && startX <= BarLeft)
        {
            graphics.FillRectangle(rangeBrush, (float)(startX - 2), TrackTop, 4, TrackHeight);
            return;
        }
        if (right > left)
            graphics.FillRectangle(rangeBrush, (float)left, TrackTop, (float)(right - left), TrackHeight);
    }

    private void PaintPatternActions(Graphics graphics)
    {
        if (_patternPreview is not { } pattern || pattern.Actions.Count == 0) return;

        using var linePen = new Pen(Color.FromArgb(80, 235, 130), 2.5f);
        using var pointBrush = new SolidBrush(Color.FromArgb(110, 255, 155));
        using var pointOutline = new Pen(Color.FromArgb(25, 25, 25), 1.5f);
        var clipLeft = ContentLeft - 50f;
        var clipRight = BarLeft + 50f;
        FunscriptAction? previous = null;
        foreach (var action in pattern.Actions)
        {
            if (previous is { } before)
            {
                var from = new PointF((float)TimeToX(before.At / 1000.0), (float)PosToY(before.Pos));
                var to = new PointF((float)TimeToX(action.At / 1000.0), (float)PosToY(action.Pos));
                if (Math.Max(from.X, to.X) >= clipLeft && Math.Min(from.X, to.X) <= clipRight)
                    graphics.DrawLine(linePen, ClipX(from, to, clipLeft, clipRight), ClipX(to, from, clipLeft, clipRight));
            }

            var x = TimeToX(action.At / 1000.0);
            if (x >= ContentLeft - PointRadius && x <= BarLeft + PointRadius)
            {
                var y = PosToY(action.Pos);
                graphics.FillEllipse(pointBrush, (float)(x - PointRadius), (float)(y - PointRadius), PointRadius * 2, PointRadius * 2);
                graphics.DrawEllipse(pointOutline, (float)(x - PointRadius), (float)(y - PointRadius), PointRadius * 2, PointRadius * 2);
            }

            previous = action;
        }
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
            g.DrawLine(hoverPen, ContentLeft, y, barRect.Right, y);
        }
    }

    protected override void OnMouseWheel(MouseEventArgs e) => ZoomByWheel(e.Delta, e.X);

    protected override void OnMouseDown(MouseEventArgs e)
    {
		Focus();
        HandleMouseDown(e, ModifierKeys);
    }

    internal void HandleMouseDown(MouseEventArgs e, Keys modifiers)
    {
        if (!TimelineContentBounds.Contains(e.Location)) return;
        if (e.Button == MouseButtons.Middle)
        {
		    Debug.WriteLine($"MouseDown: button={e.Button}, x={e.X}, y={e.Y}");

            if (_draggingIndex is not null || _scrubbing || e.X >= BarLeft) return;
            PanStart(e.X, MouseButtons.Middle);
            Invalidate();
            return;
        }
        if (_panStartX is not null) return;
        if (e.Button != MouseButtons.Left) return;

        if (TryStartShiftPan(e, modifiers)) return;

        if (e.X >= BarLeft)
        {
            InsertAtPlayhead(e.Y);
            return;
        }

        var hit = HitTest(e.Location);
        if (hit is { } index)
        {
            FinishDrag();
            _selectedIndex = index;
            _draggingIndex = index;
            _dragBefore = _script.Actions.ToArray();
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
        if (!TimelineContentBounds.Contains(e.Location)) return;
        if (e.Button != MouseButtons.Left) return;
        if (e.X >= BarLeft) return;
        if (HitTest(e.Location) is not null) return;

        var at = (int)Math.Round(Math.Clamp(XToTime(e.X), 0, DurationSeconds) * 1000);
        var pos = YToPos(e.Y);
        Commit(() => _script.AddOrReplace(new FunscriptAction(at, pos)));
        Invalidate();
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        if (UpdatePan(e.X, ModifierKeys)) return;
        if (e.X >= BarLeft && e.X < ContentRight && _draggingIndex is null && !_scrubbing)
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
        if (_panStartX is not null)
        {
            if (e.Button == _panButton)
            {
                if (_panButton == MouseButtons.Left)
                    Debug.WriteLine($"ShiftLeftUp: button={e.Button}, modifiers={ModifierKeys}, x={e.X}, y={e.Y}");
                else
                    Debug.WriteLine($"MouseUp: button={e.Button}, x={e.X}, y={e.Y}");
                PanEnd();
            }
            return;
        }
        FinishDrag();
        _scrubbing = false;
    }

    private void PanStart(int clientX, MouseButtons button)
    {
        if (_panStartX is not null) return;
        _panStartX = clientX;
        _panStartSeconds = _viewStartSeconds;
        _panButton = button;
        _hoverBarPos = null;
        Cursor = Cursors.SizeWE;
        Capture = true;
    }

    internal bool TryStartShiftPan(MouseEventArgs e, Keys modifiers)
    {
        if (e.Button != MouseButtons.Left || (modifiers & Keys.Shift) == 0
            || _panStartX is not null || _draggingIndex is not null || _scrubbing
            || !TimelineContentBounds.Contains(e.Location) || e.X >= BarLeft
            || HitTest(e.Location) is not null)
            return false;

        PanStart(e.X, MouseButtons.Left);
        Debug.WriteLine($"ShiftLeftDown: button={e.Button}, modifiers={modifiers}, x={e.X}, y={e.Y}");
        Invalidate();
        return true;
    }

    internal bool UpdatePan(int clientX, Keys modifiers)
    {
        if (_panStartX is not { } startX) return false;
        if (_panButton == MouseButtons.Left && (modifiers & Keys.Shift) == 0)
        {
            PanEnd();
            return true;
        }

	    if (_panButton == MouseButtons.Left)
	        Debug.WriteLine($"ShiftLeftMove: buttons={Control.MouseButtons}, modifiers={modifiers}, x={clientX}");
	    else
	        Debug.WriteLine($"MouseMove: buttons={Control.MouseButtons}, x={clientX}");

        _viewStartSeconds = _panStartSeconds - (clientX - startX) / _pixelsPerSecond;
        ClampViewStart();
        UpdateScrollBar();
        Invalidate();
        return true;
    }

    private void PanEnd(bool suppressFollow = true)
    {
        if (_panStartX is null) return;
        if (suppressFollow)
            _suppressPlaybackFollowUntilVisible = !IsPlayheadInViewport();
        _panStartX = null;
        _panButton = null;
        Cursor = Cursors.Default;
        Capture = false;
    }

    protected override void OnMouseCaptureChanged(EventArgs e)
    {
        if (!Capture) PanEnd();
        base.OnMouseCaptureChanged(e);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Delete && _selectedIndex is { } index)
        {
            Commit(() => _script.RemoveAt(index));
            _selectedIndex = null;
            Invalidate();
            e.Handled = true;
        }
    }

    protected override void OnKeyUp(KeyEventArgs e)
    {
        if (_panButton == MouseButtons.Left && (ModifierKeys & Keys.Shift) == 0)
            PanEnd();

        base.OnKeyUp(e);
    }

    public override bool PreProcessMessage(ref Message msg)
    {
        if (Focused && msg.Msg == 0x0100 && msg.WParam.ToInt64() == (int)Keys.Space && ModifierKeys == Keys.None)
        {
            if ((msg.LParam.ToInt64() & (1L << 30)) == 0)
                PlayPauseRequested?.Invoke(this, EventArgs.Empty);
            return true;
        }
        if (TryDecodePositionKey(msg.Msg, msg.LParam.ToInt64(), Focused, ModifierKeys, out var position))
        {
            if (position is { } value) InsertPositionAtPlayhead(value);
            return true;
        }

        return base.PreProcessMessage(ref msg);
    }

    internal static bool TryDecodePositionKey(int message, long keyData, bool focused, Keys modifiers, out int? position)
    {
        position = null;
        if (!focused || message != 0x0100 || modifiers != Keys.None || (keyData & (1L << 24)) != 0)
            return false;
        var scanCode = (int)((keyData >> 16) & 0xff);
        var value = scanCode == 0x29 ? 0
            : scanCode is >= 0x02 and <= 0x0b ? (scanCode - 1) * 10
            : -1;
        if (value < 0) return false;
        if ((keyData & (1L << 30)) == 0) position = value;
        return true;
    }

    protected override bool IsInputKey(Keys keyData)
    {
        var keyCode = keyData & Keys.KeyCode;
        return keyCode == Keys.Delete || keyCode == Keys.Left || keyCode == Keys.Right || base.IsInputKey(keyData);
    }

    private void Seek(int x)
    {
        var seconds = Math.Clamp(XToTime(x), 0, DurationSeconds);
        _playheadSeconds = seconds;
        SeekRequested?.Invoke(seconds);
        Invalidate();
    }

    private void InsertAtPlayhead(int y) => InsertPositionAtPlayhead(YToPos(y));

    public void InsertPositionAtPlayhead(int position)
    {
        if (position is < 0 or > 100)
            throw new ArgumentOutOfRangeException(nameof(position), "Position must be between 0 and 100.");
        var at = (int)Math.Round(Math.Clamp(_playheadSeconds, 0, DurationSeconds) * 1000);
        Commit(() => _script.AddOrReplace(new FunscriptAction(at, position)));
        var actions = _script.Actions;
        for (var i = 0; i < actions.Count; i++)
        {
            if (actions[i].At != at) continue;
            _selectedIndex = i;
            break;
        }
        Invalidate();
    }

    private int? HitTest(Point location)
    {
        var actions = _script.Actions;
        for (var i = 0; i < actions.Count; i++)
        {
            var x = TimeToX(actions[i].At / 1000.0);
            if (x < ContentLeft - HitRadius || x > ContentRight + HitRadius) continue;
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
        LayoutScrollBar();
        ReclampZoom();
    }
}
