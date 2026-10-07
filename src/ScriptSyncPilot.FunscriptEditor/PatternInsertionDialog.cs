using ScriptSyncPilot.Common;

namespace ScriptSyncPilot.FunscriptEditor;

internal sealed class PatternInsertionDialog : Form
{
    private readonly Func<double> _playheadSeconds;
    private readonly Func<IReadOnlyList<FunscriptAction>> _currentActions;
    private readonly Func<GeneratedPattern, bool> _insert;
    private readonly NumericUpDown _observedCycles = Number(12, 1, 1_000_000, 0);
    private readonly NumericUpDown _observationStart = TimeNumber();
    private readonly NumericUpDown _observationEnd = TimeNumber();
    private readonly NumericUpDown _outputStart = TimeNumber();
    private readonly NumericUpDown _outputEnd = TimeNumber();
    private readonly NumericUpDown _cycleCount = Number(5, 1, 100_000, 0);
    private readonly NumericUpDown _minimumPosition = Number(0, 0, 99, 0);
    private readonly NumericUpDown _maximumPosition = Number(100, 1, 100, 0);
    private readonly NumericUpDown _riseFraction = Number(0.25m, 0.01m, 0.99m, 2, 0.05m);
    private readonly NumericUpDown _maximumHours = Number(3, 1, 24, 0);
    private readonly ComboBox _profile = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 220 };
    private readonly ComboBox _startMode = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 220 };
    private readonly PatternGraphControl _graph = new() { Dock = DockStyle.Fill };
    private readonly Label _summary = new() { Dock = DockStyle.Fill, AutoSize = false, TextAlign = ContentAlignment.MiddleLeft };
    private readonly Button _applyButton = new() { Text = "Insert", Enabled = false, AutoSize = true };

    public GeneratedPattern? GeneratedPattern { get; private set; }
    public PatternGenerationOptions? GenerationOptions { get; private set; }

    public PatternInsertionDialog(
        Func<double> playheadSeconds,
        Func<IReadOnlyList<FunscriptAction>> currentActions,
        Func<GeneratedPattern, bool> insert)
    {
        _playheadSeconds = playheadSeconds;
        _currentActions = currentActions;
        _insert = insert;

        Text = "Insert Movement Pattern";
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MinimizeBox = false;
        MaximizeBox = false;
        ShowInTaskbar = false;
        ClientSize = new Size(900, 600);

        var playhead = PlayheadValue();
        _observationStart.Value = playhead;
        _observationEnd.Value = Math.Min(_observationEnd.Maximum, playhead + 30);
        _outputStart.Value = playhead;
        _outputEnd.Value = Math.Min(_outputEnd.Maximum, playhead + 12);

        _profile.Items.AddRange(["Triangle", "Fast up / slow down", "Slow at direction changes"]);
        _profile.SelectedIndex = 0;
        _startMode.Items.AddRange(["Pattern start", "Continue existing"]);
        _startMode.SelectedIndex = 0;

        var inputs = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            ColumnCount = 2,
            Padding = new Padding(10),
        };
        inputs.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        inputs.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        AddTimeInput(inputs, "Observation start (s)", _observationStart);
        AddTimeInput(inputs, "Observation end (s)", _observationEnd);
        AddInput(inputs, "Observed full cycles", _observedCycles);
        var suggest = new Button { Text = "Suggest cycle count", AutoSize = true, Anchor = AnchorStyles.Left };
        suggest.Click += (_, _) => SuggestCycleCount();
        inputs.Controls.Add(suggest, 0, inputs.RowCount++);
        inputs.SetColumnSpan(suggest, 2);
        AddTimeInput(inputs, "Output start (s)", _outputStart);
        AddTimeInput(inputs, "Output end (s)", _outputEnd);
        AddInput(inputs, "Output full cycles", _cycleCount);
        AddInput(inputs, "Profile", _profile);
        AddInput(inputs, "Start mode", _startMode);
        AddInput(inputs, "Minimum position", _minimumPosition);
        AddInput(inputs, "Maximum position", _maximumPosition);
        AddInput(inputs, "Fast-up rise fraction", _riseFraction);
        AddInput(inputs, "Maximum section (hours)", _maximumHours);

        var preview = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2, Padding = new Padding(10) };
        preview.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        preview.RowStyles.Add(new RowStyle(SizeType.Absolute, 72));
        preview.Controls.Add(_graph, 0, 0);
        preview.Controls.Add(_summary, 0, 1);

        var body = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1 };
        body.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 430));
        body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        body.Controls.Add(inputs, 0, 0);
        body.Controls.Add(preview, 1, 0);

        var suggestPreview = new Button { Text = "Preview", AutoSize = true };
        suggestPreview.Click += (_, _) => GeneratePreview();
        _applyButton.Click += (_, _) =>
        {
            // Regenerate against the current script; it may have been edited since the preview.
            if (GeneratePreview() && GeneratedPattern is { } generated && _insert(generated)) Close();
        };
        var cancel = new Button { Text = "Close", AutoSize = true };
        cancel.Click += (_, _) => Close();
        var footer = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            AutoSize = true,
            FlowDirection = FlowDirection.RightToLeft,
            Padding = new Padding(8),
        };
        footer.Controls.Add(cancel);
        footer.Controls.Add(_applyButton);
        footer.Controls.Add(suggestPreview);
        Controls.Add(body);
        Controls.Add(footer);
        AcceptButton = _applyButton;
        CancelButton = cancel;

        _profile.SelectedIndexChanged += (_, _) => InvalidatePreview();
        _startMode.SelectedIndexChanged += (_, _) => InvalidatePreview();
        foreach (var input in new[] { _observedCycles, _observationStart, _observationEnd, _outputStart, _outputEnd, _cycleCount, _minimumPosition, _maximumPosition, _riseFraction, _maximumHours })
            input.ValueChanged += (_, _) => InvalidatePreview();
        _profile.SelectedIndexChanged += (_, _) => _riseFraction.Enabled = _profile.SelectedIndex == 1;
    }

    private static NumericUpDown Number(decimal value, decimal minimum, decimal maximum, int decimalPlaces, decimal increment = 1) => new()
    {
        Value = value,
        Minimum = minimum,
        Maximum = maximum,
        DecimalPlaces = decimalPlaces,
        Increment = increment,
        Width = 140,
    };

    private static NumericUpDown TimeNumber() => Number(0, 0, 1_000_000, 3, 0.1m);

    private static void AddInput(TableLayoutPanel panel, string label, Control input)
    {
        var row = panel.RowCount++;
        input.Anchor = AnchorStyles.Left;
        panel.Controls.Add(new Label { Text = label, AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(3, 0, 6, 0) }, 0, row);
        panel.Controls.Add(input, 1, row);
    }

    private void AddTimeInput(TableLayoutPanel panel, string label, NumericUpDown input)
    {
        var pick = new Button { Text = "From playhead", AutoSize = true, Anchor = AnchorStyles.Left };
        pick.Click += (_, _) => input.Value = Math.Clamp(PlayheadValue(), input.Minimum, input.Maximum);
        var row = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = new Padding(0) };
        input.Anchor = AnchorStyles.Left;
        row.Controls.Add(input);
        row.Controls.Add(pick);
        AddInput(panel, label, row);
    }

    private decimal PlayheadValue() =>
        Math.Max(0, Math.Round((decimal)_playheadSeconds(), 3, MidpointRounding.AwayFromZero));

    private decimal ObservedSeconds()
    {
        var seconds = _observationEnd.Value - _observationStart.Value;
        return seconds > 0 ? seconds : throw new ArgumentException("Observation end must be after observation start.");
    }

    private decimal OutputSeconds()
    {
        var seconds = _outputEnd.Value - _outputStart.Value;
        return seconds > 0 ? seconds : throw new ArgumentException("Output end must be after output start.");
    }

    private void SuggestCycleCount()
    {
        try
        {
            _cycleCount.Value = PatternGenerator.SuggestCycleCount(_observedCycles.Value, ObservedSeconds(), OutputSeconds());
        }
        catch (Exception ex) when (ex is ArgumentException or OverflowException)
        {
            MessageBox.Show(this, ex.Message, "Cannot suggest cycle count", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private bool GeneratePreview()
    {
        try
        {
            var options = new PatternGenerationOptions(_outputStart.Value, OutputSeconds(), (int)_cycleCount.Value)
            {
                Profile = (PatternProfile)_profile.SelectedIndex,
                StartMode = (PatternStartMode)_startMode.SelectedIndex,
                MinimumPosition = (int)_minimumPosition.Value,
                MaximumPosition = (int)_maximumPosition.Value,
                RiseFraction = _riseFraction.Value,
                MaximumDurationSeconds = _maximumHours.Value * 3600m,
            };
            var generated = PatternGenerator.Generate(options, _currentActions());
            GenerationOptions = options;
            GeneratedPattern = generated;
            _graph.SetPattern(generated);
            _summary.Text = $"Observed: {PatternGenerator.ObservedCyclesPerMinute(_observedCycles.Value, ObservedSeconds()):0.##} cycles/min\r\n" +
                            $"Effective: {generated.EffectiveCyclesPerMinute:0.##} cycles/min | {generated.Actions.Count:N0} actions\r\n" +
                            $"Range: {generated.StartMs / 1000m:0.###}-{generated.EndMs / 1000m:0.###} s" +
                            (generated.UsedPatternStartFallback ? " | Continue Existing unavailable; using Pattern Start" : "");
            _applyButton.Enabled = true;
            return true;
        }
        catch (Exception ex) when (ex is ArgumentException or OverflowException)
        {
            GeneratedPattern = null;
            GenerationOptions = null;
            _graph.SetPattern(null);
            _summary.Text = ex.Message;
            _applyButton.Enabled = false;
            return false;
        }
    }

    private void InvalidatePreview()
    {
        GeneratedPattern = null;
        GenerationOptions = null;
        _graph.SetPattern(null);
        _summary.Text = "Change settings, then preview the generated movement.";
        _applyButton.Enabled = false;
    }
}

internal sealed class PatternConflictDialog : Form
{
    private static PatternMergeChoice _lastMergeChoice = PatternMergeChoice.KeepExisting;
    private readonly PatternInsertionPlan _plan;
    private readonly RadioButton _stop = new() { Text = "Stop; leave the script unchanged", AutoSize = true };
    private readonly RadioButton _replace = new() { Text = "Replace conflicting interior points", AutoSize = true };
    private readonly RadioButton _merge = new() { Text = "Merge with existing points", AutoSize = true };
    private readonly ComboBox _mergeChoice = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 180 };
    private readonly PatternGraphControl _resolvedPreview = new() { Dock = DockStyle.Fill };
    private readonly Label _previewSummary = new() { AutoSize = true, Dock = DockStyle.Fill };

    public PatternConflictChoice Choice => _replace.Checked ? PatternConflictChoice.Replace
        : _merge.Checked ? PatternConflictChoice.Merge : PatternConflictChoice.Stop;

    public PatternMergeChoice MergeChoice => (PatternMergeChoice)_mergeChoice.SelectedIndex;

    public PatternConflictDialog(PatternInsertionPlan plan)
    {
        _plan = plan;
        Text = "Pattern Conflicts";
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MinimizeBox = false;
        MaximizeBox = false;
        ClientSize = new Size(480, 430);

        _mergeChoice.Items.AddRange(["Keep existing value", "Use generated value"]);
        _mergeChoice.SelectedIndex = (int)_lastMergeChoice;
        _stop.Checked = true;
        _mergeChoice.Enabled = false;
        _stop.CheckedChanged += (_, _) => RefreshResolvedPreview();
        _replace.CheckedChanged += (_, _) => RefreshResolvedPreview();
        _merge.CheckedChanged += (_, _) =>
        {
            _mergeChoice.Enabled = _merge.Checked && plan.RequiresMergeChoice;
            RefreshResolvedPreview();
        };
        _mergeChoice.SelectedIndexChanged += (_, _) =>
        {
            _lastMergeChoice = (PatternMergeChoice)_mergeChoice.SelectedIndex;
            RefreshResolvedPreview();
        };

        var explanation = new Label
        {
            AutoSize = true,
            MaximumSize = new Size(410, 0),
            Text = $"The section contains {plan.ConflictingActions.Count} existing interior point(s). " +
                   $"{plan.Collisions.Count} generated point(s) share a timestamp with a different value. " +
                   "Existing start/end boundary points are preserved.",
        };
        var options = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            Padding = new Padding(12),
        };
        options.Controls.Add(explanation);
        options.Controls.Add(_stop);
        options.Controls.Add(_replace);
        options.Controls.Add(_merge);
        options.Controls.Add(_mergeChoice);

        var preview = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2, Padding = new Padding(12, 0, 12, 0) };
        preview.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        preview.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        preview.Controls.Add(_resolvedPreview, 0, 0);
        preview.Controls.Add(_previewSummary, 0, 1);

        var body = new Panel { Dock = DockStyle.Fill, AutoScroll = true };
        body.Controls.Add(preview);
        body.Controls.Add(options);

        var accept = new Button { Text = "Continue", DialogResult = DialogResult.OK, AutoSize = true };
        var cancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, AutoSize = true };
        var footer = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            AutoSize = true,
            FlowDirection = FlowDirection.RightToLeft,
            Padding = new Padding(8),
        };
        footer.Controls.Add(cancel);
        footer.Controls.Add(accept);
        Controls.Add(body);
        Controls.Add(footer);
        AcceptButton = accept;
        CancelButton = cancel;
        RefreshResolvedPreview();
    }

    private void RefreshResolvedPreview()
    {
        var candidate = _plan.Resolve(Choice, MergeChoice);
        _resolvedPreview.SetPreview(candidate.Actions, _plan.StartMs, _plan.EndMs);
        var count = candidate.Actions.Count(action => action.At >= _plan.StartMs && action.At <= _plan.EndMs);
        _previewSummary.Text = candidate.HasChanges
            ? $"Preview: {count:N0} destination points in the section. Existing boundary points are preserved."
            : "Preview: destination actions remain unchanged.";
    }
}

