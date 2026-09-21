using System.Diagnostics;

namespace ScriptSyncPilot.HandySync;

/// <summary>
/// Single-window app: launches mpv for the chosen video, and drives a Handy
/// device via HSSP so the script stays in sync with mpv's own playhead
/// (play/pause/seeks all re-sync; a periodic synctime call corrects drift).
/// </summary>
public sealed class MainForm : Form
{
    private readonly TextBox _apiKeyBox = new() { PlaceholderText = "Handy application API key" };
    private readonly TextBox _connectionKeyBox = new() { PlaceholderText = "Device connection key" };
    private readonly TextBox _mpvPipeBox = new() { PlaceholderText = @"mpv IPC pipe, e.g. \\.\pipe\vrheadtrack" };
    private readonly TextBox _scriptPathBox = new() { ReadOnly = true };
    private readonly NumericUpDown _offsetBox = new() { Minimum = -2000, Maximum = 2000, Increment = 10 };
    private readonly Button _startButton = new() { Text = "Load Script + Sync" };
    private readonly Button _stopButton = new() { Text = "Stop", Enabled = false };
    private readonly TextBox _log = new() { Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, Dock = DockStyle.Fill };
    private readonly Label _status = new() { Text = "Idle", AutoSize = true };
    private readonly System.Windows.Forms.Timer _syncTimer = new() { Interval = 3000 };

    private readonly AppSettings _settings = AppSettings.Load();

    private HandyApiClient? _handy;
    private MpvSession? _mpv;
    private long _clockOffsetMs;
    private bool _hsspReady;
    private readonly CancellationTokenSource _lifetimeCts = new();

    public MainForm()
    {
        Text = "ScriptSyncPilot – Handy Sync";
        Width = 720;
        Height = 560;

        _apiKeyBox.Text = _settings.ApiKey;
        _connectionKeyBox.Text = _settings.ConnectionKey;
        _mpvPipeBox.Text = _settings.MpvIpcPipe;
        _scriptPathBox.Text = _settings.LastScriptPath;
        _offsetBox.Value = Math.Clamp(_settings.OffsetMs, (int)_offsetBox.Minimum, (int)_offsetBox.Maximum);

        var browseScript = new Button { Text = "Browse…" };
        browseScript.Click += (_, _) =>
        {
            using var dlg = new OpenFileDialog { Filter = "Funscript|*.funscript;*.json|All files|*.*" };
            if (dlg.ShowDialog(this) == DialogResult.OK) _scriptPathBox.Text = dlg.FileName;
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

        AddRow("API key", _apiKeyBox);
        AddRow("Connection key", _connectionKeyBox);
        AddRow("mpv IPC pipe", _mpvPipeBox);
        AddRow("Funscript", _scriptPathBox, browseScript);
        AddRow("Offset (ms)", _offsetBox);

        var buttons = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, Padding = new Padding(8) };
        buttons.Controls.Add(_startButton);
        buttons.Controls.Add(_stopButton);
        buttons.Controls.Add(_status);

        Controls.Add(_log);
        Controls.Add(buttons);
        Controls.Add(layout);

        _startButton.Click += async (_, _) => await StartAsync();
        _stopButton.Click += (_, _) => Stop();
        _offsetBox.ValueChanged += async (_, _) => await OnOffsetChangedAsync();
        _syncTimer.Tick += async (_, _) => await OnSyncTickAsync();

        FormClosing += (_, _) =>
        {
            _settings.ApiKey = _apiKeyBox.Text;
            _settings.ConnectionKey = _connectionKeyBox.Text;
            _settings.MpvIpcPipe = _mpvPipeBox.Text;
            _settings.LastScriptPath = _scriptPathBox.Text;
            _settings.OffsetMs = (int)_offsetBox.Value;
            _settings.Save();
            Stop();
        };
    }

    private void Log(string message)
    {
        AppLog.Write(message);
        if (!IsDisposed && IsHandleCreated)
            BeginInvoke(() => _log.AppendText($"[{DateTime.Now:HH:mm:ss}] {message}{Environment.NewLine}"));
    }

