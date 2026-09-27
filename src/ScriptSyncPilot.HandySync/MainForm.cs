using System.Diagnostics;
using ScriptSyncPilot.Common;

namespace ScriptSyncPilot.HandySync;

/// <summary>
/// Single-window app: follows an mpv instance's playhead and, when a Handy is
/// connected, drives it via HSSP so the script stays in sync (play/pause/seeks
/// all re-sync; a periodic synctime call corrects drift).
///
/// Script loading / mpv attach and the Handy connection are deliberately
/// independent: the session runs and the waveform tracks playback with no
/// device present, and a Handy can be connected or dropped mid-session.
/// </summary>
public sealed class MainForm : Form
{
    private readonly TextBox _connectionKeyBox = new() { PlaceholderText = "Device connection key" };
    private readonly TextBox _mpvPipeBox = new() { PlaceholderText = @"mpv IPC pipe, e.g. \\.\pipe\vrheadtrack" };
    private readonly TextBox _scriptPathBox = new() { ReadOnly = true };
    private readonly TrackBar _offsetSlider = new() { Minimum = -1000, Maximum = 1000, TickFrequency = 100, SmallChange = 10, LargeChange = 100 };
    private readonly Label _offsetValue = new() { AutoSize = true, TextAlign = ContentAlignment.MiddleCenter, MinimumSize = new Size(72, 0) };
    private readonly Button _startButton = new() { Text = "Start Session" };
    private readonly Button _stopButton = new() { Text = "Stop Session", Enabled = false };
    private readonly Button _connectButton = new() { Text = "Connect Handy" };
    private readonly Button _disconnectButton = new() { Text = "Disconnect Handy", Enabled = false };
    private readonly TextBox _log = new() { Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, Dock = DockStyle.Fill };
    private readonly Label _status = new() { Text = "Idle", AutoSize = true };
    private readonly ScriptWaveformControl _waveform = new() { Dock = DockStyle.Bottom, Height = 90 };
    private readonly System.Windows.Forms.Timer _syncTimer = new() { Interval = 3000 };

    private readonly AppSettings _settings = AppSettings.Load();

    private HandyApiClient? _handy;
    private MpvSession? _mpv;
    private Funscript? _script;
    private CancellationTokenSource? _offsetChangeCts;
    private long _clockOffsetMs;
    private bool _hsspReady;
    private readonly CancellationTokenSource _lifetimeCts = new();

    public MainForm()
    {
        Text = "ScriptSyncPilot – Handy Sync";
        Width = 720;
        Height = 560;

        _connectionKeyBox.Text = _settings.ConnectionKey;
        _mpvPipeBox.Text = _settings.MpvIpcPipe;
        _scriptPathBox.Text = _settings.LastScriptPath;
        if (_scriptPathBox.Text.Length > 0 && File.Exists(_scriptPathBox.Text))
            LoadScript(_scriptPathBox.Text);
        _offsetSlider.Value = Math.Clamp(_settings.OffsetMs, _offsetSlider.Minimum, _offsetSlider.Maximum);
        UpdateOffsetLabel();

        var browseScript = new Button { Text = "Browse…" };
        browseScript.Click += (_, _) =>
        {
            using var dlg = new OpenFileDialog { Filter = "Funscript|*.funscript;*.json|All files|*.*" };
            if (dlg.ShowDialog(this) == DialogResult.OK)
            {
                _scriptPathBox.Text = dlg.FileName;
                LoadScript(dlg.FileName);
            }
        };

        var layout = new TableLayoutPanel { Dock = DockStyle.Top, ColumnCount = 3, AutoSize = true, Padding = new Padding(8) };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        void AddRow(string label, Control field, Control? extra = null)
        {
            var row = layout.RowCount++;
            layout.Controls.Add(new Label { Text = label, AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(3, 8, 3, 3) }, 0, row);
            field.Dock = DockStyle.Fill;
            layout.Controls.Add(field, 1, row);
            if (extra != null) layout.Controls.Add(extra, 2, row);
        }

        AddRow("Connection key", _connectionKeyBox);
        AddRow("mpv IPC pipe", _mpvPipeBox);
        AddRow("Funscript", _scriptPathBox, browseScript);
        AddRow("Offset (ms)", _offsetSlider, _offsetValue);

        var buttons = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, Padding = new Padding(8) };
        buttons.Controls.Add(_startButton);
        buttons.Controls.Add(_stopButton);
        buttons.Controls.Add(_connectButton);
        buttons.Controls.Add(_disconnectButton);
        buttons.Controls.Add(_status);

