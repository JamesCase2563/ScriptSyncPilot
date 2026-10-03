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
    private readonly SemaphoreSlim _scriptSetupGate = new(1, 1);
    private HandyApiClient? _installedScriptClient;
    private long _scriptVersion;
    private long _installedScriptVersion = -1;
    private MpvSession? _mpv;
    private Funscript? _script;
    private CancellationTokenSource? _offsetChangeCts;
    private long _clockOffsetMs;
    private bool _hsspReady;
    private bool _isClosing;
    private bool _allowClose;
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
        _stopButton.Click += async (_, _) => await StopAsync();
        _connectButton.Click += async (_, _) => await ConnectHandyAsync();
        _disconnectButton.Click += async (_, _) => await DisconnectHandyAsync();
        _waveform.SeekRequested += OnWaveformSeekRequested;
        _offsetSlider.ValueChanged += async (_, _) =>
        {
            UpdateOffsetLabel();
            await OnOffsetChangedAsync();
        };
        _syncTimer.Tick += async (_, _) => await OnSyncTickAsync();

        FormClosing += async (_, e) =>
        {
            if (_allowClose) return;
            e.Cancel = true;
            if (_isClosing) return;
            _isClosing = true;

            _settings.ConnectionKey = _connectionKeyBox.Text;
            _settings.MpvIpcPipe = _mpvPipeBox.Text;
            _settings.LastScriptPath = _scriptPathBox.Text;
            _settings.OffsetMs = _offsetSlider.Value;
            _settings.Save();

            _lifetimeCts.Cancel();
            await StopAsync(stopDevice: false);
            await DisconnectHandyAsync();
            _allowClose = true;
            Close();
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
        Funscript script;
        try
        {
            script = Funscript.Load(path);
            if (script.Actions.Count == 0)
                throw new InvalidDataException("Funscript contains no actions.");
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException)
        {
            Log($"Could not load script: {ex.Message}");
            return;
        }

        _script = script;
        _scriptVersion++;
        _scriptPathBox.Text = path;
        _waveform.SetScript(_script);
        Log($"Loaded funscript with {_script.Actions.Count} actions.");

        // The device keeps whatever hssp/setup last gave it, so a script swap has to be pushed or it plays the old one.
        if (_hsspReady) _ = ReSetupScriptAsync();
    }

    private async Task ReSetupScriptAsync()
    {
        var client = _handy;
        if (client is null || _script is null) return;

        _hsspReady = false;
        try
        {
            await EnsureCurrentScriptInstalledAsync(client, _lifetimeCts.Token);
            if (!ReferenceEquals(_handy, client) || _script is null) return;

            _hsspReady = true;
            if (_mpv is { IsPaused: false } session) await OnMpvPauseChangedAsync(session, false);
        }
        catch (Exception ex) when (ex is HandyApiException or IOException or HttpRequestException or TaskCanceledException or ObjectDisposedException)
        {
            if (!ReferenceEquals(_handy, client)) return;
            Log($"Could not push new script to device: {ex.Message}");
            await DisconnectHandyAsync();
        }
    }

    private async Task EnsureCurrentScriptInstalledAsync(HandyApiClient client, CancellationToken cancellationToken)
    {
        await _scriptSetupGate.WaitAsync(cancellationToken);
        try
        {
            while (ReferenceEquals(_handy, client))
            {
                var script = _script;
                if (script is null) return;

                var scriptVersion = _scriptVersion;
                if (ReferenceEquals(_installedScriptClient, client) && _installedScriptVersion == scriptVersion)
                    return;

                await client.HsspStopAsync(cancellationToken);
                if (!ReferenceEquals(_handy, client)) return;
                if (!await SetupScriptOnDeviceAsync(client, script, scriptVersion, cancellationToken))
                {
                    if (!ReferenceEquals(_handy, client)) return;
                    continue;
                }

                _installedScriptClient = client;
                _installedScriptVersion = scriptVersion;

                if (scriptVersion == _scriptVersion && ReferenceEquals(script, _script))
                    return;
            }
        }
        finally
        {
            _scriptSetupGate.Release();
        }
    }

    private async Task<bool> SetupScriptOnDeviceAsync(HandyApiClient client, Funscript script, long scriptVersion, CancellationToken cancellationToken)
    {
        SetStatus("Uploading script…");
        var url = await client.UploadScriptCsvAsync(script.ToCsv(), cancellationToken);
        Log($"Script hosted at {url}");

        if (!ReferenceEquals(_handy, client) || scriptVersion != _scriptVersion || !ReferenceEquals(script, _script))
            return false;

        await client.HsspSetupAsync(url, cancellationToken);
        Log("HSSP setup complete.");
        return true;
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
            var session = new MpvSession();
            _mpv = session;
            session.PauseChanged += paused => _ = OnMpvPauseChangedAsync(session, paused);
            session.PositionChanged += pos => _ = OnMpvPositionChangedAsync(session, pos);
            session.DurationChanged += OnMpvDurationChanged;
            session.Disconnected += () => BeginInvoke(() =>
            {
                if (ReferenceEquals(_mpv, session)) _ = StopAsync();
            });
            await session.AttachAsync(_mpvPipeBox.Text.Trim(), _lifetimeCts.Token);
            Log("Attached to mpv IPC pipe.");

            _syncTimer.Start();
            _stopButton.Enabled = true;
            SetStatus(_hsspReady ? "Playing" : "Playing (no Handy)");

            if (_hsspReady && session.IsPaused == false)
                await OnMpvPauseChangedAsync(session, false);
        }
        catch (Exception ex) when (ex is IOException or TaskCanceledException)
        {
            Log($"Error: {ex.Message}");
            SetStatus("Error");
            await StopAsync();
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
            var client = new HandyApiClient(_connectionKeyBox.Text.Trim());
            _handy = client;

            if (!await client.IsConnectedAsync(_lifetimeCts.Token))
                throw new HandyApiException("Device reports not connected. Check the connection key.");
            Log("Handy device connected.");

            await client.SetModeAsync(mode: 1, _lifetimeCts.Token); // HSSP
            Log("Device set to HSSP mode.");

            SetStatus("Measuring clock offset…");
            _clockOffsetMs = await client.EstimateClockOffsetMsAsync(samples: 15, _lifetimeCts.Token);
            Log($"Estimated client-server clock offset: {_clockOffsetMs} ms.");

            await EnsureCurrentScriptInstalledAsync(client, _lifetimeCts.Token);
            if (!ReferenceEquals(_handy, client) || _script is null) return;

            _hsspReady = true;
            _disconnectButton.Enabled = true;
            SetStatus(_mpv is null ? "Handy ready" : "Playing");

            if (_mpv is { IsPaused: false } session)
                await OnMpvPauseChangedAsync(session, false);
        }
        catch (Exception ex) when (ex is HandyApiException or IOException or HttpRequestException or TaskCanceledException or ObjectDisposedException)
        {
            Log($"Handy error: {ex.Message}");
            SetStatus("Handy error");
            await DisconnectHandyAsync();
        }
        finally
        {
            _connectButton.Enabled = !_hsspReady;
        }
    }

    private async Task DisconnectHandyAsync()
    {
        _offsetChangeCts?.Cancel();
        _offsetChangeCts?.Dispose();
        _offsetChangeCts = null;

        var client = _handy;
        _handy = null;
        _hsspReady = false;
        _installedScriptClient = null;
        _installedScriptVersion = -1;
        _disconnectButton.Enabled = false;
        _connectButton.Enabled = false;

        await _scriptSetupGate.WaitAsync();
        try
        {
            if (client is not null)
                await client.HsspStopAsync(CancellationToken.None);
        }
        catch (Exception ex) when (ex is HandyApiException or HttpRequestException or IOException or TaskCanceledException or ObjectDisposedException)
        {
            Log($"Could not stop Handy playback: {ex.Message}");
        }
        finally
        {
            client?.Dispose();
            _scriptSetupGate.Release();
        }

        if (!_isClosing) _connectButton.Enabled = true;
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

    private async Task OnMpvPositionChangedAsync(MpvSession session, double pos)
    {
        if (!ReferenceEquals(_mpv, session)) return;

        if (!IsDisposed && IsHandleCreated)
            BeginInvoke(() =>
            {
                if (ReferenceEquals(_mpv, session)) _waveform.SetPlayhead(pos);
            });

        var expected = _lastKnownPos + _sincePosUpdate.Elapsed.TotalSeconds;
        var isSeek = Math.Abs(pos - expected) > 0.75;
        _lastKnownPos = pos;
        _sincePosUpdate.Restart();

        var client = _handy;
        if (!_hsspReady || client is null) return;

        if (isSeek && _mpv is { IsPaused: false })
        {
            try
            {
                await _scriptSetupGate.WaitAsync(_lifetimeCts.Token);
                try
                {
                    if (!ReferenceEquals(_mpv, session) || !_hsspReady || !ReferenceEquals(_handy, client)) return;
                    await client.HsspPlayAsync(DeviceTimeMs(pos), EstimatedServerTimeMs(), ct: _lifetimeCts.Token);
                }
                finally
                {
                    _scriptSetupGate.Release();
                }

                Log($"Seek detected, resynced at {pos:F1}s.");
            }
            catch (Exception ex) when (ex is HandyApiException or HttpRequestException or TaskCanceledException or ObjectDisposedException)
            {
                Log($"Resync failed: {ex.Message}");
            }
        }
    }

    private async Task OnMpvPauseChangedAsync(MpvSession session, bool paused)
    {
        if (!ReferenceEquals(_mpv, session)) return;
        var client = _handy;
        if (!_hsspReady || client is null) return;

        try
        {
            await _scriptSetupGate.WaitAsync(_lifetimeCts.Token);
            try
            {
                if (!ReferenceEquals(_mpv, session) || !_hsspReady || !ReferenceEquals(_handy, client)) return;

                if (paused)
                {
                    await client.HsspStopAsync(_lifetimeCts.Token);
                    Log("Playback paused; device stopped.");
                    SetStatus("Paused");
                }
                else
                {
                    await client.HsspPlayAsync(DeviceTimeMs(_lastKnownPos), EstimatedServerTimeMs(), ct: _lifetimeCts.Token);
                    Log("Playback resumed; device started.");
                    SetStatus("Playing");
                }
            }
            finally
            {
                _scriptSetupGate.Release();
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
    private async Task StopAsync(bool stopDevice = true)
    {
        _syncTimer.Stop();
        _offsetChangeCts?.Cancel();
        _offsetChangeCts?.Dispose();
        _offsetChangeCts = null;

        var client = _handy;
        _hsspReady = false;
        _mpv?.Dispose();
        _mpv = null;
        _waveform.SetMediaDuration(0);
        _stopButton.Enabled = false;
        _startButton.Enabled = true;

        if (stopDevice && client is not null)
        {
            await _scriptSetupGate.WaitAsync();
            try
            {
                if (ReferenceEquals(_handy, client))
                    await client.HsspStopAsync(CancellationToken.None);
            }
            catch (Exception ex) when (ex is HandyApiException or HttpRequestException or IOException or TaskCanceledException or ObjectDisposedException)
            {
                Log($"Could not stop Handy playback: {ex.Message}");
            }
            finally
            {
                _scriptSetupGate.Release();
            }
        }

        _hsspReady = stopDevice
                     && client is not null
                     && ReferenceEquals(_handy, client)
                     && ReferenceEquals(_installedScriptClient, client)
                     && _installedScriptVersion == _scriptVersion;
        SetStatus(_hsspReady ? "Handy ready" : "Idle");
    }
}
