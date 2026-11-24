using System.Diagnostics;
using ScriptSyncPilot.Common;

namespace ScriptSyncPilot.FunscriptEditor;

/// <summary>
/// Standalone funscript editor: draw/drag points on a zoomable timeline track.
/// Optionally syncs playback with an mpv instance — either one this editor
/// launches for the loaded video, or an already-running one such as
/// VR-Flat-Player's, attached over its IPC pipe (see MpvSession).
/// </summary>
public sealed class MainForm : Form
{
    private const double SeekPositionToleranceSeconds = 0.0005;
    private const int PendingSeekVerificationDelayMs = 200;
    private const int PendingSeekVerificationTimeoutMs = 2500;
    private const int PendingSeekQueryTimeoutMs = 500;
    private const int PendingSeekRetryDelayMs = 150;

    private readonly EditorSettings _settings;
    private readonly Func<bool> _confirmDiscardChanges;
    private readonly TimelineControl _timeline = new() { Dock = DockStyle.Fill };
    private readonly TextBox _log = new() { Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, Dock = DockStyle.Bottom, Height = 90 };
    private readonly Label _segmentSpeedReadout = new()
    {
        AutoSize = false,
        Dock = DockStyle.Fill,
        Text = "Movement speed: hover over a segment or point",
        ForeColor = SystemColors.ControlText,
        TextAlign = ContentAlignment.MiddleLeft,
        Margin = new Padding(6, 3, 3, 3),
    };
    private readonly SegmentSpeedLegend _speedLegend = new() { Dock = DockStyle.Fill, MinimumSize = new Size(220, 0) };

    private readonly TextBox _videoPathBox = new() { ReadOnly = true };
    private readonly TextBox _scriptPathBox = new() { ReadOnly = true };
    private readonly TextBox _pipeBox = new() { Width = 220 };
    private readonly Button _launchButton = new() { Text = "Launch mpv" };
    private readonly Button _attachButton = new() { Text = "Attach" };
    private readonly Button _playPauseButton = new() { Text = "Play", Enabled = false };
    private readonly Label _timeLabel = new() { AutoSize = true, Text = "0.000 / 0.000 s", MinimumSize = new Size(140, 0) };
    private readonly NumericUpDown _speedInput = new()
    {
        Minimum = 0.10m,
        Maximum = 5.00m,
        DecimalPlaces = 2,
        Increment = 0.05m,
        Value = 1.00m,
        Width = 75,
        Enabled = false,
        AccessibleName = "Playback speed multiplier",
    };
    private readonly Button _resetSpeedButton = new() { Text = "1x", AutoSize = true, Enabled = false, AccessibleName = "Reset playback speed to normal" };
    private readonly Label _speedLabel = new() { AutoSize = true, Text = "Player rate: unknown", MinimumSize = new Size(140, 0), Margin = new Padding(6, 8, 3, 0) };
    private readonly TrackBar _zoomSlider = new() { Minimum = 5, Maximum = 2000, TickStyle = TickStyle.None, Value = 100 };

    private readonly Button _back10SButton = new() { Text = "◀◀ 10s", Enabled = false };
    private readonly Button _back1SButton = new() { Text = "◀ 1s", Enabled = false };
    private readonly Button _backFrameButton = new() { Text = "◀ frame", Enabled = false };
    private readonly Button _forwardFrameButton = new() { Text = "frame ▶", Enabled = false };
    private readonly Button _forward1SButton = new() { Text = "1s ▶", Enabled = false };
    private readonly Button _forward10SButton = new() { Text = "10s ▶▶", Enabled = false };
    private readonly Button _previousPointButton = new() { Text = "Previous point", AutoSize = true, Enabled = false };
    private readonly Button _selectedPointButton = new() { Text = "Selected point", AutoSize = true, Enabled = false };
    private readonly Button _nextPointButton = new() { Text = "Next point", AutoSize = true, Enabled = false };

    private Funscript _script = new();
    private string? _scriptPath;
    private bool IsDirty => _timeline.History.IsDirty;

    private MpvSession? _mpv;
    private PendingMpvSeek? _pendingMpvSeek;
    private long _mpvSeekGeneration;
    private Process? _ownedMpvProcess;
    private string? _ownedMpvPipe;
    private CancellationTokenSource _lifetimeCts = new();
    private readonly SemaphoreSlim _playbackTransitionGate = new(1, 1);
    private bool _isClosing;
    private string? _activePipe;
    private bool _syncingZoom;
    private bool _syncingSpeed;
    private TimelineWheelFilter? _wheelFilter;
    private PatternInsertionDialog? _patternDialog;

    public MainForm() : this(EditorSettings.Load())
    {
    }