    private void SetStatus(string text) => BeginInvoke(() => _status.Text = text);

    private async Task StartAsync()
    {
        if (_scriptPathBox.Text.Length == 0)
        {
            MessageBox.Show(this, "Pick a funscript first.", "ScriptSyncPilot");
            return;
        }

        _startButton.Enabled = false;
        try
        {
            SetStatus("Connecting to Handy…");
            _handy = new HandyApiClient(_apiKeyBox.Text.Trim(), _connectionKeyBox.Text.Trim());

            if (!await _handy.IsConnectedAsync(_lifetimeCts.Token))
                throw new HandyApiException("Device reports not connected. Check the connection key.");
            Log("Handy device connected.");

            await _handy.SetModeAsync(mode: 1, _lifetimeCts.Token); // HSSP
            Log("Device set to HSSP mode.");

            var script = Funscript.Load(_scriptPathBox.Text);
            Log($"Loaded funscript with {script.Actions.Count} actions.");

            SetStatus("Uploading script…");
            var url = await _handy.UploadScriptCsvAsync(script.ToCsv(), _lifetimeCts.Token);
            Log($"Script hosted at {url}");

            await _handy.HsspSetupAsync(url, _lifetimeCts.Token);
            Log("HSSP setup complete.");

            SetStatus("Measuring clock offset…");
            _clockOffsetMs = await _handy.EstimateClockOffsetMsAsync(samples: 15, _lifetimeCts.Token);
            Log($"Estimated client-server clock offset: {_clockOffsetMs} ms.");

            _hsspReady = true;

            SetStatus("Attaching to mpv…");
            _mpv = new MpvSession();
            _mpv.PauseChanged += async paused => await OnMpvPauseChangedAsync(paused);
            _mpv.PositionChanged += async pos => await OnMpvPositionChangedAsync(pos);
            _mpv.Disconnected += () => BeginInvoke(Stop);
            await _mpv.AttachAsync(_mpvPipeBox.Text.Trim(), _lifetimeCts.Token);
            Log("Attached to mpv IPC pipe.");

            _syncTimer.Start();
            _stopButton.Enabled = true;
            SetStatus("Playing");
        }
        catch (Exception ex) when (ex is HandyApiException or IOException or InvalidDataException or HttpRequestException)
        {
            Log($"Error: {ex.Message}");
            SetStatus("Error");
            Stop();
        }
        finally
        {
            _startButton.Enabled = true;
        }
    }

    private long EstimatedServerTimeMs() =>
        DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + _clockOffsetMs;

    private long DeviceTimeMs(double mpvPositionSeconds) =>
        (long)(mpvPositionSeconds * 1000) + (int)_offsetBox.Value;

    private double _lastKnownPos;
    private readonly Stopwatch _sincePosUpdate = Stopwatch.StartNew();

    private async Task OnMpvPositionChangedAsync(double pos)
    {
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
            catch (HandyApiException ex)
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
        catch (HandyApiException ex)
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
        catch (HandyApiException ex)
        {
            Log($"Drift correction failed: {ex.Message}");
        }
    }

    private async Task OnOffsetChangedAsync()
    {
        if (!_hsspReady || _handy is null || _mpv is not { IsPaused: false }) return;
        try
        {
            await _handy.HsspSyncTimeAsync(DeviceTimeMs(_lastKnownPos), EstimatedServerTimeMs(), ct: _lifetimeCts.Token);
            Log($"Offset changed to {_offsetBox.Value} ms.");
        }
        catch (HandyApiException ex)
        {
            Log($"Offset resync failed: {ex.Message}");
        }
    }

    private void Stop()
    {
        _syncTimer.Stop();
        if (_hsspReady) _ = _handy?.HsspStopAsync(CancellationToken.None);
        _hsspReady = false;
        _mpv?.Dispose();
        _mpv = null;
        _handy?.Dispose();
        _handy = null;
        _stopButton.Enabled = false;
        SetStatus("Idle");
    }
}
