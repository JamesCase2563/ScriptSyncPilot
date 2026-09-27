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
    private readonly EditorSettings _settings = EditorSettings.Load();
    private readonly TimelineControl _timeline = new() { Dock = DockStyle.Fill };
    private readonly TextBox _log = new() { Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, Dock = DockStyle.Fill, Height = 90 };

    private readonly TextBox _videoPathBox = new() { ReadOnly = true };
    private readonly TextBox _scriptPathBox = new() { ReadOnly = true };
    private readonly TextBox _pipeBox = new() { Width = 220 };
    private readonly Button _launchButton = new() { Text = "Launch mpv" };
    private readonly Button _attachButton = new() { Text = "Attach" };
    private readonly Button _playPauseButton = new() { Text = "Play", Enabled = false };
    private readonly Label _timeLabel = new() { AutoSize = true, Text = "0.000 / 0.000 s", MinimumSize = new Size(140, 0) };
    private readonly TrackBar _zoomSlider = new() { Minimum = 5, Maximum = 2000, TickStyle = TickStyle.None, Value = 100 };

    private readonly Button _back10SButton = new() { Text = "◀◀ 10s", Enabled = false };
    private readonly Button _back1SButton = new() { Text = "◀ 1s", Enabled = false };
    private readonly Button _backFrameButton = new() { Text = "◀ frame", Enabled = false };
    private readonly Button _forwardFrameButton = new() { Text = "frame ▶", Enabled = false };
    private readonly Button _forward1SButton = new() { Text = "1s ▶", Enabled = false };
    private readonly Button _forward10SButton = new() { Text = "10s ▶▶", Enabled = false };

    private Funscript _script = new();
    private string? _scriptPath;
    private bool _dirty;

    private MpvSession? _mpv;
    private Process? _ownedMpvProcess;
    private CancellationTokenSource _lifetimeCts = new();
    private string? _activePipe;
    private bool _syncingZoom;
    private TimelineWheelFilter? _wheelFilter;

    public MainForm()
    {
        Text = "ScriptSyncPilot – Funscript Editor";
        Width = 1100;
        Height = 650;

        _videoPathBox.Text = _settings.LastVideoPath;
        _pipeBox.Text = _settings.MpvIpcPipe;

        Controls.Add(_timeline);
        Controls.Add(BuildStepBar());
        Controls.Add(BuildZoomBar());
        Controls.Add(BuildTransportBar());
        Controls.Add(BuildFilesBar());
        Controls.Add(_log);
        Controls.Add(BuildMenu());

        KeyPreview = true;
        KeyDown += OnFormKeyDown;

        _timeline.SeekRequested += OnSeekRequested;
        _timeline.ScriptChanged += (_, _) => { _dirty = true; UpdateTitle(); };
        _timeline.ZoomChanged += (_, _) => SyncZoomSlider();
        _zoomSlider.ValueChanged += (_, _) =>
        {
            if (!_syncingZoom) _timeline.PixelsPerSecond = _zoomSlider.Value;
        };

        _wheelFilter = new TimelineWheelFilter(_timeline);
        Application.AddMessageFilter(_wheelFilter);

        _launchButton.Click += async (_, _) => await LaunchMpvAsync();
        _attachButton.Click += async (_, _) => await AttachExistingAsync();
        _playPauseButton.Click += async (_, _) => await TogglePlayPauseAsync();
        _back10SButton.Click += async (_, _) => await StepAsync(-10);
        _back1SButton.Click += async (_, _) => await StepAsync(-1);
        _backFrameButton.Click += async (_, _) => await StepFrameAsync(forward: false);
        _forwardFrameButton.Click += async (_, _) => await StepFrameAsync(forward: true);
        _forward1SButton.Click += async (_, _) => await StepAsync(1);
        _forward10SButton.Click += async (_, _) => await StepAsync(10);

        _timeline.LoadScript(_script);
        if (_settings.LastScriptPath.Length > 0 && File.Exists(_settings.LastScriptPath))
            LoadScriptFrom(_settings.LastScriptPath);
        UpdateTitle();

        FormClosing += (_, e) =>
        {
            if (_dirty && !ConfirmDiscardChanges()) { e.Cancel = true; return; }
            _settings.LastVideoPath = _videoPathBox.Text;
            _settings.MpvIpcPipe = _pipeBox.Text;
            if (_scriptPath is not null) _settings.LastScriptPath = _scriptPath;
            _settings.Save();
            if (_wheelFilter is not null) Application.RemoveMessageFilter(_wheelFilter);
            ShutdownPlayback();
        };
    }

    private Control BuildMenu()
    {
        var menu = new MenuStrip();
        var file = new ToolStripMenuItem("File");
        file.DropDownItems.Add("New Funscript", null, (_, _) => NewScript());
        file.DropDownItems.Add("Open Funscript…", null, (_, _) => OpenScript());
        file.DropDownItems.Add("Save Funscript", null, (_, _) => SaveScript(false));
        file.DropDownItems.Add("Save Funscript As…", null, (_, _) => SaveScript(true));
        file.DropDownItems.Add(new ToolStripSeparator());
        file.DropDownItems.Add("Open Video…", null, (_, _) => OpenVideo());
        file.DropDownItems.Add(new ToolStripSeparator());
        file.DropDownItems.Add("Exit", null, (_, _) => Close());
        menu.Items.Add(file);
        return menu;
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
        bar.Controls.Add(_playPauseButton);
        bar.Controls.Add(_timeLabel);
        return bar;
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

    private void Log(string message)
    {
        AppLog.Write(message);
        if (_log.IsDisposed) return;
        _log.AppendText($"[{DateTime.Now:HH:mm:ss}] {message}{Environment.NewLine}");
    }

    private void UpdateTitle()
    {
        _scriptPathBox.Text = _scriptPath ?? "";
        Text = $"ScriptSyncPilot – Funscript Editor – {(_scriptPath is null ? "untitled" : Path.GetFileName(_scriptPath))}{(_dirty ? " *" : "")}";
    }

    private bool ConfirmDiscardChanges() =>
        MessageBox.Show(this, "Discard unsaved changes?", "ScriptSyncPilot", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes;

    private void NewScript()
    {
        if (_dirty && !ConfirmDiscardChanges()) return;
        _script = new Funscript();
        _scriptPath = null;
        _dirty = false;
        _timeline.LoadScript(_script);
        UpdateTitle();
    }

    private void OpenScript()
    {
        if (_dirty && !ConfirmDiscardChanges()) return;
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
            _dirty = false;
            _timeline.LoadScript(_script);
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
            _dirty = false;
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

        ShutdownPlayback();
        var pipe = $@"\\.\pipe\ssp-funscript-editor-{Environment.ProcessId}";
        try
        {
            _ownedMpvProcess = Process.Start(new ProcessStartInfo
            {
                FileName = _settings.MpvPath,
                ArgumentList = { $"--input-ipc-server={pipe}", "--pause", _videoPathBox.Text },
                UseShellExecute = false,
            });
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or IOException)
        {
            Log($"Failed to launch mpv ('{_settings.MpvPath}'): {ex.Message}");
            return;
        }

        _pipeBox.Text = pipe;
        await AttachAsync(pipe);
    }

    private async Task AttachExistingAsync()
    {
        ShutdownPlayback();
        await AttachAsync(_pipeBox.Text);
    }

    private async Task AttachAsync(string pipe)
    {
        _lifetimeCts = new CancellationTokenSource();
        var mpv = new MpvSession();
        mpv.PositionChanged += OnMpvPositionChanged;
        mpv.PauseChanged += OnMpvPauseChanged;
        mpv.DurationChanged += OnMpvDurationChanged;
        mpv.Disconnected += OnMpvDisconnected;

        try
        {
            await mpv.AttachAsync(pipe, _lifetimeCts.Token);
        }
        catch (Exception ex) when (ex is IOException or TimeoutException or OperationCanceledException)
        {
            Log($"Could not attach to '{pipe}': {ex.Message}");
            mpv.Dispose();
            return;
        }

        _mpv = mpv;
        _activePipe = pipe;
        SetPlaybackControlsEnabled(true);
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
    }

    private void OnMpvPositionChanged(double seconds)
    {
        if (IsDisposed) return;
        BeginInvoke(() =>
        {
            _timeline.SetPlayhead(seconds);
            _timeLabel.Text = $"{seconds:0.000} / {(_mpv?.DurationSeconds ?? 0):0.000} s";
        });
    }

    private void OnMpvPauseChanged(bool paused)
    {
        if (IsDisposed) return;
        BeginInvoke(() => _playPauseButton.Text = paused ? "Play" : "Pause");
    }

    private void OnMpvDurationChanged(double seconds)
    {
        if (IsDisposed) return;
        BeginInvoke(() => _timeline.UpdateDuration(seconds));
    }

    private void OnMpvDisconnected()
    {
        if (IsDisposed) return;
        BeginInvoke(() =>
        {
            Log($"mpv disconnected ('{_activePipe}').");
            SetPlaybackControlsEnabled(false);
            _mpv = null;
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
        if (_mpv is null) return;
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

    private void OnSeekRequested(double seconds)
    {
        if (_mpv is null) return;
        _ = SeekAsync(seconds);
    }

    private async Task SeekAsync(double seconds)
    {
        try
        {
            await _mpv!.SeekAsync(seconds, _lifetimeCts.Token);
        }
        catch (Exception ex) when (ex is IOException or OperationCanceledException)
        {
            Log($"Seek failed: {ex.Message}");
        }
    }

    private void ShutdownPlayback()
    {
        _lifetimeCts.Cancel();
        _mpv?.Dispose();
        _mpv = null;
        _playPauseButton.Enabled = false;

        if (_ownedMpvProcess is { HasExited: false })
        {
            try { _ownedMpvProcess.Kill(); }
            catch (InvalidOperationException) { }
        }
        _ownedMpvProcess = null;
    }
}
