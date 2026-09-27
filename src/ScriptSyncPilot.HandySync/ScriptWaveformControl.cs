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
        Invalidate();
    }

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

            using var pen = new Pen(Color.FromArgb(90, 170, 255));
            for (var x = 0; x < width; x++)
            {
                if (!hasData[x]) continue;
                var yTop = PosToY(max[x], height);
                var yBottom = PosToY(min[x], height);
                g.DrawLine(pen, x, yTop, x, Math.Max(yBottom, yTop + 1));
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
}