    internal MainForm(EditorSettings settings, Func<bool>? confirmDiscardChanges = null)
    {
        _settings = settings;
        _timeline.ConfigureSpeedColoring(
            _settings.SpeedColoringEnabled,
            _settings.SpeedScaleMode,
            _settings.ManualSpeedMinimumPpS,
            _settings.ManualSpeedMaximumPpS);
        _confirmDiscardChanges = confirmDiscardChanges ?? ConfirmDiscardChangesFromUser;
        Text = "ScriptSyncPilot – Funscript Editor";
        Width = 1100;
        Height = 650;

        _videoPathBox.Text = _settings.LastVideoPath;
        _pipeBox.Text = _settings.MpvIpcPipe;

        Controls.Add(_timeline);
        Controls.Add(BuildTimelineFooter());
        Controls.Add(BuildZoomBar());
        Controls.Add(BuildTransportBar());
        Controls.Add(BuildFilesBar());
        Controls.Add(_log);
        Controls.Add(BuildMenu());

        KeyPreview = true;
        KeyDown += OnFormKeyDown;

        _timeline.SeekRequested += OnSeekRequested;
        _timeline.PlayPauseRequested += async (_, _) => await TogglePlayPauseAsync();
        _timeline.ScriptChanged += (_, _) => UpdateTitle();
        _timeline.MovementReadoutChanged += UpdateMovementReadout;
        _timeline.SpeedColorScaleChanged += (_, _) => UpdateSpeedLegend();
        UpdateSpeedLegend();
        _timeline.ZoomChanged += (_, _) => SyncZoomSlider();
        _zoomSlider.ValueChanged += (_, _) =>
        {
            if (!_syncingZoom) _timeline.PixelsPerSecond = _zoomSlider.Value;
        };

        _wheelFilter = new TimelineWheelFilter(_timeline);
        Application.AddMessageFilter(_wheelFilter);

        _launchButton.Click += async (_, _) => await LaunchMpvAsync();
        _attachButton.Click += async (_, _) =>
        {
            if (_mpv is not null)
                ShutdownPlayback(terminateOwnedMpv: false);
            else
                await AttachExistingAsync();
        };
        _playPauseButton.Click += async (_, _) => await TogglePlayPauseAsync();
        _speedInput.ValueChanged += async (_, _) =>
        {
            if (!_syncingSpeed) await SetPlaybackSpeedAsync((double)_speedInput.Value);
        };
        _resetSpeedButton.Click += async (_, _) => await SetPlaybackSpeedAsync(1.0);
        _back10SButton.Click += async (_, _) => await StepAsync(-10);
        _back1SButton.Click += async (_, _) => await StepAsync(-1);
        _backFrameButton.Click += async (_, _) => await StepFrameAsync(forward: false);
        _forwardFrameButton.Click += async (_, _) => await StepFrameAsync(forward: true);
        _forward1SButton.Click += async (_, _) => await StepAsync(1);
        _forward10SButton.Click += async (_, _) => await StepAsync(10);
        _previousPointButton.Click += (_, _) => _timeline.NavigateToPreviousAction();
        _selectedPointButton.Click += (_, _) => _timeline.NavigateToSelectedAction();
        _nextPointButton.Click += (_, _) => _timeline.NavigateToNextAction();
        _timeline.NavigationAvailabilityChanged += (_, _) => UpdatePointNavigationButtons();
        UpdatePointNavigationButtons();

        foreach (var button in new[] { _launchButton, _attachButton, _playPauseButton, _resetSpeedButton,
            _back10SButton, _back1SButton, _backFrameButton, _forwardFrameButton, _forward1SButton, _forward10SButton,
            _previousPointButton, _selectedPointButton, _nextPointButton })
            button.Click += (_, _) =>
            {
                if (!_isClosing && _timeline.CanFocus) _timeline.Focus();
            };

        _timeline.LoadScript(_script);
        if (_settings.LastScriptPath.Length > 0 && File.Exists(_settings.LastScriptPath))
            LoadScriptFrom(_settings.LastScriptPath);
        UpdateTitle();

        FormClosing += (_, e) =>
        {
            if (IsDirty && !ConfirmDiscardChanges()) { e.Cancel = true; return; }
            _isClosing = true;
            _settings.LastVideoPath = _videoPathBox.Text;
            _settings.MpvIpcPipe = _pipeBox.Text;
            _settings.SpeedColoringEnabled = _timeline.SpeedColoringEnabled;
            _settings.SpeedScaleMode = _timeline.SpeedScaleMode;
            _settings.ManualSpeedMinimumPpS = _timeline.ManualSpeedColorRange.Minimum;
            _settings.ManualSpeedMaximumPpS = _timeline.ManualSpeedColorRange.Maximum;
            if (_scriptPath is not null) _settings.LastScriptPath = _scriptPath;
            _settings.Save();
            if (_wheelFilter is not null) Application.RemoveMessageFilter(_wheelFilter);
            ShutdownPlayback();
        };

        MouseDown += (_, _) => _timeline.Focus();
    }

    private Control BuildMenu()
    {
        var menu = new MenuStrip();
        var file = new ToolStripMenuItem("File");
        file.DropDownItems.Add(new ToolStripMenuItem("New Funscript", null, (_, _) => NewScript())
        {
            ShortcutKeys = Keys.Control | Keys.N,
        });
        file.DropDownItems.Add(new ToolStripMenuItem("Open Funscript…", null, (_, _) => OpenScript())
        {
            ShortcutKeys = Keys.Control | Keys.O,
        });
        file.DropDownItems.Add(new ToolStripMenuItem("Save Funscript", null, (_, _) => SaveScript(false))
        {
            ShortcutKeys = Keys.Control | Keys.S,
        });
        file.DropDownItems.Add("Save Funscript As…", null, (_, _) => SaveScript(true));
        file.DropDownItems.Add(new ToolStripSeparator());
        file.DropDownItems.Add("Open Video…", null, (_, _) => OpenVideo());
        file.DropDownItems.Add(new ToolStripSeparator());
        file.DropDownItems.Add("Exit", null, (_, _) => Close());
        menu.Items.Add(file);

        var edit = new ToolStripMenuItem("Edit");
        var undo = new ToolStripMenuItem("Undo", null, (_, _) => _timeline.Undo()) { ShortcutKeys = Keys.Control | Keys.Z };
        var redo = new ToolStripMenuItem("Redo", null, (_, _) => _timeline.Redo()) { ShortcutKeys = Keys.Control | Keys.Y };
        edit.DropDownOpening += (_, _) =>
        {
            undo.Enabled = _timeline.History.CanUndo;
            redo.Enabled = _timeline.History.CanRedo;
        };
        // Shortcuts only fire on enabled items, so re-enable once the menu closes.
        edit.DropDownClosed += (_, _) => undo.Enabled = redo.Enabled = true;
        edit.DropDownItems.Add(undo);
        edit.DropDownItems.Add(redo);
        edit.DropDownItems.Add(new ToolStripSeparator());
        edit.DropDownItems.Add("Insert Pattern…", null, (_, _) => InsertPattern());
        edit.DropDownItems.Add(new ToolStripSeparator());
        edit.DropDownItems.Add("Movement Speed Preferences…", null, (_, _) => OpenMovementSpeedPreferences());
        menu.Items.Add(edit);
        return menu;
    }