internal sealed class PatternGraphControl : Panel
{
    private IReadOnlyList<FunscriptAction>? _actions;
    private int _startMs;
    private int _endMs;

    public PatternGraphControl()
    {
        DoubleBuffered = true;
        BackColor = SystemColors.Window;
        BorderStyle = BorderStyle.FixedSingle;
    }

    public void SetPattern(GeneratedPattern? pattern)
    {
        SetPreview(pattern?.Actions, pattern?.StartMs ?? 0, pattern?.EndMs ?? 0);
    }

    public void SetPreview(IReadOnlyList<FunscriptAction>? actions, int startMs, int endMs)
    {
        _actions = actions;
        _startMs = startMs;
        _endMs = endMs;
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var graph = Rectangle.Inflate(ClientRectangle, -24, -18);
        if (graph.Width <= 0 || graph.Height <= 0) return;

        using var grid = new Pen(SystemColors.ControlLight);
        foreach (var position in new[] { 0, 25, 50, 75, 100 })
        {
            var y = graph.Bottom - position * graph.Height / 100f;
            e.Graphics.DrawLine(grid, graph.Left, y, graph.Right, y);
        }

        var actions = _actions?.Where(action => action.At >= _startMs && action.At <= _endMs).ToArray();
        if (actions is null || actions.Length < 2 || _endMs <= _startMs) return;
        using var curve = new Pen(Color.RoyalBlue, 2);
        var duration = _endMs - _startMs;
        for (var index = 1; index < actions.Length; index++)
        {
            var previous = actions[index - 1];
            var current = actions[index];
            var x1 = graph.Left + (previous.At - _startMs) * (double)graph.Width / duration;
            var x2 = graph.Left + (current.At - _startMs) * (double)graph.Width / duration;
            var y1 = graph.Bottom - previous.Pos * (double)graph.Height / 100;
            var y2 = graph.Bottom - current.Pos * (double)graph.Height / 100;
            e.Graphics.DrawLine(curve, (float)x1, (float)y1, (float)x2, (float)y2);
        }
    }
}