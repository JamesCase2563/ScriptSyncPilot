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

    private readonly TextBox _videoPathBox = new() { ReadOnly = true, Width = 260 };
    private readonly TextBox _pipeBox = new() { Width = 220 };
    private readonly Button _launchButton = new() { Text = "Launch mpv" };
    private readonly Button _attachButton = new() { Text = "Attach" };
    private readonly Button _playPauseButton = new() { Text = "Play", Enabled = false };
    private readonly Label _timeLabel = new() { AutoSize = true, Text = "0.000 / 0.000 s", MinimumSize = new Size(140, 0) };
    private readonly TrackBar _zoomSlider = new() { Minimum = 5, Maximum = 2000, Value = 100, TickStyle = TickStyle.None, Width = 200 };

    private Funscript _script = new();
    private string? _scriptPath;
    private bool _dirty;

    private MpvSession? _mpv;
    private Process? _ownedMpvProcess;
    private CancellationTokenSource _lifetimeCts = new();
    private string? _activePipe;

    public MainForm()
    {
        Text = "ScriptSyncPilot – Funscript Editor";
        Width = 1100;
        Height = 650;

        _videoPathBox.Text = _settings.LastVideoPath;
        _pipeBox.Text = _settings.MpvIpcPipe;

        Controls.Add(_timeline);
        Controls.Add(BuildTransportBar());
        Controls.Add(_log);
        Controls.Add(BuildMenu());

        _timeline.SeekRequested += OnSeekRequested;
        _timeline.ScriptChanged += (_, _) => { _dirty = true; UpdateTitle(); };
        _zoomSlider.ValueChanged += (_, _) => _timeline.PixelsPerSecond = _zoomSlider.Value;

        _launchButton.Click += async (_, _) => await LaunchMpvAsync();
        _attachButton.Click += async (_, _) => await AttachExistingAsync();
        _playPauseButton.Click += async (_, _) => await TogglePlayPauseAsync();

        _timeline.LoadScript(_script, 60);
        UpdateTitle();

        FormClosing += (_, e) =>
        {
            if (_dirty && !ConfirmDiscardChanges()) { e.Cancel = true; return; }
            _settings.LastVideoPath = _videoPathBox.Text;
            _settings.MpvIpcPipe = _pipeBox.Text;
            if (_scriptPath is not null) _settings.LastScriptPath = _scriptPath;
            _settings.Save();
            ShutdownPlayback();
        };
    }

    private Control BuildMenu()
    {
        var menu = new MenuStrip();
        var file = new ToolStripMenuItem("File");
        file.DropDownItems.Add("New", null, (_, _) => NewScript());
        file.DropDownItems.Add("Open Funscript…", null, (_, _) => OpenScript());
        file.DropDownItems.Add("Save", null, (_, _) => SaveScript(false));
        file.DropDownItems.Add("Save As…", null, (_, _) => SaveScript(true));
        file.DropDownItems.Add(new ToolStripSeparator());
        file.DropDownItems.Add("Open Video…", null, (_, _) => OpenVideo());
        file.DropDownItems.Add(new ToolStripSeparator());
        file.DropDownItems.Add("Exit", null, (_, _) => Close());
        menu.Items.Add(file);
        return menu;
    }

    private Control BuildTransportBar()
    {
        var browseVideo = new Button { Text = "Video…" };
        browseVideo.Click += (_, _) => OpenVideo();

        var bar = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, Padding = new Padding(6), FlowDirection = FlowDirection.LeftToRight };
        bar.Controls.Add(browseVideo);
        bar.Controls.Add(_videoPathBox);
        bar.Controls.Add(_launchButton);
        bar.Controls.Add(new Label { Text = "  mpv IPC pipe:", AutoSize = true, Margin = new Padding(12, 8, 3, 0) });
        bar.Controls.Add(_pipeBox);
        bar.Controls.Add(_attachButton);
        bar.Controls.Add(_playPauseButton);
        bar.Controls.Add(_timeLabel);
        bar.Controls.Add(new Label { Text = "  Zoom:", AutoSize = true, Margin = new Padding(12, 8, 3, 0) });
        bar.Controls.Add(_zoomSlider);
        return bar;
    }

    private void Log(string message)
    {
        AppLog.Write(message);
        if (_log.IsDisposed) return;
        _log.AppendText($"[{DateTime.Now:HH:mm:ss}] {message}{Environment.NewLine}");
    }

    private void UpdateTitle() =>
        Text = $"ScriptSyncPilot – Funscript Editor – {(_scriptPath is null ? "untitled" : Path.GetFileName(_scriptPath))}{(_dirty ? " *" : "")}";

    private bool ConfirmDiscardChanges() =>
        MessageBox.Show(this, "Discard unsaved changes?", "ScriptSyncPilot", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes;

    private void NewScript()
    {
        if (_dirty && !ConfirmDiscardChanges()) return;
        _script = new Funscript();
        _scriptPath = null;
        _dirty = false;
        _timeline.LoadScript(_script, Math.Max(60, _mpv?.DurationSeconds ?? 60));
        UpdateTitle();
    }

    private void OpenScript()
    {
        if (_dirty && !ConfirmDiscardChanges()) return;
        using var dlg = new OpenFileDialog { Filter = "Funscript|*.funscript;*.json|All files|*.*" };
        if (dlg.ShowDialog(this) != DialogResult.OK) return;
        try
        {
            _script = Funscript.Load(dlg.FileName);
            _scriptPath = dlg.FileName;
            _dirty = false;
            _timeline.LoadScript(_script, Math.Max(60, _mpv?.DurationSeconds ?? 60));
            UpdateTitle();
            Log($"Loaded funscript with {_script.Actions.Count} actions from {dlg.FileName}.");
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
        _playPauseButton.Enabled = true;
        Log($"Attached to mpv IPC pipe '{pipe}'.");
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
            _playPauseButton.Enabled = false;
            _mpv = null;
        });
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
