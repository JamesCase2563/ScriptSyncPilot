using System.ComponentModel;
using System.Drawing.Drawing2D;

namespace ScriptSyncPilot.HandySync;

public sealed class DeviceSliderControl : Control
{
    private const int ThumbRadius = 8;
    private int _minimum;
    private int _maximum = 100;
    private int _lowerValue;
    private int _upperValue = 100;
    private int _activeThumb;
    private bool _dragging;

    public DeviceSliderControl()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
                 | ControlStyles.ResizeRedraw | ControlStyles.UserPaint | ControlStyles.Selectable, true);
        TabStop = true;
        MinimumSize = new Size(80, 32);
        Size = new Size(220, 40);
    }

    public event EventHandler? ValuesChanged;

    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int Minimum
    {
        get => _minimum;
        set
        {
            if (_minimum == value) return;
            _minimum = value;
            _maximum = Math.Max(_maximum, _minimum + 1);
            SetValues(_lowerValue, _upperValue);
        }
    }

    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int Maximum
    {
        get => _maximum;
        set
        {
            if (_maximum == value) return;
            _maximum = Math.Max(value, _minimum + 1);
            SetValues(_lowerValue, _upperValue);
        }
    }

    public int LowerValue => _lowerValue;
    public int UpperValue => _upperValue;
    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool IsRange { get; set; }
    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Orientation Orientation { get; set; } = Orientation.Horizontal;
    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int SmallStep { get; set; } = 1;
    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int LargeStep { get; set; } = 10;

    public bool IsBeingAdjusted => _dragging;

    public void SetValues(int lowerValue, int upperValue)
    {
        _lowerValue = Math.Clamp(lowerValue, _minimum, _maximum);
        _upperValue = IsRange ? Math.Clamp(upperValue, _minimum, _maximum) : _lowerValue;
        if (IsRange && _lowerValue > _upperValue) _lowerValue = _upperValue;
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        var trackStart = TrackStart;
        var trackEnd = TrackEnd;
        var lowPoint = ValueToPoint(IsRange ? _lowerValue : Math.Clamp(0, _minimum, _maximum));
        var highPoint = ValueToPoint(IsRange ? _upperValue : _lowerValue);
        using var trackPen = new Pen(Enabled ? Color.FromArgb(175, 182, 187) : SystemColors.GrayText, 4);
        using var selectedPen = new Pen(Enabled ? Color.FromArgb(0, 126, 116) : SystemColors.GrayText, 5);
        using var thumbBrush = new SolidBrush(Enabled ? Color.WhiteSmoke : SystemColors.Control);
        using var thumbPen = new Pen(Enabled ? Color.FromArgb(43, 58, 61) : SystemColors.GrayText, 2);

        e.Graphics.DrawLine(trackPen, trackStart, trackEnd);
        e.Graphics.DrawLine(selectedPen, lowPoint, highPoint);
        DrawThumb(e.Graphics, lowPoint, thumbBrush, thumbPen, !IsRange || _activeThumb == 0);
        if (IsRange) DrawThumb(e.Graphics, highPoint, thumbBrush, thumbPen, _activeThumb == 1);

        if (Focused)
        {
            var activePoint = IsRange && _activeThumb == 1 ? highPoint : lowPoint;
            var cue = new Rectangle((int)activePoint.X - 12, (int)activePoint.Y - 12, 24, 24);
            ControlPaint.DrawFocusRectangle(e.Graphics, cue);
        }
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (!Enabled || e.Button != MouseButtons.Left) return;
        Focus();
        var point = new Point(e.X, e.Y);
        _activeThumb = IsRange && Distance(point, ValueToPoint(_upperValue)) < Distance(point, ValueToPoint(_lowerValue)) ? 1 : 0;
        _dragging = true;
        Capture = true;
        SetActiveValue(PointToValue(point));
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (_dragging && Capture) SetActiveValue(PointToValue(new Point(e.X, e.Y)));
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        if (e.Button != MouseButtons.Left) return;
        _dragging = false;
        Capture = false;
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (!Enabled) return;
        if (IsRange && e.KeyCode == Keys.Space)
        {
            _activeThumb = 1 - _activeThumb;
            Invalidate();
            e.Handled = true;
            return;
        }

        var increase = Orientation == Orientation.Horizontal
            ? e.KeyCode == Keys.Right
            : e.KeyCode == Keys.Up;
        var decrease = Orientation == Orientation.Horizontal
            ? e.KeyCode == Keys.Left
            : e.KeyCode == Keys.Down;
        var step = e.KeyCode is Keys.PageUp or Keys.PageDown ? LargeStep : SmallStep;
        if (increase || e.KeyCode == Keys.PageUp)
        {
            SetActiveValue(GetActiveValue() + step);
            e.Handled = true;
        }
        else if (decrease || e.KeyCode == Keys.PageDown)
        {
            SetActiveValue(GetActiveValue() - step);
            e.Handled = true;
        }
        else if (e.KeyCode == Keys.Home)
        {
            SetActiveValue(_minimum);
            e.Handled = true;
        }
        else if (e.KeyCode == Keys.End)
        {
            SetActiveValue(_maximum);
            e.Handled = true;
        }
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

    private PointF TrackStart => Orientation == Orientation.Horizontal
        ? new PointF(ThumbRadius + 2, Height / 2f)
        : new PointF(Width / 2f, Height - ThumbRadius - 2);

    private PointF TrackEnd => Orientation == Orientation.Horizontal
        ? new PointF(Width - ThumbRadius - 2, Height / 2f)
        : new PointF(Width / 2f, ThumbRadius + 2);

    private PointF ValueToPoint(int value)
    {
        var ratio = (float)(value - _minimum) / (_maximum - _minimum);
        var start = TrackStart;
        var end = TrackEnd;
        return new PointF(start.X + (end.X - start.X) * ratio, start.Y + (end.Y - start.Y) * ratio);
    }

    private int PointToValue(Point point)
    {
        var start = TrackStart;
        var end = TrackEnd;
        var axis = Orientation == Orientation.Horizontal ? point.X : point.Y;
        var startAxis = Orientation == Orientation.Horizontal ? start.X : start.Y;
        var endAxis = Orientation == Orientation.Horizontal ? end.X : end.Y;
        var ratio = Math.Clamp((axis - startAxis) / (endAxis - startAxis), 0, 1);
        return (int)Math.Round(_minimum + ratio * (_maximum - _minimum));
    }

    private int GetActiveValue() => _activeThumb == 0 ? _lowerValue : _upperValue;

    private void SetActiveValue(int value)
    {
        var oldLower = _lowerValue;
        var oldUpper = _upperValue;
        if (!IsRange || _activeThumb == 0)
            _lowerValue = Math.Clamp(value, _minimum, IsRange ? _upperValue : _maximum);
        else
            _upperValue = Math.Clamp(value, _lowerValue, _maximum);

        if (oldLower == _lowerValue && oldUpper == _upperValue) return;
        Invalidate();
        ValuesChanged?.Invoke(this, EventArgs.Empty);
    }

    private static float Distance(Point left, PointF right)
    {
        var dx = left.X - right.X;
        var dy = left.Y - right.Y;
        return MathF.Sqrt(dx * dx + dy * dy);
    }

    private void DrawThumb(Graphics graphics, PointF center, Brush brush, Pen pen, bool active)
    {
        var radius = active ? ThumbRadius + 1 : ThumbRadius;
        graphics.FillEllipse(brush, center.X - radius, center.Y - radius, radius * 2, radius * 2);
        graphics.DrawEllipse(pen, center.X - radius, center.Y - radius, radius * 2, radius * 2);
    }
}