        Controls.Add(_log);
        Controls.Add(_waveform);
        Controls.Add(buttons);
        Controls.Add(layout);

        _startButton.Click += async (_, _) => await StartAsync();
        _stopButton.Click += (_, _) => Stop();
        _connectButton.Click += async (_, _) => await ConnectHandyAsync();
        _disconnectButton.Click += (_, _) => DisconnectHandy();
        _waveform.SeekRequested += OnWaveformSeekRequested;
        _offsetSlider.ValueChanged += async (_, _) =>
        {
            UpdateOffsetLabel();
            await OnOffsetChangedAsync();
        };
        _syncTimer.Tick += async (_, _) => await OnSyncTickAsync();

        FormClosing += (_, _) =>
        {
            _settings.ConnectionKey = _connectionKeyBox.Text;
            _settings.MpvIpcPipe = _mpvPipeBox.Text;
            _settings.LastScriptPath = _scriptPathBox.Text;
            _settings.OffsetMs = _offsetSlider.Value;
            _settings.Save();
            Stop();
            DisconnectHandy();
        };
    }

    private void Log(string message)
    {
        AppLog.Write(message);
        if (!IsDisposed && IsHandleCreated)
            BeginInvoke(() => _log.AppendText($"[{DateTime.Now:HH:mm:ss}] {message}{Environment.NewLine}"));
    }

    private void SetStatus(string text) => BeginInvoke(() => _status.Text = text);

    private void LoadScript(string path)
    {
        try
        {
            _script = Funscript.Load(path);
            _waveform.SetScript(_script);
            Log($"Loaded funscript with {_script.Actions.Count} actions.");
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException)
        {
            _script = null;
            _waveform.SetScript(null);
            Log($"Could not load script: {ex.Message}");
            return;
        }

        // The device keeps whatever hssp/setup last gave it, so a script swap has to be pushed or it plays the old one.
        if (_hsspReady) _ = ReSetupScriptAsync();
    }

    private async Task ReSetupScriptAsync()
    {
        if (_handy is null || _script is null) return;
        try
        {
            await _handy.HsspStopAsync(_lifetimeCts.Token);
            await SetupScriptOnDeviceAsync();
            if (_mpv is { IsPaused: false }) await OnMpvPauseChangedAsync(false);
        }
        catch (Exception ex) when (ex is HandyApiException or IOException or HttpRequestException or TaskCanceledException)
        {
            Log($"Could not push new script to device: {ex.Message}");
            BeginInvoke(DisconnectHandy);
        }
    }

    private async Task SetupScriptOnDeviceAsync()
    {
        SetStatus("Uploading script…");
        var url = await _handy!.UploadScriptCsvAsync(_script!.ToCsv(), _lifetimeCts.Token);
        Log($"Script hosted at {url}");

        await _handy.HsspSetupAsync(url, _lifetimeCts.Token);
        Log("HSSP setup complete.");
    }

    /// <summary>Attaches to mpv and follows its playhead. Does not require a Handy.</summary>
    private async Task StartAsync()
    {
        if (_script is null)
        {
            MessageBox.Show(this, "Pick a funscript first.", "ScriptSyncPilot");
            return;
        }

        _startButton.Enabled = false;
        try
        {
            SetStatus("Attaching to mpv…");
            _mpv = new MpvSession();
            _mpv.PauseChanged += async paused => await OnMpvPauseChangedAsync(paused);
            _mpv.PositionChanged += async pos => await OnMpvPositionChangedAsync(pos);
            _mpv.DurationChanged += OnMpvDurationChanged;
            _mpv.Disconnected += () => BeginInvoke(Stop);
            await _mpv.AttachAsync(_mpvPipeBox.Text.Trim(), _lifetimeCts.Token);
            Log("Attached to mpv IPC pipe.");

            _syncTimer.Start();
            _stopButton.Enabled = true;
            SetStatus(_hsspReady ? "Playing" : "Playing (no Handy)");

            if (_hsspReady && _mpv is { IsPaused: false })
                await OnMpvPauseChangedAsync(false);
        }
        catch (Exception ex) when (ex is IOException or TaskCanceledException)
        {
            Log($"Error: {ex.Message}");
            SetStatus("Error");
            Stop();
        }
        finally
        {
            _startButton.Enabled = _mpv is null;
        }
    }

    /// <summary>Connects the device and prepares HSSP. Independent of whether a session is running.</summary>
    private async Task ConnectHandyAsync()
    {
        if (_script is null)
        {
            MessageBox.Show(this, "Pick a funscript first.", "ScriptSyncPilot");
            return;
        }

        _connectButton.Enabled = false;
        try
        {
            SetStatus("Connecting to Handy…");
            _handy = new HandyApiClient(_connectionKeyBox.Text.Trim());

            if (!await _handy.IsConnectedAsync(_lifetimeCts.Token))
                throw new HandyApiException("Device reports not connected. Check the connection key.");
            Log("Handy device connected.");

            await _handy.SetModeAsync(mode: 1, _lifetimeCts.Token); // HSSP
            Log("Device set to HSSP mode.");

            await SetupScriptOnDeviceAsync();

            SetStatus("Measuring clock offset…");
            _clockOffsetMs = await _handy.EstimateClockOffsetMsAsync(samples: 15, _lifetimeCts.Token);
            Log($"Estimated client-server clock offset: {_clockOffsetMs} ms.");

            _hsspReady = true;
            _disconnectButton.Enabled = true;
            SetStatus(_mpv is null ? "Handy ready" : "Playing");

            if (_mpv is { IsPaused: false })
                await OnMpvPauseChangedAsync(false);
        }
        catch (Exception ex) when (ex is HandyApiException or IOException or HttpRequestException or TaskCanceledException)
        {
            Log($"Handy error: {ex.Message}");
            SetStatus("Handy error");
            DisconnectHandy();
        }
        finally
        {
            _connectButton.Enabled = !_hsspReady;
        }
    }

    private void DisconnectHandy()
    {
        _offsetChangeCts?.Cancel();
        _offsetChangeCts?.Dispose();
        _offsetChangeCts = null;
        if (_hsspReady) _ = _handy?.HsspStopAsync(CancellationToken.None);
        _hsspReady = false;
        _handy?.Dispose();
        _handy = null;
        _disconnectButton.Enabled = false;
        _connectButton.Enabled = true;
        SetStatus(_mpv is null ? "Idle" : "Playing (no Handy)");
    }

    private void OnMpvDurationChanged(double seconds)
    {
        if (IsDisposed || !IsHandleCreated) return;
        BeginInvoke(() => _waveform.SetMediaDuration(seconds));
    }

    private void OnWaveformSeekRequested(double seconds)
    {
        if (_mpv is null) return;
        _ = SeekMpvAsync(seconds);
    }

    private async Task SeekMpvAsync(double seconds)
    {
        try
        {
            await _mpv!.SeekAsync(seconds, _lifetimeCts.Token);
        }
        catch (Exception ex) when (ex is IOException or ObjectDisposedException or TaskCanceledException)
        {
            Log($"Seek failed: {ex.Message}");
        }
    }

    private long EstimatedServerTimeMs() =>
        DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + _clockOffsetMs;
    private long DeviceTimeMs(double mpvPositionSeconds) =>
        (long)(mpvPositionSeconds * 1000) + _offsetSlider.Value;

    private void UpdateOffsetLabel() => _offsetValue.Text = $"{_offsetSlider.Value:+#;-#;0} ms";

    private double _lastKnownPos;
    private readonly Stopwatch _sincePosUpdate = Stopwatch.StartNew();

    private async Task OnMpvPositionChangedAsync(double pos)
    {
        if (!IsDisposed && IsHandleCreated)
            BeginInvoke(() => _waveform.SetPlayhead(pos));

        if (!_hsspReady || _handy is null) return;

        var expected = _lastKnownPos + _sincePosUpdate.Elapsed.TotalSeconds;
        var isSeek = Math.Abs(pos - expected) > 0.75;
        _lastKnownPos = pos;
        _sincePosUpdate.Restart();

        if (isSeek && _mpv is { IsPaused: false })
        {
            try
            {
                await _handy.HsspPlayAsync(DeviceTimeMs(pos), EstimatedServerTimeMs(), ct: _lifetimeCts.Token);
                Log($"Seek detected, resynced at {pos:F1}s.");
            }
            catch (Exception ex) when (ex is HandyApiException or HttpRequestException or TaskCanceledException or ObjectDisposedException)
            {
                Log($"Resync failed: {ex.Message}");
            }
        }
    }

    private async Task OnMpvPauseChangedAsync(bool paused)
    {
        if (!_hsspReady || _handy is null) return;
        try
        {
            if (paused)
            {
                await _handy.HsspStopAsync(_lifetimeCts.Token);
                Log("Playback paused; device stopped.");
                SetStatus("Paused");
            }
            else
            {
                await _handy.HsspPlayAsync(DeviceTimeMs(_lastKnownPos), EstimatedServerTimeMs(), ct: _lifetimeCts.Token);
                Log("Playback resumed; device started.");
                SetStatus("Playing");
            }
        }
        catch (Exception ex) when (ex is HandyApiException or HttpRequestException or TaskCanceledException or ObjectDisposedException)
        {
            Log($"Play/stop failed: {ex.Message}");
        }
    }

    private async Task OnSyncTickAsync()
    {
        if (!_hsspReady || _handy is null || _mpv is not { IsPaused: false }) return;
        try
        {
            await _handy.HsspSyncTimeAsync(DeviceTimeMs(_lastKnownPos), EstimatedServerTimeMs(), ct: _lifetimeCts.Token);
        }
        catch (Exception ex) when (ex is HandyApiException or HttpRequestException or TaskCanceledException or ObjectDisposedException)
        {
            Log($"Drift correction failed: {ex.Message}");
        }
    }

    private async Task OnOffsetChangedAsync()
    {
        if (!_hsspReady || _handy is null || _mpv is not { IsPaused: false }) return;

        _offsetChangeCts?.Cancel();
        _offsetChangeCts?.Dispose();
        _offsetChangeCts = CancellationTokenSource.CreateLinkedTokenSource(_lifetimeCts.Token);
        var ct = _offsetChangeCts.Token;
        var offsetMs = _offsetSlider.Value;

        try
        {
            await Task.Delay(100, ct);
            await _handy.HsspSyncTimeAsync(DeviceTimeMs(_lastKnownPos), EstimatedServerTimeMs(), ct: ct);
            Log($"Offset changed to {offsetMs} ms.");
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
        }
        catch (Exception ex) when (ex is HandyApiException or HttpRequestException or TaskCanceledException or ObjectDisposedException)
        {
            Log($"Offset resync failed: {ex.Message}");
        }
    }

    // Ends the mpv session only; the Handy connection outlives it so a new session can reuse the HSSP setup.
    private void Stop()
    {
        _syncTimer.Stop();
        _offsetChangeCts?.Cancel();
        _offsetChangeCts?.Dispose();
        _offsetChangeCts = null;
        if (_hsspReady) _ = _handy?.HsspStopAsync(CancellationToken.None);
        _mpv?.Dispose();
        _mpv = null;
        _waveform.SetMediaDuration(0);
        _stopButton.Enabled = false;
        _startButton.Enabled = true;
        SetStatus(_hsspReady ? "Handy ready" : "Idle");
    }
}
