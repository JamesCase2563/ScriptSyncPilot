using ScriptSyncPilot.Common;

namespace ScriptSyncPilot.HandySync;

/// <summary>
/// Read-only overview of the loaded script: the whole funscript squeezed into
/// the control width as a position-over-time graph, with a red playhead line.
/// Unlike the editor's timeline this never zooms or scrolls, so one pixel can
/// cover many actions; the column min/max is drawn rather than a single
/// sample, otherwise fast strokes vanish at typical window widths.
/// </summary>
public sealed class ScriptWaveformControl : Control
{
    private Funscript? _script;
    private double _scriptDurationSeconds;
    private double _mediaDurationSeconds;
    private double _playheadSeconds;
    private bool _dragging;
    private bool _speedColoringEnabled = true;
    private SegmentSpeedScaleMode _speedScaleMode = SegmentSpeedScaleMode.FitToScript;
    private SegmentSpeedColorRange _manualSpeedRange = new(0, 700);
    private SegmentSpeedColorRange _fittedSpeedRange = new(0, 100);

    /// <summary>Raised with the clicked time in seconds when the user clicks or drags on the bar.</summary>
    public event Action<double>? SeekRequested;

    public ScriptWaveformControl()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        BackColor = Color.FromArgb(30, 30, 30);
        Cursor = Cursors.Hand;
    }

    // The script usually ends before the video does, so the video's duration wins as the X axis when known.
    private double AxisDurationSeconds => _mediaDurationSeconds > 0 ? _mediaDurationSeconds : _scriptDurationSeconds;

    public void SetScript(Funscript? script)
    {
        _script = script;
        _scriptDurationSeconds = script is { Actions.Count: > 0 } ? script.Actions[^1].At / 1000.0 : 0;
        _playheadSeconds = 0;
        _fittedSpeedRange = SegmentSpeedScaleCalculator.FitToScript(script?.Actions ?? []);
        Invalidate();
    }

    public void ConfigureSpeedColoring(bool enabled, SegmentSpeedScaleMode mode, double manualMinimum, double manualMaximum)
    {
        _speedColoringEnabled = enabled;
        _speedScaleMode = Enum.IsDefined(mode) ? mode : SegmentSpeedScaleMode.FitToScript;
        try
        {
            _manualSpeedRange = new SegmentSpeedColorRange(manualMinimum, manualMaximum);
        }
        catch (ArgumentOutOfRangeException)
        {
            _manualSpeedRange = new SegmentSpeedColorRange(0, 700);
        }
        _fittedSpeedRange = SegmentSpeedScaleCalculator.FitToScript(_script?.Actions ?? []);
        Invalidate();
    }

    public SegmentSpeedColorRange ActiveSpeedRange => _speedScaleMode == SegmentSpeedScaleMode.Manual
        ? _manualSpeedRange
        : _fittedSpeedRange;

    public SegmentSpeedColorRange FittedSpeedRange => _fittedSpeedRange;
    public bool SpeedColoringEnabled => _speedColoringEnabled;
    public SegmentSpeedScaleMode SpeedScaleMode => _speedScaleMode;
    public SegmentSpeedColorRange ManualSpeedRange => _manualSpeedRange;

    public void SetMediaDuration(double seconds)
    {
        _mediaDurationSeconds = seconds > 0 ? seconds : 0;
        Invalidate();
    }

    public void SetPlayhead(double seconds)
    {
        _playheadSeconds = seconds;
        Invalidate();
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Left) return;
        _dragging = true;
        RequestSeek(e.X);
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        if (_dragging) RequestSeek(e.X);
    }

    protected override void OnMouseUp(MouseEventArgs e) => _dragging = false;

    protected override void OnSystemColorsChanged(EventArgs e)
    {
        base.OnSystemColorsChanged(e);
        Invalidate();
    }

    private void RequestSeek(int x)
    {
        var duration = AxisDurationSeconds;
        if (duration <= 0 || ClientSize.Width <= 0) return;
        var seconds = Math.Clamp((double)x / ClientSize.Width * duration, 0, duration);
        _playheadSeconds = seconds;
        SeekRequested?.Invoke(seconds);
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        var width = ClientSize.Width;
        var height = ClientSize.Height;

        using (var midPen = new Pen(Color.FromArgb(55, 55, 55)))
            g.DrawLine(midPen, 0, height / 2f, width, height / 2f);

        var actions = _script?.Actions;
        var duration = AxisDurationSeconds;
        if (actions is { Count: > 0 } && duration > 0 && width > 0)
        {
            var min = new int[width];
            var max = new int[width];
            var hasData = new bool[width];
            var maximumColumnSpeed = ComputeMaximumSegmentSpeedPerColumn(actions, duration, width);
            foreach (var a in actions)
            {
                var column = (int)(a.At / 1000.0 / duration * (width - 1));
                column = Math.Clamp(column, 0, width - 1);
                if (!hasData[column])
                {
                    hasData[column] = true;
                    min[column] = max[column] = a.Pos;
                }
                else
                {
                    min[column] = Math.Min(min[column], a.Pos);
                    max[column] = Math.Max(max[column], a.Pos);
                }
            }

            var highContrast = SystemInformation.HighContrast;
            using var basePen = new Pen(highContrast ? SystemColors.Highlight : Color.FromArgb(90, 170, 255), 1.5f);
            using var contrastBackgroundPen = highContrast ? new Pen(SystemColors.Window, 4) : null;
            using var contrastTextPen = highContrast ? new Pen(SystemColors.WindowText, 3) : null;
            var colorPens = new Dictionary<Color, Pen>();
            try
            {
                for (var x = 0; x < width; x++)
                {
                    if (!hasData[x]) continue;
                    var yTop = PosToY(max[x], height);
                    var yBottom = PosToY(min[x], height);
                    var color = basePen.Color;
                    if (maximumColumnSpeed[x] is { } maximumSpeed && GetSpeedColor(maximumSpeed) is { } mapped)
                    {
                        color = highContrast ? SystemColors.Highlight : Color.FromArgb(
                            mapped.Color.A, mapped.Color.R, mapped.Color.G, mapped.Color.B);
                    }
                    if (!colorPens.TryGetValue(color, out var colorPen))
                        colorPens[color] = colorPen = new Pen(color, 1.5f);

                    var endY = Math.Max(yBottom, yTop + 1);
                    if (contrastBackgroundPen is not null && contrastTextPen is not null)
                    {
                        g.DrawLine(contrastBackgroundPen, x, yTop, x, endY);
                        g.DrawLine(contrastTextPen, x, yTop, x, endY);
                    }
                    g.DrawLine(colorPen, x, yTop, x, endY);
                }
            }
            finally
            {
                foreach (var pen in colorPens.Values) pen.Dispose();
            }
        }

        if (duration > 0)
        {
            var x = (float)(_playheadSeconds / duration * width);
            if (x >= 0 && x <= width)
            {
                using var playPen = new Pen(Color.Red, 1.5f);
                g.DrawLine(playPen, x, 0, x, height);
            }
        }

        base.OnPaint(e);
    }

    private static float PosToY(int pos, int height) => height - pos / 100f * height;

    internal SegmentSpeedColor? GetSpeedColor(double speed) =>
        _speedColoringEnabled ? ActiveSpeedRange.MapColor(speed) : null;

    internal static double?[] ComputeMaximumSegmentSpeedPerColumn(
        IReadOnlyList<FunscriptAction> actions, double durationSeconds, int width)
    {
        ArgumentNullException.ThrowIfNull(actions);
        if (width < 0) throw new ArgumentOutOfRangeException(nameof(width));
        var maximums = new double?[width];
        if (width == 0 || !double.IsFinite(durationSeconds) || durationSeconds <= 0) return maximums;

        var xScale = Math.Max(0, width - 1);
        for (var i = 0; i < actions.Count - 1; i++)
        {
            var segment = SegmentSpeedCalculator.Calculate(actions[i], actions[i + 1]);
            if (!segment.HasValidTiming || segment.AveragePercentagePointsPerSecond is not { } speed) continue;

            var startX = actions[i].At / 1000.0 / durationSeconds * xScale;
            var endX = actions[i + 1].At / 1000.0 / durationSeconds * xScale;
            var firstColumn = Math.Clamp((int)Math.Floor(Math.Min(startX, endX)), 0, width - 1);
            var lastColumn = Math.Clamp((int)Math.Floor(Math.Max(startX, endX)), 0, width - 1);
            for (var column = firstColumn; column <= lastColumn; column++)
            {
                if (maximums[column] is { } current && current >= speed) continue;
                maximums[column] = speed;
            }
        }

        return maximums;
    }
}
