using System.Globalization;
using ScriptSyncPilot.Common;

namespace ScriptSyncPilot.HandySync;

internal sealed class SpeedColorPreferencesDialog : Form
{
    private const decimal MaximumSpeed = 100_000_000m;

    private readonly IReadOnlyList<FunscriptAction> _actions;
    private readonly CheckBox _enabled = new() { Text = "Color waveform columns by script speed", AutoSize = true };
    private readonly RadioButton _fit = new() { Text = "Fit to script (P99)", AutoSize = true };
    private readonly RadioButton _manual = new() { Text = "Manual range", AutoSize = true };
    private readonly NumericUpDown _minimum = CreateSpeedInput();
    private readonly NumericUpDown _maximum = CreateSpeedInput();
    private readonly Label _fitRange = new() { AutoSize = true, ForeColor = SystemColors.ControlText };

    public bool ColoringEnabled => _enabled.Checked;
    public SegmentSpeedScaleMode ScaleMode => _manual.Checked ? SegmentSpeedScaleMode.Manual : SegmentSpeedScaleMode.FitToScript;
    public double ManualMinimum => (double)_minimum.Value;
    public double ManualMaximum => (double)_maximum.Value;

    public SpeedColorPreferencesDialog(
        bool enabled,
        SegmentSpeedScaleMode mode,
        SegmentSpeedColorRange manualRange,
        SegmentSpeedColorRange fitRange,
        IReadOnlyList<FunscriptAction> actions,
        bool hasScript)
    {
        _actions = actions;
        Text = "Speed Color Preferences";
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        AutoScaleMode = AutoScaleMode.Dpi;
        ClientSize = new Size(480, 300);

        _enabled.Checked = enabled;
        _fit.Checked = mode == SegmentSpeedScaleMode.FitToScript;
        _manual.Checked = mode == SegmentSpeedScaleMode.Manual;
        _fit.Enabled = hasScript;
        _minimum.Value = ToInputValue(manualRange.Minimum);
        _maximum.Value = ToInputValue(manualRange.Maximum);
        _fitRange.Text = hasScript ? FormatRange(fitRange) : "Fit to script is available after loading a script.";

        var content = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 7,
            Padding = new Padding(14),
        };
        content.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        for (var row = 0; row < 6; row++) content.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        content.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        content.Controls.Add(_enabled, 0, 0);
        content.SetColumnSpan(_enabled, 2);
        content.Controls.Add(_fit, 0, 1);
        content.SetColumnSpan(_fit, 2);

        var fitButton = new Button { Text = "Fit to current script", AutoSize = true, Enabled = hasScript };
        fitButton.Click += (_, _) =>
        {
            _fit.Checked = true;
            _fitRange.Text = FormatRange(SegmentSpeedScaleCalculator.FitToScript(_actions));
        };
        content.Controls.Add(fitButton, 0, 2);
        content.Controls.Add(_fitRange, 1, 2);

        content.Controls.Add(_manual, 0, 3);
        content.SetColumnSpan(_manual, 2);
        content.Controls.Add(new Label { Text = "Minimum (pp/s)", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 4);
        content.Controls.Add(_minimum, 1, 4);
        content.Controls.Add(new Label { Text = "Maximum (pp/s)", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 5);
        content.Controls.Add(_maximum, 1, 5);
        var help = new Label
        {
            Text = "Fit uses all valid script segments. Each waveform column shows the fastest valid overlapping segment. Colors describe script speed, not device capability.",
            AutoSize = true,
            MaximumSize = new Size(430, 0),
            ForeColor = SystemColors.ControlText,
        };
        content.Controls.Add(help, 0, 6);
        content.SetColumnSpan(help, 2);

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            AutoSize = true,
            FlowDirection = FlowDirection.RightToLeft,
            Padding = new Padding(8),
        };
        var ok = new Button { Text = "OK", AutoSize = true };
        var cancel = new Button { Text = "Cancel", AutoSize = true, DialogResult = DialogResult.Cancel };
        ok.Click += (_, _) => AcceptChanges();
        buttons.Controls.Add(cancel);
        buttons.Controls.Add(ok);
        Controls.Add(content);
        Controls.Add(buttons);
        AcceptButton = ok;
        CancelButton = cancel;

        _manual.CheckedChanged += (_, _) => UpdateManualInputs();
        UpdateManualInputs();
    }

    private void AcceptChanges()
    {
        if (_manual.Checked && _minimum.Value >= _maximum.Value)
        {
            MessageBox.Show(this, "Minimum speed must be lower than maximum speed.",
                "Invalid speed range", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            _minimum.Focus();
            return;
        }
        DialogResult = DialogResult.OK;
        Close();
    }

    private void UpdateManualInputs()
    {
        _minimum.Enabled = _manual.Checked;
        _maximum.Enabled = _manual.Checked;
    }

    private static NumericUpDown CreateSpeedInput() => new()
    {
        DecimalPlaces = 1,
        Increment = 10,
        Minimum = 0,
        Maximum = MaximumSpeed,
        Width = 150,
        ThousandsSeparator = true,
    };

    private static decimal ToInputValue(double value) => (decimal)Math.Clamp(value, 0, (double)MaximumSpeed);

    private static string FormatRange(SegmentSpeedColorRange range) =>
        string.Format(CultureInfo.InvariantCulture, "Current fit: {0:0.##}–{1:0.##} pp/s", range.Minimum, range.Maximum);
}