    private void OpenMovementSpeedPreferences()
    {
        using var dialog = new MovementSpeedPreferencesDialog(
            _timeline.SpeedColoringEnabled,
            _timeline.SpeedScaleMode,
            _timeline.ManualSpeedColorRange,
            _timeline.FittedSpeedColorRange,
            _script.Actions,
            _scriptPath is not null);
        if (dialog.ShowDialog(this) != DialogResult.OK) return;

        _timeline.ConfigureSpeedColoring(
            dialog.ColoringEnabled,
            dialog.ScaleMode,
            dialog.ManualMinimum,
            dialog.ManualMaximum);
    }

    private void UpdateSpeedLegend() =>
        _speedLegend.SetScale(_timeline.ActiveSpeedColorRange, _timeline.SpeedColoringEnabled);

    private void InsertPattern()
    {
        if (_patternDialog is { IsDisposed: false })
        {
            _patternDialog.Activate();
            return;
        }

        _patternDialog = new PatternInsertionDialog(
            () => _timeline.PlayheadSeconds,
            () => _script.Actions,
            _settings,
            ApplyPattern,
            OnPatternDialogKeyDown,
            MovePlayheadTo,
            _timeline.SetPatternPreview,
            _timeline.SetPatternPreviewRange);
        _patternDialog.FormClosed += (_, _) =>
        {
            _patternDialog = null;
            if (!IsDisposed && _timeline.CanFocus) _timeline.Focus();
        };
        _patternDialog.Show(this);
    }

