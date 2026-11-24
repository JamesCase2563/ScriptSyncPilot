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
    private const int PlayheadPointHaloRadius = PointRadius + 4;
    private const int HitRadius = 8;
    private const double SegmentHoverRadius = 6;
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
    private double? _videoFrameRate;
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
    private TimelineMovementReadout? _movementReadout;
    private bool _speedColoringEnabled = true;
    private SegmentSpeedScaleMode _speedScaleMode = SegmentSpeedScaleMode.FitToScript;
    private SegmentSpeedColorRange _manualSpeedColorRange = new(0, 700);
    private SegmentSpeedColorRange _fittedSpeedColorRange = new(0, 100);

    public event Action<double>? SeekRequested;
    public event EventHandler? PlayPauseRequested;
    public event EventHandler? ScriptChanged;
    public event EventHandler? ZoomChanged;
    public event EventHandler? NavigationAvailabilityChanged;
    public event Action<TimelineMovementReadout?>? MovementReadoutChanged;
    public event EventHandler? SpeedColorScaleChanged;

    [System.ComponentModel.Browsable(false)]
    public bool SpeedColoringEnabled => _speedColoringEnabled;

    [System.ComponentModel.Browsable(false)]
    public SegmentSpeedScaleMode SpeedScaleMode => _speedScaleMode;

    [System.ComponentModel.Browsable(false)]
    public SegmentSpeedColorRange ManualSpeedColorRange => _manualSpeedColorRange;

    [System.ComponentModel.Browsable(false)]
    public SegmentSpeedColorRange FittedSpeedColorRange => _fittedSpeedColorRange;

    [System.ComponentModel.Browsable(false)]
    public SegmentSpeedColorRange ActiveSpeedColorRange => _speedScaleMode == SegmentSpeedScaleMode.Manual
        ? _manualSpeedColorRange
        : _fittedSpeedColorRange;

    [System.ComponentModel.Browsable(false)]
    public bool CanNavigateToPreviousAction => FindPreviousActionIndex() >= 0;

    [System.ComponentModel.Browsable(false)]
    public bool CanNavigateToNextAction => FindNextActionIndex() >= 0;

    [System.ComponentModel.Browsable(false)]
    public bool CanNavigateToSelectedAction => _selectedIndex is { } index && index >= 0 && index < _script.Actions.Count;

    [System.ComponentModel.Browsable(false)]
    public double PlayheadSeconds => _playheadSeconds;

    [System.ComponentModel.Browsable(false)]
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public double? VideoFrameRate
    {
        get => _videoFrameRate;
        set
        {
            _videoFrameRate = value is > 0 && double.IsFinite(value.Value) ? value : null;
            NavigationAvailabilityChanged?.Invoke(this, EventArgs.Empty);
            Invalidate();
        }
    }

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
        _movementReadout = null;
        MovementReadoutChanged?.Invoke(null);
        _suppressPlaybackFollowUntilVisible = false;
        _script = script;
        FitSpeedColorScaleToCurrentScript();
        _selectedIndex = null;
        _draggingIndex = null;
        _dragBefore = null;
        History.Reset();
        ReclampZoom();
        NavigationAvailabilityChanged?.Invoke(this, EventArgs.Empty);
    }

    public void ConfigureSpeedColoring(bool enabled, SegmentSpeedScaleMode mode, double manualMinimum, double manualMaximum)
    {
        _speedColoringEnabled = enabled;
        _speedScaleMode = Enum.IsDefined(mode) ? mode : SegmentSpeedScaleMode.FitToScript;
        try
        {
            _manualSpeedColorRange = new SegmentSpeedColorRange(manualMinimum, manualMaximum);
        }
        catch (ArgumentOutOfRangeException)
        {
            _manualSpeedColorRange = new SegmentSpeedColorRange(0, 700);
        }

        FitSpeedColorScaleToCurrentScript();
    }

    public void FitSpeedColorScaleToCurrentScript()
    {
        _fittedSpeedColorRange = SegmentSpeedScaleCalculator.FitToScript(_script.Actions);
        SpeedColorScaleChanged?.Invoke(this, EventArgs.Empty);
        Invalidate();
    }

    public SegmentSpeedColorPosition? MapSpeedColor(SegmentSpeed? segment)
        => GetSegmentSpeedColor(segment)?.Position;

    internal SegmentSpeedColor? GetSegmentSpeedColor(SegmentSpeed? segment)
    {
        if (!_speedColoringEnabled || segment is not { HasValidTiming: true, AveragePercentagePointsPerSecond: { } speed })
            return null;
        return ActiveSpeedColorRange.MapColor(speed);
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
        UpdateMovementReadout();

        ScriptChanged?.Invoke(this, EventArgs.Empty);
        NavigationAvailabilityChanged?.Invoke(this, EventArgs.Empty);
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
        UpdateMovementReadout();
        ScriptChanged?.Invoke(this, EventArgs.Empty);
        NavigationAvailabilityChanged?.Invoke(this, EventArgs.Empty);
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
        NavigationAvailabilityChanged?.Invoke(this, EventArgs.Empty);
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
        NavigationAvailabilityChanged?.Invoke(this, EventArgs.Empty);
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
        var index = FindPreviousActionIndex();
        return index >= 0 && NavigateToAction(_script.Actions[index]);
    }

    /// <summary>Moves to the closest action strictly after the playhead and requests a seek.</summary>
    public bool NavigateToNextAction()
    {
        var index = FindNextActionIndex();
        return index >= 0 && NavigateToAction(_script.Actions[index]);
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

    private int FindPreviousActionIndex()
    {
        var actions = _script.Actions;
        var referenceSeconds = GetNavigationReferenceSeconds();
        var low = 0;
        var high = actions.Count;
        while (low < high)
        {
            var mid = low + (high - low) / 2;
            if (actions[mid].At / 1000.0 < referenceSeconds) low = mid + 1;
            else high = mid;
        }
        return low - 1;
    }

    private int FindNextActionIndex()
    {
        var actions = _script.Actions;
        var referenceSeconds = GetNavigationReferenceSeconds();
        var low = 0;
        var high = actions.Count;
        while (low < high)
        {
            var mid = low + (high - low) / 2;
            if (actions[mid].At / 1000.0 <= referenceSeconds) low = mid + 1;
            else high = mid;
        }
        return low < actions.Count ? low : -1;
    }

    private double GetNavigationReferenceSeconds()
    {
        var alignedIndex = FindPlayheadAlignedActionIndex(_script.Actions, _playheadSeconds);
        return alignedIndex >= 0 ? _script.Actions[alignedIndex].At / 1000.0 : _playheadSeconds;
    }

    private int FindPlayheadAlignedActionIndex(IReadOnlyList<FunscriptAction> actions, double playheadSeconds)
    {
        if (_videoFrameRate is not { } frameRate || actions.Count == 0) return -1;

        var closestIndex = -1;
        var closestDistance = double.PositiveInfinity;
        for (var i = 0; i < actions.Count; i++)
        {
            var distance = Math.Abs(actions[i].At / 1000.0 - playheadSeconds);
            if (distance < closestDistance)
            {
                closestDistance = distance;
                closestIndex = i;
            }
        }

        // A half-frame window treats mpv's frame-based position as aligned while
        // choosing only the nearest timestamp, independent of timeline zoom.
        return closestDistance <= 0.5 / frameRate ? closestIndex : -1;
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
            var alignedActionIndex = FindPlayheadAlignedActionIndex(actions, _playheadSeconds);
            PaintActionSegments(g, actions);

            for (var i = 0; i < actions.Count; i++)
            {
                var x = TimeToX(actions[i].At / 1000.0);
                if (x < ContentLeft - PointRadius || x > BarLeft + PointRadius) continue;
                var y = PosToY(actions[i].Pos);
                if (i == alignedActionIndex)
                {
                    // Contrasting strokes keep the ring visible over the graph and
                    // adapt to Windows high-contrast colors. Draw behind the point so
                    // its fill and selection styling remain clear.
                    using var haloContrastPen = new Pen(SystemColors.Window, 4);
                    using var haloPen = new Pen(SystemColors.Highlight, 2);
                    var haloDiameter = PlayheadPointHaloRadius * 2;
                    g.DrawEllipse(haloContrastPen, (float)(x - PlayheadPointHaloRadius), (float)(y - PlayheadPointHaloRadius), haloDiameter, haloDiameter);
                    g.DrawEllipse(haloPen, (float)(x - PlayheadPointHaloRadius), (float)(y - PlayheadPointHaloRadius), haloDiameter, haloDiameter);
                }
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

    private void PaintActionSegments(Graphics graphics, IReadOnlyList<FunscriptAction> actions)
    {
        var clipLeft = ContentLeft - 50f;
        var clipRight = BarLeft + 50f;
        var highContrast = SystemInformation.HighContrast;
        var pens = new Dictionary<Color, Pen>();
        using var contrastBackgroundPen = highContrast ? new Pen(SystemColors.Window, 4) : null;
        using var contrastTextPen = highContrast ? new Pen(SystemColors.WindowText, 3) : null;
        try
        {
            for (var i = 0; i < actions.Count - 1; i++)
            {
                var startAction = actions[i];
                var endAction = actions[i + 1];
                var start = new PointF((float)TimeToX(startAction.At / 1000.0), (float)PosToY(startAction.Pos));
                var end = new PointF((float)TimeToX(endAction.At / 1000.0), (float)PosToY(endAction.Pos));
                if (Math.Max(start.X, end.X) < clipLeft || Math.Min(start.X, end.X) > clipRight) continue;

                var speed = SegmentSpeedCalculator.Calculate(startAction, endAction);
                var mapping = GetSegmentSpeedColor(speed);
                var color = mapping is { } mapped
                    ? highContrast ? SystemColors.Highlight : ToUiColor(mapped.Color)
                    : highContrast ? SystemColors.Highlight : Color.FromArgb(90, 170, 255);
                if (!pens.TryGetValue(color, out var linePen))
                    pens[color] = linePen = new Pen(color, 2.5f);

                var clippedStart = ClipX(start, end, clipLeft, clipRight);
                var clippedEnd = ClipX(end, start, clipLeft, clipRight);
                if (contrastBackgroundPen is not null && contrastTextPen is not null)
                {
                    graphics.DrawLine(contrastBackgroundPen, clippedStart, clippedEnd);
                    graphics.DrawLine(contrastTextPen, clippedStart, clippedEnd);
                }
                graphics.DrawLine(linePen, clippedStart, clippedEnd);
            }
        }
        finally
        {
            foreach (var pen in pens.Values) pen.Dispose();
        }
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
            UpdateMovementReadout(e.Location);
            Invalidate();
            return;
        }
        if (_panStartX is not null) return;
        if (e.Button != MouseButtons.Left) return;

        if (TryStartShiftPan(e, modifiers)) return;

        if (e.X >= BarLeft)
        {
            InsertAtPlayhead(e.Y);
            UpdateMovementReadout();
            return;
        }

        var hit = HitTest(e.Location);
        if (hit is { } index)
        {
            FinishDrag();
            _selectedIndex = index;
            _draggingIndex = index;
            _dragBefore = _script.Actions.ToArray();
            NavigationAvailabilityChanged?.Invoke(this, EventArgs.Empty);
        }
        else
        {
            _selectedIndex = null;
            NavigationAvailabilityChanged?.Invoke(this, EventArgs.Empty);
            _scrubbing = true;
            Seek(e.X);
        }
        UpdateMovementReadout(e.Location);
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
        if (UpdatePan(e.X, ModifierKeys))
        {
            UpdateMovementReadout(e.Location);
            return;
        }
        if (e.X >= BarLeft && e.X < ContentRight && _draggingIndex is null && !_scrubbing)
        {
            _hoverBarPos = YToPos(e.Y);
            Cursor = Cursors.Hand;
            UpdateMovementReadout(e.Location);
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
            NavigationAvailabilityChanged?.Invoke(this, EventArgs.Empty);
            UpdateMovementReadout(e.Location);
            Invalidate();
        }
        else if (_scrubbing)
        {
            UpdateMovementReadout(e.Location);
            Seek(e.X);
        }
        else
        {
            UpdateMovementReadout(e.Location);
        }
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        UpdateMovementReadout();
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
        UpdateMovementReadout(e.Location);
    }

    private void PanStart(int clientX, MouseButtons button)
    {
        if (_panStartX is not null) return;
        _panStartX = clientX;
        _panStartSeconds = _viewStartSeconds;
        _panButton = button;
        _hoverBarPos = null;
        UpdateMovementReadout();
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
        if (Focused && e.Modifiers == Keys.Control && e.KeyCode == Keys.Up)
        {
            NavigateToPreviousAction();
            e.Handled = true;
            e.SuppressKeyPress = true;
            return;
        }
        if (Focused && e.Modifiers == Keys.Control && e.KeyCode == Keys.Down)
        {
            NavigateToNextAction();
            e.Handled = true;
            e.SuppressKeyPress = true;
            return;
        }
        if (Focused && e.Modifiers == Keys.Control && e.KeyCode == Keys.Enter)
        {
            NavigateToSelectedAction();
            e.Handled = true;
            e.SuppressKeyPress = true;
            return;
        }
        if (e.KeyCode == Keys.Delete && _selectedIndex is { } index)
        {
            Commit(() => _script.RemoveAt(index));
            _selectedIndex = null;
            NavigationAvailabilityChanged?.Invoke(this, EventArgs.Empty);
            UpdateMovementReadout();
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
        var modifiers = keyData & Keys.Modifiers;
        var pointNavigationKey = ((keyCode is Keys.Up or Keys.Down) && modifiers == Keys.Control)
            || (keyCode == Keys.Enter && modifiers == Keys.Control);
        return pointNavigationKey || keyCode == Keys.Delete || keyCode == Keys.Left || keyCode == Keys.Right
            || base.IsInputKey(keyData);
    }

    private void Seek(int x)
    {
        var seconds = Math.Clamp(XToTime(x), 0, DurationSeconds);
        _playheadSeconds = seconds;
        NavigationAvailabilityChanged?.Invoke(this, EventArgs.Empty);
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
        NavigationAvailabilityChanged?.Invoke(this, EventArgs.Empty);
        UpdateMovementReadout();
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

    private int? FindHoveredSegmentIndex(Point location)
    {
        if (!TimelineContentBounds.Contains(location) || location.X >= BarLeft
            || location.Y < TrackTop || location.Y > TrackBottom
            || HitTest(location) is not null)
            return null;

        var actions = _script.Actions;
        var bestDistanceSquared = SegmentHoverRadius * SegmentHoverRadius;
        int? bestIndex = null;
        for (var i = 0; i < actions.Count - 1; i++)
        {
            var start = new PointF((float)TimeToX(actions[i].At / 1000.0), (float)PosToY(actions[i].Pos));
            var end = new PointF((float)TimeToX(actions[i + 1].At / 1000.0), (float)PosToY(actions[i + 1].Pos));
            var distanceSquared = DistanceSquaredToSegment(location, start, end);
            if (distanceSquared > bestDistanceSquared) continue;
            bestDistanceSquared = distanceSquared;
            bestIndex = i;
        }

        return bestIndex;
    }

    private static double DistanceSquaredToSegment(Point point, PointF start, PointF end)
    {
        var dx = end.X - start.X;
        var dy = end.Y - start.Y;
        var lengthSquared = dx * dx + dy * dy;
        var projection = lengthSquared <= 0
            ? 0
            : Math.Clamp(((point.X - start.X) * dx + (point.Y - start.Y) * dy) / lengthSquared, 0, 1);
        var nearestX = start.X + projection * dx;
        var nearestY = start.Y + projection * dy;
        var distanceX = point.X - nearestX;
        var distanceY = point.Y - nearestY;
        return distanceX * distanceX + distanceY * distanceY;
    }

    private static Color ToUiColor(RgbColor color) => Color.FromArgb(color.A, color.R, color.G, color.B);

    private void UpdateMovementReadout(Point? pointerLocation = null)
    {
        var actions = _script.Actions;
        int? pointIndex = _draggingIndex;
        var canHover = _panStartX is null && !_scrubbing && pointerLocation is { } pointer
            && TimelineContentBounds.Contains(pointer);

        if (pointIndex is null && canHover && pointerLocation!.Value.X >= BarLeft
            && pointerLocation.Value.X < ContentRight
            && pointerLocation.Value.Y >= TrackTop && pointerLocation.Value.Y <= TrackBottom)
        {
            var preview = CreateValueBarPreview(pointerLocation.Value);
            if (_movementReadout != preview)
            {
                _movementReadout = preview;
                MovementReadoutChanged?.Invoke(preview);
            }
            return;
        }

        if (pointIndex is null && canHover && pointerLocation!.Value.X < BarLeft)
            pointIndex = HitTest(pointerLocation!.Value);

        TimelineMovementReadout? nextReadout = pointIndex is { } point && point >= 0 && point < actions.Count
            ? CreatePointReadout(actions, point)
            : null;

        if (nextReadout is null && canHover && pointIndex is null
            && FindHoveredSegmentIndex(pointerLocation!.Value) is { } segmentIndex)
        {
            var measurement = SegmentSpeedCalculator.Calculate(actions[segmentIndex], actions[segmentIndex + 1]);
            nextReadout = new TimelineMovementReadout(
                TimelineMovementReadoutKind.Segment, null, measurement,
                false, null, false, null);
        }

        if (nextReadout is null && _selectedIndex is { } selected && selected >= 0 && selected < actions.Count)
            nextReadout = CreatePointReadout(actions, selected);

        if (_movementReadout == nextReadout) return;
        _movementReadout = nextReadout;
        MovementReadoutChanged?.Invoke(nextReadout);
    }

    private TimelineMovementReadout CreateValueBarPreview(Point location)
    {
        var at = (int)Math.Round(Math.Clamp(_playheadSeconds, 0, DurationSeconds) * 1000);
        var candidate = new FunscriptAction(at, YToPos(location.Y));
        var previewActions = _script.Actions.ToList();
        var index = previewActions.FindIndex(action => action.At >= at);
        if (index >= 0 && previewActions[index].At == at)
            previewActions[index] = candidate;
        else
        {
            if (index < 0) index = previewActions.Count;
            previewActions.Insert(index, candidate);
        }

        var readout = CreatePointReadout(previewActions, index);
        return readout with { Kind = TimelineMovementReadoutKind.ValueBarPreview };
    }

    private static TimelineMovementReadout CreatePointReadout(IReadOnlyList<FunscriptAction> actions, int index)
    {
        var hasIncoming = index > 0;
        var hasOutgoing = index < actions.Count - 1;
        SegmentSpeed? incoming = hasIncoming ? SegmentSpeedCalculator.Calculate(actions[index - 1], actions[index]) : null;
        SegmentSpeed? outgoing = hasOutgoing ? SegmentSpeedCalculator.Calculate(actions[index], actions[index + 1]) : null;
        return new TimelineMovementReadout(
            TimelineMovementReadoutKind.Point, actions[index], null,
            hasIncoming, incoming, hasOutgoing, outgoing);
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        LayoutScrollBar();
        ReclampZoom();
    }
}
