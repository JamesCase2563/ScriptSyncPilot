using System.Globalization;

namespace ScriptSyncPilot.FunscriptEditor;

public sealed class SegmentSpeedLegend : Control
{
    private SegmentSpeedColorRange _range = new(0, 100);
    private bool _coloringEnabled = true;

    public SegmentSpeedLegend()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        AccessibleName = "Movement speed color legend";
        AccessibleDescription = "Shows the script-space speed color range in percentage points per second.";
        TabStop = false;
        BackColor = SystemColors.Control;
        ForeColor = SystemColors.ControlText;
    }

    public void SetScale(SegmentSpeedColorRange range, bool coloringEnabled)
    {
        _range = range;
        _coloringEnabled = coloringEnabled;
        AccessibleDescription = SystemInformation.HighContrast
            ? "High contrast mode uses the system highlight color for segments. Hover over a segment to read its numeric speed."
            : $"Segment colors range from {_range.Minimum.ToString("0.##", CultureInfo.InvariantCulture)} to {_range.Maximum.ToString("0.##", CultureInfo.InvariantCulture)} percentage points per second.";
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var graphics = e.Graphics;
        graphics.Clear(BackColor);
        var highContrast = SystemInformation.HighContrast;
        var textColor = highContrast ? SystemColors.ControlText : ForeColor;
        using var textBrush = new SolidBrush(textColor);
        using var borderPen = new Pen(highContrast ? SystemColors.WindowText : SystemColors.ControlDark);
        using var font = new Font(Font.FontFamily, Math.Max(7, Font.Size - 1), FontStyle.Regular);

        if (!_coloringEnabled)
        {
            graphics.DrawString("Speed colors off", font, textBrush, new PointF(4, Math.Max(0, (Height - font.Height) / 2)));
            return;
        }

        graphics.DrawString(highContrast ? "Speed · sqrt scale · system color" : "Speed (pp/s) · sqrt scale", font, textBrush, 3, 1);
        var bar = new Rectangle(4, font.Height + 3, Math.Max(1, Width - 8), Math.Max(6, font.Height / 2));
        if (highContrast)
        {
            using var systemBrush = new SolidBrush(SystemColors.Highlight);
            graphics.FillRectangle(systemBrush, bar);
        }
        else
        {
            for (var x = 0; x < bar.Width; x++)
            {
                var linearPosition = bar.Width <= 1 ? 0 : x / (double)(bar.Width - 1);
                var speed = _range.Minimum + linearPosition * (_range.Maximum - _range.Minimum);
                var mapped = _range.Map(speed);
                using var brush = new SolidBrush(SegmentSpeedPalette.GetColor(mapped.NormalizedPosition));
                graphics.FillRectangle(brush, bar.X + x, bar.Y, 1, bar.Height);
            }
        }
        graphics.DrawRectangle(borderPen, bar);

        var minimum = _range.Minimum.ToString("0.##", CultureInfo.InvariantCulture);
        var maximum = _range.Maximum.ToString("0.##", CultureInfo.InvariantCulture);
        var labelsY = Math.Min(Height - font.Height, bar.Bottom + 1);
        graphics.DrawString(minimum, font, textBrush, 3, labelsY);
        var maximumSize = graphics.MeasureString(maximum, font);
        graphics.DrawString(maximum, font, textBrush, Math.Max(4, Width - maximumSize.Width - 3), labelsY);
    }

    protected override void OnSystemColorsChanged(EventArgs e)
    {
        base.OnSystemColorsChanged(e);
        BackColor = SystemColors.Control;
        ForeColor = SystemColors.ControlText;
        SetScale(_range, _coloringEnabled);
    }
}