    private bool ApplyPattern(GeneratedPattern generated)
    {
        PatternInsertionPlan plan;
        try
        {
            plan = PatternInsertionPlan.Create(_script.Actions, generated);
        }
        catch (ArgumentException ex)
        {
            var guidance = ex.ParamName == "existingActions"
                ? "Fix the loaded funscript so timestamps are non-negative and unique, and positions are between 0 and 100, then reload it and retry."
                : "Check the pattern settings, generate a new preview, and retry.";
            MessageBox.Show((IWin32Window?)_patternDialog ?? this,
                $"{ex.Message}\n\n{guidance}\n\nNo changes were made.",
                "Could not insert pattern", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return false;
        }
        var choice = PatternConflictChoice.Merge;
        var mergeChoice = PatternMergeChoice.KeepExisting;
        if (plan.HasConflicts)
        {
            using var conflictDialog = new PatternConflictDialog(plan, _settings);
            if (conflictDialog.ShowDialog((IWin32Window?)_patternDialog ?? this) != DialogResult.OK) return false;
            choice = conflictDialog.Choice;
            mergeChoice = conflictDialog.MergeChoice;
        }

        var candidate = plan.Resolve(choice, mergeChoice);
        _timeline.ReplaceActions(candidate.Actions, generated.StartMs);
        return true;
    }

    private Control BuildFilesBar()
    {
        var layout = new TableLayoutPanel { Dock = DockStyle.Top, ColumnCount = 3, AutoSize = true, Padding = new Padding(6) };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        void AddRow(string label, Control field, Control buttons)
        {
            var row = layout.RowCount++;
            layout.Controls.Add(new Label { Text = label, AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(3, 8, 3, 3) }, 0, row);
            field.Dock = DockStyle.Fill;
            layout.Controls.Add(field, 1, row);
            layout.Controls.Add(buttons, 2, row);
        }

        var openVideo = new Button { Text = "Open…", AutoSize = true };
        openVideo.Click += (_, _) => OpenVideo();
        var videoButtons = new FlowLayoutPanel { AutoSize = true, Margin = new Padding(0) };
        videoButtons.Controls.Add(openVideo);

        var openScript = new Button { Text = "Open…", AutoSize = true };
        openScript.Click += (_, _) => OpenScript();
        var newScript = new Button { Text = "New", AutoSize = true };
        newScript.Click += (_, _) => NewScript();
        var saveScript = new Button { Text = "Save", AutoSize = true };
        saveScript.Click += (_, _) => SaveScript(false);
        var saveScriptAs = new Button { Text = "Save As…", AutoSize = true };
        saveScriptAs.Click += (_, _) => SaveScript(true);
        var scriptButtons = new FlowLayoutPanel { AutoSize = true, Margin = new Padding(0) };
        scriptButtons.Controls.Add(newScript);
        scriptButtons.Controls.Add(openScript);
        scriptButtons.Controls.Add(saveScript);
        scriptButtons.Controls.Add(saveScriptAs);

        AddRow("Video", _videoPathBox, videoButtons);
        AddRow("Funscript", _scriptPathBox, scriptButtons);
        return layout;
    }

    private Control BuildTransportBar()
    {
        var bar = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, Padding = new Padding(6), FlowDirection = FlowDirection.LeftToRight };
        bar.Controls.Add(_launchButton);
        bar.Controls.Add(new Label { Text = "  mpv IPC pipe:", AutoSize = true, Margin = new Padding(12, 8, 3, 0) });
        bar.Controls.Add(_pipeBox);
        bar.Controls.Add(_attachButton);
        bar.Controls.Add(_timeLabel);
        var speedControls = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = new Padding(0) };
        speedControls.Controls.Add(new Label { Text = "Speed (x)", AutoSize = true, Margin = new Padding(12, 8, 3, 0) });
        speedControls.Controls.Add(_speedInput);
        speedControls.Controls.Add(_resetSpeedButton);
        speedControls.Controls.Add(_speedLabel);
        bar.Controls.Add(speedControls);
        return bar;
    }

    private Control BuildTimelineFooter()
    {
        var footer = new TableLayoutPanel
        {
            Dock = DockStyle.Bottom,
            AutoSize = true,
            ColumnCount = 1,
            RowCount = 3,
            Margin = new Padding(0),
            Padding = new Padding(0),
        };
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        for (var row = 0; row < footer.RowCount; row++)
            footer.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        footer.Controls.Add(BuildSegmentSpeedReadoutBar(), 0, 0);
        footer.Controls.Add(BuildPointNavigationBar(), 0, 1);
        footer.Controls.Add(BuildStepBar(), 0, 2);
        return footer;
    }

    private Control BuildSegmentSpeedReadoutBar()
    {
        var height = _segmentSpeedReadout.Font.Height * 3 + 12;
        var bar = new Panel
        {
            Dock = DockStyle.Top,
            AutoSize = false,
            Height = height,
            MinimumSize = new Size(0, height),
            Margin = new Padding(0),
        };
        var contents = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            Margin = new Padding(0),
            Padding = new Padding(0),
        };
        contents.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        contents.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 230));
        contents.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        contents.Controls.Add(_segmentSpeedReadout, 0, 0);
        contents.Controls.Add(_speedLegend, 1, 0);
        bar.Controls.Add(contents);
        return bar;
    }

    private void UpdateMovementReadout(TimelineMovementReadout? readout)
    {
        if (readout is not { } value)
        {
            _segmentSpeedReadout.Text = "Movement speed: hover over a segment or point";
            return;
        }

        if (value.Kind == TimelineMovementReadoutKind.Segment && value.Segment is { } segment)
        {
            _segmentSpeedReadout.Text = $"Movement speed (script average at 1x): {FormatSegmentMeasurement(segment)}";
            return;
        }

        if (value.Kind is TimelineMovementReadoutKind.Point or TimelineMovementReadoutKind.ValueBarPreview
            && value.PointAction is { } point)
        {
            var at = point.At.ToString(System.Globalization.CultureInfo.InvariantCulture);
            var position = point.Pos.ToString(System.Globalization.CultureInfo.InvariantCulture);
            var context = value.Kind == TimelineMovementReadoutKind.ValueBarPreview
                ? "Value bar preview at"
                : "Point at";
            _segmentSpeedReadout.Text = string.Format(System.Globalization.CultureInfo.InvariantCulture,
                "{0} {1} ms · {2} pp (script average at 1x){3}Incoming: {4}{3}Outgoing: {5}",
                context, at, position, Environment.NewLine,
                FormatSegmentMeasurement(value.HasIncomingSegment, value.IncomingSegment, "No incoming segment"),
                FormatSegmentMeasurement(value.HasOutgoingSegment, value.OutgoingSegment, "No outgoing segment"));
        }
    }

    private string FormatSegmentMeasurement(SegmentSpeed measurement) =>
        FormatSegmentMeasurement(true, measurement, "No segment");

    private string FormatSegmentMeasurement(bool exists, SegmentSpeed? measurement, string missingText)
    {
        if (!exists) return missingText;
        if (measurement is not { } value) return "Invalid timing";
        var duration = value.ElapsedMilliseconds.ToString(System.Globalization.CultureInfo.InvariantCulture);
        var distance = value.DistancePercentagePoints.ToString(System.Globalization.CultureInfo.InvariantCulture);
        if (value.AveragePercentagePointsPerSecond is not { } speed)
            return string.Format(System.Globalization.CultureInfo.InvariantCulture, "{0} ms · {1} pp · Invalid timing", duration, distance);

        var capNote = _timeline.MapSpeedColor(value)?.Clamp switch
        {
            SegmentSpeedClamp.High => " · color capped",
            SegmentSpeedClamp.Low => " · color capped low",
            _ => "",
        };
        return string.Format(System.Globalization.CultureInfo.InvariantCulture, "{0} ms · {1} pp · {2:0.0} pp/s{3}", duration, distance, speed, capNote);
    }

    private Control BuildZoomBar()
    {
        var layout = new TableLayoutPanel { Dock = DockStyle.Top, ColumnCount = 3, RowCount = 1, AutoSize = true, Padding = new Padding(6, 0, 6, 0) };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        layout.Controls.Add(new Label { Text = "Zoom", AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(3, 12, 6, 3) }, 0, 0);
        _zoomSlider.Dock = DockStyle.Fill;
        layout.Controls.Add(_zoomSlider, 1, 0);

        var fit = new Button { Text = "Fit All", AutoSize = true, Margin = new Padding(6, 8, 3, 3) };
        fit.Click += (_, _) => _timeline.ZoomToFit();
        layout.Controls.Add(fit, 2, 0);
        return layout;
    }

    // The fit-to-window zoom is the slider's floor, and it moves with the duration and the window width.
    private void SyncZoomSlider()
    {
        _syncingZoom = true;
        var min = Math.Max(1, (int)Math.Ceiling(_timeline.MinPixelsPerSecond));
        var max = Math.Max(min + 1, (int)Math.Ceiling(_timeline.MaxPixelsPerSecond));
        _zoomSlider.Maximum = max;
        _zoomSlider.Minimum = min;
        _zoomSlider.Value = (int)Math.Clamp(Math.Round(_timeline.PixelsPerSecond), min, max);
        _syncingZoom = false;
    }

    private Control BuildStepBar()
    {
        var bar = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, Padding = new Padding(6, 0, 6, 6), FlowDirection = FlowDirection.LeftToRight };
        bar.Controls.Add(_back10SButton);
        bar.Controls.Add(_back1SButton);
        bar.Controls.Add(_backFrameButton);
        bar.Controls.Add(_playPauseButton);
        bar.Controls.Add(_forwardFrameButton);
        bar.Controls.Add(_forward1SButton);
        bar.Controls.Add(_forward10SButton);
        bar.Controls.Add(new Label
        {
            Text = "  Shortcuts: Left/Right = frame, Shift = 1s, Ctrl = 10s",
            AutoSize = true,
            Margin = new Padding(12, 8, 3, 0),
            ForeColor = SystemColors.GrayText,
        });
        return bar;
    }

    private Control BuildPointNavigationBar()
    {
        var bar = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            Padding = new Padding(6, 0, 6, 6),
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
        };
        bar.Controls.Add(_previousPointButton);
        bar.Controls.Add(_selectedPointButton);
        bar.Controls.Add(_nextPointButton);
        bar.Controls.Add(new Label
        {
            Text = "  Shortcuts: Ctrl+Up = previous, Ctrl+Down = next, Ctrl+Enter = selected",
            AutoSize = true,
            Margin = new Padding(12, 8, 3, 0),
            ForeColor = SystemColors.GrayText,
        });
        return bar;
    }

    private void UpdatePointNavigationButtons()
    {
        _previousPointButton.Enabled = _timeline.CanNavigateToPreviousAction;
        _selectedPointButton.Enabled = _timeline.CanNavigateToSelectedAction;
        _nextPointButton.Enabled = _timeline.CanNavigateToNextAction;
    }

    private void Log(string message)
    {
        AppLog.Write(message);
        if (_log.IsDisposed) return;
        _log.AppendText($"[{DateTime.Now:HH:mm:ss}] {message}{Environment.NewLine}");
    }

    private void UpdateTitle()
    {
        _scriptPathBox.Text = _scriptPath ?? "";
        Text = $"ScriptSyncPilot – Funscript Editor – {(_scriptPath is null ? "untitled" : Path.GetFileName(_scriptPath))}{(IsDirty ? " *" : "")}";
    }

    private bool ConfirmDiscardChanges() => _confirmDiscardChanges();

    private bool ConfirmDiscardChangesFromUser() =>
        MessageBox.Show(this, "Discard unsaved changes?", "ScriptSyncPilot", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes;

    private void NewScript()
    {
        if (IsDirty && !ConfirmDiscardChanges()) return;
        _script = new Funscript();
        _scriptPath = null;
        _timeline.LoadScript(_script);
        _patternDialog?.InvalidatePreview();
        UpdateTitle();
    }

    private void OpenScript()
    {
        if (IsDirty && !ConfirmDiscardChanges()) return;
        using var dlg = new OpenFileDialog { Filter = "Funscript|*.funscript;*.json|All files|*.*" };
        if (dlg.ShowDialog(this) != DialogResult.OK) return;
        LoadScriptFrom(dlg.FileName);
    }

    private void LoadScriptFrom(string path)
    {
        try
        {
            _script = Funscript.Load(path);
            _scriptPath = path;
            _timeline.LoadScript(_script);
            _patternDialog?.InvalidatePreview();
            UpdateTitle();
            Log($"Loaded funscript with {_script.Actions.Count} actions from {path}.");
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException)
        {
            MessageBox.Show(this, ex.Message, "Could not open funscript", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void SaveScript(bool forcePickPath)
    {
        var path = _scriptPath;
        if (forcePickPath || path is null)
        {
            using var dlg = new SaveFileDialog { Filter = "Funscript|*.funscript", FileName = path ?? "script.funscript" };
            if (dlg.ShowDialog(this) != DialogResult.OK) return;
            path = dlg.FileName;
        }

        try
        {
            _script.Save(path);
            _scriptPath = path;
            _timeline.History.MarkSaved();
            UpdateTitle();
            Log($"Saved {_script.Actions.Count} actions to {path}.");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            MessageBox.Show(this, ex.Message, "Could not save funscript", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void OpenVideo()
    {
        using var dlg = new OpenFileDialog { Filter = "Video files|*.mp4;*.mkv;*.webm;*.avi;*.mov|All files|*.*" };
        if (dlg.ShowDialog(this) != DialogResult.OK) return;
        _videoPathBox.Text = dlg.FileName;
    }

    private async Task LaunchMpvAsync()
    {
        if (string.IsNullOrWhiteSpace(_videoPathBox.Text))
        {
            MessageBox.Show(this, "Pick a video first.", "ScriptSyncPilot");
            return;
        }

        _launchButton.Enabled = false;
        _attachButton.Enabled = false;
        var transitionAcquired = false;
        try
        {
            await _playbackTransitionGate.WaitAsync();
            transitionAcquired = true;
            ShutdownPlayback();
            var pipe = $@"\\.\pipe\ssp-funscript-editor-{Environment.ProcessId}";
            try
            {
                _ownedMpvProcess = Process.Start(new ProcessStartInfo
                {
                    FileName = _settings.MpvPath,
                    ArgumentList = { $"--input-ipc-server={pipe}", "--pause", "--speed=1", _videoPathBox.Text },
                    UseShellExecute = false,
                });
            }
            catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or IOException)
            {
                Log($"Failed to launch mpv ('{_settings.MpvPath}'): {ex.Message}");
                return;
            }

            _pipeBox.Text = pipe;
            _ownedMpvPipe = pipe;
            await AttachAsync(pipe);
        }
        finally
        {
            if (transitionAcquired) _playbackTransitionGate.Release();
            _launchButton.Enabled = !_isClosing;
            _attachButton.Enabled = !_isClosing;
        }
    }

    private async Task AttachExistingAsync()
    {
        _launchButton.Enabled = false;
        _attachButton.Enabled = false;
        var transitionAcquired = false;
        try
        {
            await _playbackTransitionGate.WaitAsync();
            transitionAcquired = true;
            var pipe = _pipeBox.Text;
            var preserveOwnedPlayer = _ownedMpvProcess is { HasExited: false }
                && string.Equals(pipe, _ownedMpvPipe, StringComparison.OrdinalIgnoreCase);
            ShutdownPlayback(terminateOwnedMpv: !preserveOwnedPlayer);
            await AttachAsync(pipe);
        }
        finally
        {
            if (transitionAcquired) _playbackTransitionGate.Release();
            _launchButton.Enabled = !_isClosing;
            _attachButton.Enabled = !_isClosing;
        }
    }

    private async Task AttachAsync(string pipe)
    {
        var lifetimeCts = new CancellationTokenSource();
        _lifetimeCts = lifetimeCts;
        var mpv = new MpvSession();
        mpv.PositionChanged += seconds => OnMpvPositionChanged(mpv, seconds);
        mpv.PauseChanged += paused => OnMpvPauseChanged(mpv, paused);
        mpv.DurationChanged += seconds => OnMpvDurationChanged(mpv, seconds);
        mpv.SpeedChanged += speed => OnMpvSpeedChanged(mpv, speed);
        mpv.FrameRateChanged += frameRate => OnMpvFrameRateChanged(mpv, frameRate);
        mpv.Disconnected += () => OnMpvDisconnected(mpv);

        try
        {
            await mpv.AttachAsync(pipe, lifetimeCts.Token);
            if (_isClosing || lifetimeCts.IsCancellationRequested || !ReferenceEquals(_lifetimeCts, lifetimeCts))
            {
                mpv.Dispose();
                return;
            }
        }
        catch (Exception ex) when (ex is IOException or TimeoutException or OperationCanceledException or ArgumentException)
        {
            Log($"Could not attach to '{pipe}': {ex.Message}");
            mpv.Dispose();
            if (ReferenceEquals(_lifetimeCts, lifetimeCts)) ShutdownPlayback();
            return;
        }

        _mpv = mpv;
        _activePipe = pipe;
        _attachButton.Text = "Detach";
        OnMpvPositionChanged(mpv, mpv.PositionSeconds);
        OnMpvPauseChanged(mpv, mpv.IsPaused);
        OnMpvDurationChanged(mpv, mpv.DurationSeconds);
        _timeline.VideoFrameRate = mpv.FrameRate;
        SetPlaybackControlsEnabled(true);
        if (mpv.Speed is { } speed) OnMpvSpeedChanged(mpv, speed);
        _ = RefreshMpvFrameRateAsync(mpv, lifetimeCts.Token);
        Log($"Attached to mpv IPC pipe '{pipe}'.");
    }

    private void SetPlaybackControlsEnabled(bool enabled)
    {
        _playPauseButton.Enabled = enabled;
        _back10SButton.Enabled = enabled;
        _back1SButton.Enabled = enabled;
        _backFrameButton.Enabled = enabled;
        _forwardFrameButton.Enabled = enabled;
        _forward1SButton.Enabled = enabled;
        _forward10SButton.Enabled = enabled;
        if (!enabled)
        {
            _speedInput.Enabled = false;
            _resetSpeedButton.Enabled = false;
            _speedLabel.Text = "Player rate: unknown";
            _syncingSpeed = true;
            try { _speedInput.Value = 1.00m; }
            finally { _syncingSpeed = false; }
        }
    }

    private void OnMpvPositionChanged(MpvSession session, double seconds)
    {
        if (IsDisposed || !IsHandleCreated) return;
        BeginInvoke(() =>
        {
            if (!ReferenceEquals(_mpv, session)) return;
            if (!ShouldApplyMpvPositionUpdate(session)) return;
            _timeline.SetPlayhead(seconds, followPlayback: !session.IsPaused);
            _timeLabel.Text = $"{seconds:0.000} / {session.DurationSeconds:0.000} s";
        });
    }

    private bool ShouldApplyMpvPositionUpdate(MpvSession session)
    {
        if (_pendingMpvSeek is not { } pending || !ReferenceEquals(pending.Session, session))
            return true;

        if (!session.IsPaused)
        {
            _pendingMpvSeek = null;
            return true;
        }

        // time-pos notifications carry no seek generation, so even a plausible value
        // may belong to an older paused seek. The correlated get_property response
        // reconciles the current generation instead.
        return false;
    }

    internal static bool IsPositionNearSeekTarget(double targetSeconds, double reportedPositionSeconds) =>
        IsPositionNearSeekTarget(targetSeconds, reportedPositionSeconds, frameRate: null);

    internal static bool IsPositionNearSeekTarget(double targetSeconds, double reportedPositionSeconds, double? frameRate)
    {
        if (!double.IsFinite(targetSeconds) || !double.IsFinite(reportedPositionSeconds)) return false;
        var tolerance = frameRate is > 0 && double.IsFinite(frameRate.Value)
            ? Math.Max(SeekPositionToleranceSeconds, 0.5 / frameRate.Value)
            : SeekPositionToleranceSeconds;
        return Math.Abs(reportedPositionSeconds - targetSeconds) <= tolerance;
    }

    private void OnMpvPauseChanged(MpvSession session, bool paused)
    {
        if (IsDisposed || !IsHandleCreated) return;
        BeginInvoke(() =>
        {
            if (!ReferenceEquals(_mpv, session)) return;
            _playPauseButton.Text = paused ? "Play" : "Pause";
            if (!paused && _pendingMpvSeek is { } pending && ReferenceEquals(pending.Session, session))
                _pendingMpvSeek = null;
        });
    }

    private void OnMpvDurationChanged(MpvSession session, double seconds)
    {
        if (IsDisposed || !IsHandleCreated) return;
        BeginInvoke(() =>
        {
            if (!ReferenceEquals(_mpv, session)) return;
            _timeline.UpdateDuration(seconds);
            _timeLabel.Text = $"{session.PositionSeconds:0.000} / {seconds:0.000} s";
        });
    }

    private async Task RefreshMpvFrameRateAsync(MpvSession session, CancellationToken cancellationToken)
    {
        try
        {
            OnMpvFrameRateChanged(session, await session.GetFrameRateAsync(cancellationToken));
        }
        catch (Exception ex) when (ex is IOException or OperationCanceledException)
        {
            // Frame-rate observations remain active; the initial query is best-effort.
        }
    }

    private void OnMpvFrameRateChanged(MpvSession session, double? frameRate)
    {
        if (IsDisposed || !IsHandleCreated) return;
        BeginInvoke(() =>
        {
            if (!ReferenceEquals(_mpv, session)) return;
            _timeline.VideoFrameRate = frameRate;
        });
    }

    private void OnMpvSpeedChanged(MpvSession session, double speed)
    {
        if (_isClosing || IsDisposed || !IsHandleCreated) return;
        try
        {
            BeginInvoke(() =>
            {
                ApplySpeedUpdateIfCurrentSession(_mpv, session, _isClosing || IsDisposed, () =>
                {
                    _syncingSpeed = true;
                    try
                    {
                        _speedInput.Value = decimal.Round((decimal)Math.Clamp(speed,
                            (double)_speedInput.Minimum, (double)_speedInput.Maximum), _speedInput.DecimalPlaces);
                        _speedLabel.Text = $"Player rate: {speed:G}x";
                        _speedInput.Enabled = true;
                        _resetSpeedButton.Enabled = true;
                    }
                    finally { _syncingSpeed = false; }
                });
            });
        }
        catch (InvalidOperationException) when (_isClosing || IsDisposed || !IsHandleCreated)
        {
        }
    }

    private void OnMpvDisconnected(MpvSession disconnectedSession)
    {
        if (IsDisposed) return;
        BeginInvoke(() =>
        {
            if (!ReferenceEquals(_mpv, disconnectedSession)) return;

            Log($"mpv disconnected ('{_activePipe}').");
            ShutdownPlayback();
        });
    }

    // Relative seek by whole seconds (positive or negative); single-frame steps go through StepFrameAsync.
    private async Task StepAsync(double deltaSeconds)
    {
        if (_mpv is null) return;
        try
        {
            await _mpv.SeekRelativeAsync(deltaSeconds, _lifetimeCts.Token);
        }
        catch (Exception ex) when (ex is IOException or OperationCanceledException)
        {
            Log($"Seek failed: {ex.Message}");
        }
    }

    private async Task StepFrameAsync(bool forward)
    {
        if (_mpv is null) return;
        try
        {
            await _mpv.StepFrameAsync(forward, _lifetimeCts.Token);
        }
        catch (Exception ex) when (ex is IOException or OperationCanceledException)
        {
            Log($"Frame step failed: {ex.Message}");
        }
    }

    private void OnFormKeyDown(object? sender, KeyEventArgs e)
    {
        // Playback stepping belongs to the timeline context. Let text fields,
        // numeric inputs, sliders, and other focused controls handle their own keys.
        if (!_timeline.Focused || _mpv is null) return;
        switch (e.KeyCode)
        {
            case Keys.Left when e.Control:
                _ = StepAsync(-10);
                break;
            case Keys.Right when e.Control:
                _ = StepAsync(10);
                break;
            case Keys.Left when e.Shift:
                _ = StepAsync(-1);
                break;
            case Keys.Right when e.Shift:
                _ = StepAsync(1);
                break;
            case Keys.Left:
                _ = StepFrameAsync(forward: false);
                break;
            case Keys.Right:
                _ = StepFrameAsync(forward: true);
                break;
            default:
                return;
        }
        e.Handled = true;
    }

    private void OnPatternDialogKeyDown(KeyEventArgs e)
    {
        if (e.Control && !e.Alt && !e.Shift)
        {
            switch (e.KeyCode)
            {
                case Keys.N:
                    NewScript();
                    break;
                case Keys.O:
                    OpenScript();
                    break;
                case Keys.S:
                    SaveScript(false);
                    break;
                default:
                    return;
            }

            e.Handled = true;
            e.SuppressKeyPress = true;
            return;
        }
    }

    private async Task TogglePlayPauseAsync()
    {
        if (_mpv is null) return;
        try
        {
            await _mpv.SetPauseAsync(!_mpv.IsPaused, _lifetimeCts.Token);
        }
        catch (Exception ex) when (ex is IOException or OperationCanceledException)
        {
            Log($"Play/pause failed: {ex.Message}");
        }
    }

    private async Task SetPlaybackSpeedAsync(double speed)
    {
        var session = _mpv;
        if (session is null || _isClosing) return;
        var token = _lifetimeCts.Token;
        await SendPlaybackSpeedAsync(
            session,
            (capturedSession, cancellationToken) => capturedSession.SetSpeedAsync(speed, cancellationToken),
            token,
            capturedSession => !_isClosing && ReferenceEquals(_mpv, capturedSession),
            Log);
    }

    internal static async Task SendPlaybackSpeedAsync<TSession>(
        TSession session,
        Func<TSession, CancellationToken, Task> sendSpeed,
        CancellationToken cancellationToken,
        Func<TSession, bool> canReportFailure,
        Action<string> reportFailure)
    {
        try
        {
            await sendSpeed(session, cancellationToken);
        }
        catch (Exception ex) when (ex is IOException or OperationCanceledException or ObjectDisposedException or ArgumentException)
        {
            if (canReportFailure(session)) reportFailure($"Speed change failed: {ex.Message}");
        }
    }

    internal static void ApplySpeedUpdateIfCurrentSession(
        object? activeSession,
        object updateSession,
        bool isClosing,
        Action applyUpdate)
    {
        if (!isClosing && ReferenceEquals(activeSession, updateSession))
            applyUpdate();
    }

    private void OnSeekRequested(double seconds)
    {
        var session = _mpv;
        if (session is null) return;
        var generation = ++_mpvSeekGeneration;
        var pending = session.IsPaused
            ? new PendingMpvSeek(session, seconds, generation)
            : null;
        _pendingMpvSeek = pending;
        _ = SeekAsync(session, seconds, pending);
    }

    private void MovePlayheadTo(double seconds)
    {
        _timeline.SetPlayhead(seconds);
        OnSeekRequested(seconds);
    }

    private async Task SeekAsync(MpvSession session, double seconds, PendingMpvSeek? pendingSeek)
    {
        try
        {
            await session.SeekAsync(seconds, _lifetimeCts.Token);
            if (pendingSeek is not null)
                _ = VerifyPendingPausedSeekAsync(pendingSeek);
        }
        catch (Exception ex) when (ex is IOException or OperationCanceledException)
        {
            Log($"Seek failed: {ex.Message}");
            if (pendingSeek is not null && IsCurrentPendingSeek(pendingSeek))
            {
                _pendingMpvSeek = null;
                _timeline.SetPlayhead(session.PositionSeconds);
            }
        }
    }

    private async Task VerifyPendingPausedSeekAsync(PendingMpvSeek pendingSeek)
    {
        try
        {
            await Task.Delay(PendingSeekVerificationDelayMs, _lifetimeCts.Token);
            var deadline = Environment.TickCount64 + PendingSeekVerificationTimeoutMs;
            var retriedSeek = false;
            double? lastActualSeconds = null;

            while (IsCurrentPendingSeek(pendingSeek) && pendingSeek.Session.IsPaused)
            {
                try
                {
                    using var queryTimeout = CancellationTokenSource.CreateLinkedTokenSource(_lifetimeCts.Token);
                    queryTimeout.CancelAfter(PendingSeekQueryTimeoutMs);
                    var actualSeconds = await pendingSeek.Session.GetPositionAsync(queryTimeout.Token);
                    if (!IsCurrentPendingSeek(pendingSeek) || !pendingSeek.Session.IsPaused) return;
                    lastActualSeconds = actualSeconds;

                    if (IsPositionNearSeekTarget(pendingSeek.TargetSeconds, actualSeconds, pendingSeek.Session.FrameRate))
                    {
                        _pendingMpvSeek = null;
                        _timeline.SetPlayhead(actualSeconds);
                        _timeLabel.Text = $"{actualSeconds:0.000} / {pendingSeek.Session.DurationSeconds:0.000} s";
                        return;
                    }
                }
                catch (OperationCanceledException) when (_lifetimeCts.IsCancellationRequested)
                {
                    return;
                }
                catch (OperationCanceledException)
                {
                    // Retry below while this seek generation remains current.
                }

                if (!IsCurrentPendingSeek(pendingSeek) || !pendingSeek.Session.IsPaused) return;
                if (!retriedSeek)
                {
                    await pendingSeek.Session.SeekAsync(pendingSeek.TargetSeconds, _lifetimeCts.Token);
                    retriedSeek = true;
                }

                if (Environment.TickCount64 >= deadline)
                {
                    if (!IsCurrentPendingSeek(pendingSeek)) return;
                    KeepRequestedPausedSeek(pendingSeek, lastActualSeconds);
                    _pendingMpvSeek = null;
                    return;
                }

                await Task.Delay(PendingSeekRetryDelayMs, _lifetimeCts.Token);
            }
        }
        catch (OperationCanceledException) when (_lifetimeCts.IsCancellationRequested)
        {
        }
        catch (Exception ex) when (ex is IOException or OperationCanceledException or ObjectDisposedException)
        {
            if (IsCurrentPendingSeek(pendingSeek))
            {
                Log($"Could not verify the paused mpv seek: {ex.Message}");
                _pendingMpvSeek = null;
            }
        }
    }

    private bool IsCurrentPendingSeek(PendingMpvSeek pendingSeek) =>
        ReferenceEquals(_pendingMpvSeek, pendingSeek)
        && pendingSeek.Generation == _mpvSeekGeneration
        && ReferenceEquals(_mpv, pendingSeek.Session);

    private void KeepRequestedPausedSeek(PendingMpvSeek pendingSeek, double? lastActualSeconds)
    {
        if (!IsCurrentPendingSeek(pendingSeek)) return;
        if (lastActualSeconds is { } actualSeconds)
            Log($"mpv did not confirm the requested point at {pendingSeek.TargetSeconds:0.000} s (last reported {actualSeconds:0.000} s); resuming position updates.");
        else
            Log("Could not verify the paused mpv seek; resuming position updates.");
    }

    private void ShutdownPlayback(bool terminateOwnedMpv = true)
    {
        _lifetimeCts.Cancel();
        _mpv?.Dispose();
        _mpv = null;
        _pendingMpvSeek = null;
        _mpvSeekGeneration++;
        _activePipe = null;
        _attachButton.Text = "Attach";
        _timeline.UpdateDuration(0);
        _timeline.VideoFrameRate = null;
        SetPlaybackControlsEnabled(false);

        if (terminateOwnedMpv && _ownedMpvProcess is { HasExited: false })
        {
            try { _ownedMpvProcess.Kill(); }
            catch (InvalidOperationException) { }
        }
        if (terminateOwnedMpv)
        {
            _ownedMpvProcess = null;
            _ownedMpvPipe = null;
        }
    }

    private sealed record PendingMpvSeek(MpvSession Session, double TargetSeconds, long Generation);
}
