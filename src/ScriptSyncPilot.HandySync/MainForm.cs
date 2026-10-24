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
    private readonly DeviceSliderControl _offsetSlider = new() { AccessibleName = "Device offset (milliseconds)", Minimum = -1000, Maximum = 1000, SmallStep = 10, LargeStep = 100 };
    private readonly Label _offsetValue = new() { AutoSize = true, TextAlign = ContentAlignment.MiddleCenter, MinimumSize = new Size(72, 0) };
    private readonly Label _offsetState = new() { AutoSize = true, Text = "not connected" };
    private readonly DeviceSliderControl _strokeSlider = new()
    {
        AccessibleName = "Stroke range (percent)",
        Minimum = 0,
        Maximum = 100,
        IsRange = true,
        Orientation = Orientation.Vertical,
        SmallStep = 1,
        LargeStep = 10,
        Size = new Size(52, 120),
        MinimumSize = new Size(44, 100)
    };
    private readonly Label _strokeValue = new() { AutoSize = true, Text = "0–100 %" };
    private readonly Label _strokeState = new() { AutoSize = true, Text = "not connected" };
    private readonly Label _temperatureValue = new() { AutoSize = true, Text = "Motor -- °C" };
    private readonly ToolTip _deviceToolTip = new();
    private readonly Button _attachMpvButton = new() { Text = "Attach" };
    private readonly Button _detachMpvButton = new() { Text = "Detach", Enabled = false };
    private readonly Button _playVideoButton = new() { Text = "Play", Enabled = false };
    private readonly Button _pauseVideoButton = new() { Text = "Pause", Enabled = false };
    private readonly Button _connectButton = new() { Text = "Connect" };
    private readonly Button _disconnectButton = new() { Text = "Disconnect", Enabled = false };
    private readonly TextBox _log = new() { Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, Dock = DockStyle.Fill };
    private readonly Label _status = new() { Text = "Idle", AutoSize = true };
    private readonly ScriptWaveformControl _waveform = new() { Dock = DockStyle.Bottom, Height = 90 };
    private readonly System.Windows.Forms.Timer _syncTimer = new() { Interval = 3000 };
    private readonly System.Windows.Forms.Timer _devicePollTimer = new() { Interval = 5000 };

    private readonly AppSettings _settings;
    private readonly Func<string, HandyApiClient> _createHandyClient;
    private readonly Action<AppSettings> _saveSettings;
    private readonly Action<string> _showSettingsError;

    private HandyApiClient? _handy;
    private readonly SemaphoreSlim _deviceCommandGate = new(1, 1);
    private readonly SemaphoreSlim _sessionTransitionGate = new(1, 1);
    private HandyApiClient? _installedScriptClient;
    private long _scriptVersion;
    private long _installedScriptVersion = -1;
    private MpvSession? _mpv;
    private Funscript? _script;
    private CancellationTokenSource? _offsetWriteCts;
    private CancellationTokenSource? _strokeWriteCts;
    private bool _deviceControlsSupported;
    private bool _hasDeviceSlider = true;
    private bool _devicePollBusy;
    private int _devicePollTick;
    private int _offsetEditVersion;
    private int _strokeEditVersion;
    private int _offsetInitialReadEditVersion;
    private int _strokeInitialReadEditVersion;
    private bool _offsetInitialReadFailed;
    private bool _strokeInitialReadFailed;
    private bool _offsetWriteFailed;
    private bool _strokeWriteFailed;
    private readonly Dictionary<string, int> _pollFailureCounts = new();
    private bool _offsetWriteInFlight;
    private bool _strokeWriteInFlight;
    private int? _lastDeviceOffset;
    private HandyApiClient.SliderStroke? _lastDeviceStroke;
    private double? _strokeMinAbsolute;
    private double? _strokeMaxAbsolute;
    private double? _temperatureHighTrigger;
    private double? _temperatureHysteresis;
    private DeviceTemperatureBand _temperatureBand = DeviceTemperatureBand.Normal;
    private long _clockOffsetMs;
    private bool _hsspReady;
    private bool _mpvAttached;
    private bool _videoTransportPending;
    private bool _mpvAttachInProgress;
    private bool _mpvDetaching;
    private bool _handyDisconnecting;
    private bool _isClosing;
    private bool _allowClose;
    private readonly CancellationTokenSource _lifetimeCts = new();

    public MainForm() : this(AppSettings.Load(), key => new HandyApiClient(key)) { }

    internal MainForm(AppSettings settings, Func<string, HandyApiClient> createHandyClient,
        Action<AppSettings>? saveSettings = null, Action<string>? showSettingsError = null)
    {
        _settings = settings;
        _createHandyClient = createHandyClient;
        _saveSettings = saveSettings ?? (value => value.Save());
        _showSettingsError = showSettingsError ?? (message => MessageBox.Show(this, message,
            "Settings not saved", MessageBoxButtons.OK, MessageBoxIcon.Error));
        Text = "ScriptSyncPilot – Handy Sync";
        Width = 720;
        Height = 560;

        _connectionKeyBox.Text = _settings.GetFirstConnectionKey() ?? _settings.ConnectionKey;
        if (_settings.RequiresKeyReentry)
            Shown += (_, _) => MessageBox.Show(this,
                "Please re-enter your Handy connection key. Legacy plaintext keys are no longer loaded; the key will be protected when settings are saved.",
                "Connection key required", MessageBoxButtons.OK, MessageBoxIcon.Information);
        _mpvPipeBox.Text = _settings.MpvIpcPipe;
        _scriptPathBox.Text = _settings.LastScriptPath;
        if (_scriptPathBox.Text.Length > 0 && File.Exists(_scriptPathBox.Text))
            LoadScript(_scriptPathBox.Text);
        _offsetSlider.SetValues(Math.Clamp(_settings.DeviceOffsetMs, _offsetSlider.Minimum, _offsetSlider.Maximum), 0);
        var (strokeMin, strokeMax) = DeviceControlLogic.NormalizeStrokeRange(
            _settings.StrokeMinPercent, _settings.StrokeMaxPercent);
        _strokeSlider.SetValues(strokeMin, strokeMax);
        UpdateOffsetLabel();
        UpdateStrokeLabel();

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
        var offsetInfo = new FlowLayoutPanel { AutoSize = true, WrapContents = false, FlowDirection = FlowDirection.LeftToRight, Anchor = AnchorStyles.Left };
        offsetInfo.Controls.Add(_offsetValue);
        offsetInfo.Controls.Add(_offsetState);
        AddRow("Offset (ms)", _offsetSlider, offsetInfo);

        var strokeInfo = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false, FlowDirection = FlowDirection.LeftToRight, AutoSize = true };
        strokeInfo.Controls.Add(_strokeSlider);
        var strokeLabels = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.TopDown, WrapContents = false, Anchor = AnchorStyles.Left };
        strokeLabels.Controls.Add(_strokeValue);
        strokeLabels.Controls.Add(_strokeState);
        strokeInfo.Controls.Add(strokeLabels);
        AddRow("Stroke range", strokeInfo, _temperatureValue);

        var buttons = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, Padding = new Padding(8), WrapContents = true };
        buttons.Controls.Add(new Label { Text = "Video", AutoSize = true, Margin = new Padding(4, 8, 2, 3) });
        buttons.Controls.Add(_attachMpvButton);
        buttons.Controls.Add(_detachMpvButton);
        buttons.Controls.Add(_playVideoButton);
        buttons.Controls.Add(_pauseVideoButton);
        buttons.Controls.Add(new Label { Text = "Handy", AutoSize = true, Margin = new Padding(12, 8, 2, 3) });
        buttons.Controls.Add(_connectButton);
        buttons.Controls.Add(_disconnectButton);
        buttons.Controls.Add(_status);

        Controls.Add(_log);
        Controls.Add(_waveform);
        Controls.Add(buttons);
        Controls.Add(layout);

        _attachMpvButton.Click += async (_, _) => await AttachMpvAsync();
        _detachMpvButton.Click += async (_, _) => await DetachMpvAsync();
        _playVideoButton.Click += async (_, _) => await SetVideoPausedAsync(false);
        _pauseVideoButton.Click += async (_, _) => await SetVideoPausedAsync(true);
        _connectButton.Click += async (_, _) => await ConnectHandyAsync();
        _disconnectButton.Click += async (_, _) => await DisconnectHandyAsync();
        _waveform.SeekRequested += OnWaveformSeekRequested;
        _offsetSlider.ValuesChanged += (_, _) =>
        {
            _offsetEditVersion++;
            _offsetWriteFailed = false;
            UpdateOffsetLabel();
            _settings.DeviceOffsetMs = _offsetSlider.LowerValue;
            _offsetState.Text = _handy is null ? "not connected" : "pending";
            ScheduleOffsetWrite();
        };
        _strokeSlider.ValuesChanged += (_, _) =>
        {
            _strokeEditVersion++;
            _strokeWriteFailed = false;
            _strokeMinAbsolute = null;
            _strokeMaxAbsolute = null;
            UpdateStrokeLabel();
            _settings.StrokeMinPercent = _strokeSlider.LowerValue;
            _settings.StrokeMaxPercent = _strokeSlider.UpperValue;
            _strokeState.Text = _handy is null ? "not connected" : "pending";
            ScheduleStrokeWrite();
        };
        _syncTimer.Tick += async (_, _) => await OnSyncTickAsync();
        _devicePollTimer.Tick += async (_, _) => await PollDeviceValuesAsync();
        Activated += async (_, _) => await PollDeviceValuesAsync();

        FormClosing += async (_, e) =>
        {
            if (_allowClose) return;
            e.Cancel = true;
            if (_isClosing) return;
            _isClosing = true;

            _settings.ConnectionKey = _connectionKeyBox.Text;
            _settings.MpvIpcPipe = _mpvPipeBox.Text;
            _settings.LastScriptPath = _scriptPathBox.Text;
            _settings.DeviceOffsetMs = _offsetSlider.LowerValue;
            _settings.StrokeMinPercent = _strokeSlider.LowerValue;
            _settings.StrokeMaxPercent = _strokeSlider.UpperValue;
            try
            {
                _saveSettings(_settings);
            }
            catch (SettingsException ex)
            {
                Log(ex.Message);
                _isClosing = false;
                _showSettingsError(ex.Message);
                return;
            }

            _lifetimeCts.Cancel();
            await DetachMpvAsync(stopDevice: false);
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
        await _deviceCommandGate.WaitAsync(cancellationToken);
        try
        {
            while (ReferenceEquals(_handy, client))
            {
                var script = _script;
                if (script is null) return;

                var scriptVersion = _scriptVersion;
                if (ReferenceEquals(_installedScriptClient, client) && _installedScriptVersion == scriptVersion)
                    return;

                if (ShouldStopBeforeHsspSetup(_installedScriptClient, client))
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
            _deviceCommandGate.Release();
        }
    }

    internal static bool ShouldStopBeforeHsspSetup(HandyApiClient? installedClient, HandyApiClient client) =>
        ReferenceEquals(installedClient, client);

    internal static bool ShouldStopOnDisconnect(HandyApiClient? installedClient, HandyApiClient client) =>
        ReferenceEquals(installedClient, client);

    private async Task<bool> SetupScriptOnDeviceAsync(HandyApiClient client, Funscript script, long scriptVersion, CancellationToken cancellationToken)
    {
        SetStatus("Uploading script…");
        var url = await client.UploadScriptCsvAsync(script.ToCsv(), cancellationToken);
        Log("Script upload complete; configuring HSSP.");

        if (!ReferenceEquals(_handy, client) || scriptVersion != _scriptVersion || !ReferenceEquals(script, _script))
            return false;

        await client.HsspSetupAsync(url, cancellationToken);
        Log("HSSP setup complete.");
        return true;
    }

    /// <summary>Attaches to mpv and follows its playhead. Does not require a Handy.</summary>
    private async Task AttachMpvAsync()
    {
        if (_mpv is not null || _mpvAttachInProgress) return;
        if (_script is null)
        {
            MessageBox.Show(this, "Pick a funscript first.", "ScriptSyncPilot");
            return;
        }
        _mpvAttachInProgress = true;
        UpdateVideoTransportButtons(null);
        await _sessionTransitionGate.WaitAsync();
        try
        {
            SetStatus("Attaching to mpv video…");
            var session = new MpvSession();
            _mpv = session;
            session.PauseChanged += paused => _ = OnMpvPauseChangedAsync(session, paused);
            session.PositionChanged += pos => _ = OnMpvPositionChangedAsync(session, pos);
            session.DurationChanged += seconds => OnMpvDurationChanged(session, seconds);
            session.SpeedChanged += _ => OnMpvSpeedChanged(session);
            session.Disconnected += () => BeginInvoke(() =>
            {
                if (ReferenceEquals(_mpv, session)) _ = DetachMpvAsync();
            });
            await session.AttachAsync(_mpvPipeBox.Text.Trim(), _lifetimeCts.Token);
            Log("Attached to mpv IPC pipe.");

            _mpvAttached = true;
            _syncTimer.Start();
            UpdateVideoTransportButtons(session);
            SetStatus(session.IsPaused ? "Video attached (paused)" : _hsspReady ? "Playing" : "Video playing (Handy not ready)");

            if (_hsspReady && !session.IsPaused)
                await OnMpvPauseChangedAsync(session, false);
        }
        catch (Exception ex) when (ex is IOException or TaskCanceledException or ArgumentException)
        {
            Log($"Error: {ex.Message}");
            SetStatus("Error");
            await DetachMpvCoreAsync();
        }
        finally
        {
            _sessionTransitionGate.Release();
            _mpvAttachInProgress = false;
            UpdateVideoTransportButtons(_mpv);
        }
    }

    /// <summary>Connects the device and prepares HSSP. Independent of whether a session is running.</summary>
    private async Task ConnectHandyAsync()
    {
        if (_handyDisconnecting) return;
        if (string.IsNullOrWhiteSpace(_connectionKeyBox.Text))
        {
            MessageBox.Show(this, "Enter a Handy connection key before connecting.", "ScriptSyncPilot");
            return;
        }
        if (_script is null)
        {
            MessageBox.Show(this, "Pick a funscript first.", "ScriptSyncPilot");
            return;
        }

        _connectButton.Enabled = false;
        try
        {
            SetStatus("Connecting to Handy…");
            var client = _createHandyClient(_connectionKeyBox.Text.Trim());
            _deviceControlsSupported = false;
            _offsetSlider.Enabled = false;
            _strokeSlider.Enabled = false;
            _handy = client;
            _lastDeviceOffset = null;
            _lastDeviceStroke = null;
            _offsetInitialReadFailed = false;
            _strokeInitialReadFailed = false;
            _offsetWriteFailed = false;
            _strokeWriteFailed = false;
            _devicePollTick = 0;

            await _deviceCommandGate.WaitAsync(_lifetimeCts.Token);
            bool isConnected;
            try
            {
                if (!ReferenceEquals(_handy, client)) return;
                isConnected = await client.IsConnectedAsync(_lifetimeCts.Token);
            }
            finally
            {
                _deviceCommandGate.Release();
            }

            if (!isConnected)
                throw new HandyApiException("Device reports not connected. Check the connection key.");
            Log("Handy device connected.");
            await CheckDeviceFirmwareAsync(client);

            await _deviceCommandGate.WaitAsync(_lifetimeCts.Token);
            try
            {
                if (!ReferenceEquals(_handy, client)) return;
                await client.SetModeAsync(mode: 1, _lifetimeCts.Token);
            }
            finally
            {
                _deviceCommandGate.Release();
            }
            Log("Device set to HSSP mode.");

            SetStatus("Measuring clock offset…");
            await _deviceCommandGate.WaitAsync(_lifetimeCts.Token);
            try
            {
                if (!ReferenceEquals(_handy, client)) return;
                _clockOffsetMs = await client.EstimateClockOffsetMsAsync(samples: 15, _lifetimeCts.Token);
            }
            finally
            {
                _deviceCommandGate.Release();
            }
            Log($"Estimated client-server clock offset: {_clockOffsetMs} ms.");

            await ReadAndAdoptDeviceControlsAsync(client);
            if (!ReferenceEquals(_handy, client) || _isClosing) return;

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
            _connectButton.Enabled = !_hsspReady && !_handyDisconnecting && !_isClosing;
        }
    }

    private async Task CheckDeviceFirmwareAsync(HandyApiClient client)
    {
        try
        {
            await _deviceCommandGate.WaitAsync(_lifetimeCts.Token);
            HandyApiClient.DeviceInfo info;
            try
            {
                if (!ReferenceEquals(_handy, client)) return;
                info = await client.GetInfoAsync(_lifetimeCts.Token);
            }
            finally
            {
                _deviceCommandGate.Release();
            }

            var modelName = string.IsNullOrWhiteSpace(info.ModelName)
                ? "not reported"
                : System.Text.Json.JsonSerializer.Serialize(info.ModelName);
            Log($"Device hardware: model name {modelName}, model number {info.ModelNumber?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "not reported"}, model variant {info.ModelVariant?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "not reported"}.");

            _deviceControlsSupported = IsDeviceControlsFirmwareSupported(info.FirmwareVersion, info.FirmwareStatus);
            if (!_deviceControlsSupported)
            {
                _offsetState.Text = "unsupported firmware";
                _strokeState.Text = "unsupported firmware";
                Log($"Device controls unsupported: firmware {info.FirmwareVersion}, status {info.FirmwareStatus}.");
            }
            else
            {
                Log($"Device firmware supported: {info.FirmwareVersion}, status {info.FirmwareStatus}.");
            }
        }
        catch (Exception ex) when (ex is HandyApiException or HttpRequestException or IOException or TaskCanceledException or ObjectDisposedException)
        {
            _deviceControlsSupported = false;
            _offsetSlider.Enabled = false;
            _strokeSlider.Enabled = false;
            _offsetState.Text = "unsupported firmware";
            _strokeState.Text = "unsupported firmware";
            Log($"Could not verify device firmware; device controls are disabled: {ex.Message}");
        }
    }

    internal static bool IsDeviceControlsFirmwareSupported(string firmwareVersion, int? firmwareStatus)
    {
        var versionText = firmwareVersion.Split('+', 2)[0];
        return Version.TryParse(versionText, out var version) && version.Major >= 4
               && firmwareStatus is int status && status != 2;
    }

    private async Task ReadAndAdoptDeviceControlsAsync(HandyApiClient client)
    {
        if (!_deviceControlsSupported) return;
        var gateAcquired = false;
        try
        {
            await _deviceCommandGate.WaitAsync(_lifetimeCts.Token);
            gateAcquired = true;
            if (!ReferenceEquals(_handy, client) || _handyDisconnecting) return;

            try
            {
                var capabilities = await client.GetCapabilitiesAsync(_lifetimeCts.Token);
                _hasDeviceSlider = capabilities.SliderCount > 0;
            }
            catch (Exception ex) when (ex is HandyApiException or HttpRequestException or IOException or TaskCanceledException or ObjectDisposedException)
            {
                _hasDeviceSlider = true;
                Log($"Could not read device capabilities: {ex.Message}");
            }

            if (!_hasDeviceSlider)
            {
                _strokeState.Text = "no slider";
                Log("Device has no slider; stroke controls are disabled.");
            }

            try
            {
                var settings = await client.GetSliderSettingsAsync(_lifetimeCts.Token);
                _temperatureHighTrigger = settings.TemperatureHighTrigger;
                _temperatureHysteresis = settings.TemperatureHysteresis;
                _deviceToolTip.SetToolTip(_strokeSlider,
                    $"Physical slider {settings.PhysicalMinMm:F0}-{settings.PhysicalMaxMm:F0} mm, "
                    + $"speed {settings.MinSpeedMmPerSecond:F0}-{settings.MaxSpeedMmPerSecond:F0} mm/s.");
            }
            catch (Exception ex) when (ex is HandyApiException or HttpRequestException or IOException or TaskCanceledException or ObjectDisposedException)
            {
                _temperatureHighTrigger = null;
                _temperatureHysteresis = null;
                _deviceToolTip.SetToolTip(_strokeSlider, string.Empty);
                Log($"Could not read physical slider settings: {ex.Message}");
            }

            try
            {
                var currentOffset = await client.GetOffsetAsync(_lifetimeCts.Token);
                _offsetInitialReadFailed = false;
                _offsetWriteFailed = false;
                _lastDeviceOffset = currentOffset;
                Log($"Device HSTP offset on connect: {currentOffset} ms.");
                _offsetSlider.SetValues(currentOffset, 0);
                _settings.DeviceOffsetMs = currentOffset;
                UpdateOffsetLabel();
                _offsetState.Text = "applied";
            }
            catch (Exception ex) when (ex is HandyApiException or HttpRequestException or IOException or TaskCanceledException or ObjectDisposedException)
            {
                _offsetInitialReadFailed = true;
                _offsetInitialReadEditVersion = _offsetEditVersion;
                _offsetWriteFailed = false;
                _offsetState.Text = "not applied";
                Log($"Could not read device offset: {ex.Message}");
            }

            if (_hasDeviceSlider)
            {
                try
                {
                    var currentStroke = await client.GetStrokeAsync(_lifetimeCts.Token);
                    _strokeInitialReadFailed = false;
                    _strokeWriteFailed = false;
                    _lastDeviceStroke = currentStroke;
                    Log($"Device stroke on connect: {currentStroke.Min:P0}-{currentStroke.Max:P0} "
                        + $"({currentStroke.MinAbsolute:F1}-{currentStroke.MaxAbsolute:F1} mm).");
                    _strokeSlider.SetValues((int)Math.Round(currentStroke.Min * 100), (int)Math.Round(currentStroke.Max * 100));
                    _strokeMinAbsolute = currentStroke.MinAbsolute;
                    _strokeMaxAbsolute = currentStroke.MaxAbsolute;
                    _settings.StrokeMinPercent = _strokeSlider.LowerValue;
                    _settings.StrokeMaxPercent = _strokeSlider.UpperValue;
                    UpdateStrokeLabel();
                    _strokeState.Text = "applied";
                }
                catch (Exception ex) when (ex is HandyApiException or HttpRequestException or IOException or TaskCanceledException or ObjectDisposedException)
                {
                    _strokeInitialReadFailed = true;
                    _strokeInitialReadEditVersion = _strokeEditVersion;
                    _strokeWriteFailed = false;
                    _strokeState.Text = "not applied";
                    Log($"Could not read device stroke: {ex.Message}");
                }
            }
        }
        catch (OperationCanceledException) when (_lifetimeCts.IsCancellationRequested)
        {
        }
        finally
        {
            if (gateAcquired) _deviceCommandGate.Release();
        }

        if (_deviceControlsSupported && ReferenceEquals(_handy, client) && !_handyDisconnecting
            && !_isClosing && !_lifetimeCts.IsCancellationRequested)
        {
            _offsetSlider.Enabled = true;
            _strokeSlider.Enabled = _hasDeviceSlider;
            _devicePollTimer.Start();
        }
    }

    private async Task DisconnectHandyAsync()
    {
        _handyDisconnecting = true;
        _devicePollTimer.Stop();
        _pollFailureCounts.Clear();
        CancelPendingDeviceWrites();

        var client = _handy;
        var shouldStopDevice = client is not null && ShouldStopOnDisconnect(_installedScriptClient, client);
        _handy = null;
        _hsspReady = false;
        _installedScriptClient = null;
        _installedScriptVersion = -1;
        _disconnectButton.Enabled = false;
        _connectButton.Enabled = false;

        await _deviceCommandGate.WaitAsync();
        try
        {
            if (client is not null && shouldStopDevice)
                await client.HsspStopAsync(CancellationToken.None);
        }
        catch (Exception ex) when (ex is HandyApiException or HttpRequestException or IOException or TaskCanceledException or ObjectDisposedException)
        {
            Log($"Could not stop Handy playback: {ex.Message}");
        }
        finally
        {
            client?.Dispose();
            _deviceCommandGate.Release();
        }

        _handyDisconnecting = false;
        _offsetState.Text = "not connected";
        _strokeState.Text = "not connected";
        _offsetSlider.Enabled = true;
        _strokeSlider.Enabled = true;
        _strokeMinAbsolute = null;
        _strokeMaxAbsolute = null;
        UpdateStrokeLabel();
        _deviceToolTip.SetToolTip(_strokeSlider, string.Empty);
        _temperatureHighTrigger = null;
        _temperatureHysteresis = null;
        _temperatureValue.Text = "Motor -- °C";
        _temperatureValue.ForeColor = SystemColors.ControlText;
        _temperatureValue.BackColor = SystemColors.Control;
        _temperatureBand = DeviceTemperatureBand.Normal;
        if (!_isClosing) _connectButton.Enabled = true;
        SetStatus(_mpv is null ? "Idle" : "Playing (no Handy)");
    }

    private void OnMpvDurationChanged(MpvSession session, double seconds)
    {
        if (!ReferenceEquals(_mpv, session) || IsDisposed || !IsHandleCreated) return;
        BeginInvoke(() =>
        {
            if (!ReferenceEquals(_mpv, session)) return;
            _waveform.SetMediaDuration(seconds);
        });
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
    private long DeviceTimeMs(double mpvPositionSeconds) => (long)(mpvPositionSeconds * 1000);

    private void UpdateOffsetLabel() => _offsetValue.Text = $"{_offsetSlider.LowerValue:+#;-#;0} ms";

    private void UpdateStrokeLabel() => _strokeValue.Text = _strokeMinAbsolute is double minMm && _strokeMaxAbsolute is double maxMm
        ? $"{_strokeSlider.LowerValue}-{_strokeSlider.UpperValue} % ({minMm:F1}-{maxMm:F1} mm)"
        : $"{_strokeSlider.LowerValue}-{_strokeSlider.UpperValue} %";

    private void CancelPendingDeviceWrites()
    {
        _offsetWriteCts?.Cancel();
        _strokeWriteCts?.Cancel();
        _offsetWriteCts = null;
        _strokeWriteCts = null;
    }

    private void ScheduleOffsetWrite()
    {
        var client = _handy;
        if (client is null || !_deviceControlsSupported || _handyDisconnecting) return;
        _offsetWriteCts?.Cancel();
        var cancellation = CancellationTokenSource.CreateLinkedTokenSource(_lifetimeCts.Token);
        _offsetWriteCts = cancellation;
        _ = WriteOffsetAfterDebounceAsync(client, _offsetSlider.LowerValue, cancellation);
    }

    private async Task WriteOffsetAfterDebounceAsync(HandyApiClient client, int offset, CancellationTokenSource cancellation)
    {
        var token = cancellation.Token;
        try
        {
            await Task.Delay(200, token);
            await _deviceCommandGate.WaitAsync(token);
            try
            {
                if (token.IsCancellationRequested || !_deviceControlsSupported || _handyDisconnecting || !ReferenceEquals(_handy, client)
                    || !ReferenceEquals(_offsetWriteCts, cancellation)) return;
                _offsetWriteInFlight = true;
                await client.SetOffsetAsync(offset, token);
                if (token.IsCancellationRequested || !ReferenceEquals(_offsetWriteCts, cancellation)) return;
                _offsetWriteFailed = false;
                _offsetInitialReadFailed = false;
                _lastDeviceOffset = offset;
                _offsetState.Text = "applied";
                Log($"Device offset applied: {offset} ms.");
            }
            finally
            {
                _offsetWriteInFlight = false;
                _deviceCommandGate.Release();
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
        }
        catch (OperationCanceledException ex)
        {
            if (ReferenceEquals(_offsetWriteCts, cancellation))
            {
                _offsetWriteFailed = true;
                _offsetState.Text = "not applied";
                Log($"Device offset write timed out: {ex.Message}");
            }
        }
        catch (Exception ex) when (ex is HandyApiException or HttpRequestException or IOException or ObjectDisposedException)
        {
            if (!token.IsCancellationRequested && ReferenceEquals(_offsetWriteCts, cancellation))
            {
                _offsetWriteFailed = true;
                _offsetState.Text = "not applied";
                Log($"Device offset write failed: {ex.Message}");
            }
        }
        finally
        {
            if (ReferenceEquals(_offsetWriteCts, cancellation)) _offsetWriteCts = null;
            cancellation.Dispose();
        }
    }

    private void ScheduleStrokeWrite()
    {
        var client = _handy;
        if (client is null || !_deviceControlsSupported || !_hasDeviceSlider || _handyDisconnecting) return;
        _strokeWriteCts?.Cancel();
        var cancellation = CancellationTokenSource.CreateLinkedTokenSource(_lifetimeCts.Token);
        _strokeWriteCts = cancellation;
        _ = WriteStrokeAfterDebounceAsync(client, _strokeSlider.LowerValue, _strokeSlider.UpperValue, cancellation);
    }

    private async Task WriteStrokeAfterDebounceAsync(HandyApiClient client, int minimum, int maximum, CancellationTokenSource cancellation)
    {
        var token = cancellation.Token;
        try
        {
            await Task.Delay(200, token);
            await _deviceCommandGate.WaitAsync(token);
            try
            {
                if (token.IsCancellationRequested || !_deviceControlsSupported || !_hasDeviceSlider || _handyDisconnecting || !ReferenceEquals(_handy, client)
                    || !ReferenceEquals(_strokeWriteCts, cancellation)) return;
                _strokeWriteInFlight = true;
                var accepted = await client.SetStrokeAsync(
                    DeviceControlLogic.ToRelativeStroke(minimum), DeviceControlLogic.ToRelativeStroke(maximum), token);
                if (token.IsCancellationRequested || !ReferenceEquals(_strokeWriteCts, cancellation)) return;
                _strokeWriteFailed = false;
                _strokeInitialReadFailed = false;
                _lastDeviceStroke = accepted;
                _strokeMinAbsolute = accepted.MinAbsolute;
                _strokeMaxAbsolute = accepted.MaxAbsolute;
                _strokeSlider.SetValues(
                    DeviceControlLogic.FromRelativeStroke(accepted.Min), DeviceControlLogic.FromRelativeStroke(accepted.Max));
                _settings.StrokeMinPercent = _strokeSlider.LowerValue;
                _settings.StrokeMaxPercent = _strokeSlider.UpperValue;
                UpdateStrokeLabel();
                _strokeState.Text = "applied";
                Log($"Device stroke applied: {accepted.Min:P0}-{accepted.Max:P0} ({accepted.MinAbsolute:F1}-{accepted.MaxAbsolute:F1} mm).");
            }
            finally
            {
                _strokeWriteInFlight = false;
                _deviceCommandGate.Release();
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
        }
        catch (OperationCanceledException ex)
        {
            if (ReferenceEquals(_strokeWriteCts, cancellation))
            {
                _strokeWriteFailed = true;
                _strokeState.Text = "not applied";
                Log($"Device stroke write timed out: {ex.Message}");
            }
        }
        catch (Exception ex) when (ex is HandyApiException or HttpRequestException or IOException or ObjectDisposedException)
        {
            if (!token.IsCancellationRequested && ReferenceEquals(_strokeWriteCts, cancellation))
            {
                _strokeWriteFailed = true;
                _strokeState.Text = "not applied";
                Log($"Device stroke write failed: {ex.Message}");
            }
        }
        finally
        {
            if (ReferenceEquals(_strokeWriteCts, cancellation)) _strokeWriteCts = null;
            cancellation.Dispose();
        }
    }

    private async Task PollDeviceValuesAsync()
    {
        var client = _handy;
        if (_devicePollBusy || client is null || !_deviceControlsSupported || !IsCurrentPollClient(client)) return;
        _devicePollBusy = true;
        if (!await _deviceCommandGate.WaitAsync(0))
        {
            _devicePollBusy = false;
            return;
        }

        var offsetVersion = _offsetEditVersion;
        var strokeVersion = _strokeEditVersion;
        var endpoint = "hstp/offset";
        try
        {
            var offset = await client.GetOffsetAsync(_lifetimeCts.Token);
            if (!IsCurrentPollClient(client)) return;
            _pollFailureCounts.Remove(endpoint);
            var mayApplyOffset = DeviceControlLogic.ShouldApplyPolledValue(
                offsetVersion, _offsetEditVersion, _offsetWriteCts is not null || _offsetWriteFailed,
                _offsetWriteInFlight, _offsetSlider.IsBeingAdjusted);
            var canAdoptOffset = mayApplyOffset
                                 && (!_offsetInitialReadFailed || offsetVersion == _offsetInitialReadEditVersion);
            if (_lastDeviceOffset is int previousOffset && previousOffset != offset && canAdoptOffset)
            {
                _offsetSlider.SetValues(offset, 0);
                UpdateOffsetLabel();
                _settings.DeviceOffsetMs = offset;
                _offsetState.Text = "changed by another app";
                Log($"Device offset changed externally: {previousOffset} -> {offset} ms.");
            }
            else if (_lastDeviceOffset is null && canAdoptOffset)
            {
                _offsetSlider.SetValues(offset, 0);
                UpdateOffsetLabel();
                _settings.DeviceOffsetMs = offset;
                _offsetState.Text = "applied";
                _offsetInitialReadFailed = false;
            }
            else if (_lastDeviceOffset == offset && mayApplyOffset && !_offsetWriteFailed
                     && _offsetState.Text == "device values unknown")
            {
                _offsetState.Text = "applied";
            }
            if (canAdoptOffset) _lastDeviceOffset = offset;

            if (_hasDeviceSlider)
            {
                if (!IsCurrentPollClient(client)) return;
                endpoint = "slider/stroke";
                var stroke = await client.GetStrokeAsync(_lifetimeCts.Token);
                if (!IsCurrentPollClient(client)) return;
                _pollFailureCounts.Remove(endpoint);
                var mayApplyStroke = DeviceControlLogic.ShouldApplyPolledValue(
                    strokeVersion, _strokeEditVersion, _strokeWriteCts is not null || _strokeWriteFailed,
                    _strokeWriteInFlight, _strokeSlider.IsBeingAdjusted);
                var canAdoptStroke = mayApplyStroke
                                     && (!_strokeInitialReadFailed || strokeVersion == _strokeInitialReadEditVersion);
                if (_lastDeviceStroke is { } previousStroke
                    && (previousStroke.Min != stroke.Min || previousStroke.Max != stroke.Max) && canAdoptStroke)
                {
                    _strokeSlider.SetValues(
                        DeviceControlLogic.FromRelativeStroke(stroke.Min), DeviceControlLogic.FromRelativeStroke(stroke.Max));
                    _settings.StrokeMinPercent = _strokeSlider.LowerValue;
                    _settings.StrokeMaxPercent = _strokeSlider.UpperValue;
                    _strokeState.Text = "changed by another app";
                    Log($"Device stroke changed externally: {previousStroke.Min:P0}-{previousStroke.Max:P0} -> {stroke.Min:P0}-{stroke.Max:P0}.");
                }
                else if (_lastDeviceStroke is null && canAdoptStroke)
                {
                    _strokeSlider.SetValues(
                        DeviceControlLogic.FromRelativeStroke(stroke.Min), DeviceControlLogic.FromRelativeStroke(stroke.Max));
                    _settings.StrokeMinPercent = _strokeSlider.LowerValue;
                    _settings.StrokeMaxPercent = _strokeSlider.UpperValue;
                    _strokeState.Text = "applied";
                    _strokeInitialReadFailed = false;
                }
                else if (_lastDeviceStroke is { } knownStroke
                         && knownStroke.Min == stroke.Min && knownStroke.Max == stroke.Max
                         && mayApplyStroke && !_strokeWriteFailed && _strokeState.Text == "device values unknown")
                {
                    _strokeState.Text = "applied";
                }
                if (canAdoptStroke)
                {
                    _lastDeviceStroke = stroke;
                    _strokeMinAbsolute = stroke.MinAbsolute;
                    _strokeMaxAbsolute = stroke.MaxAbsolute;
                    UpdateStrokeLabel();
                }
            }

            _devicePollTick++;
            if (_hasDeviceSlider && _devicePollTick % 3 == 0)
            {
                if (!IsCurrentPollClient(client)) return;
                endpoint = "slider/state";
                await UpdateTemperatureAsync(client);
                if (!IsCurrentPollClient(client)) return;
                _pollFailureCounts.Remove(endpoint);
            }
        }
        catch (OperationCanceledException) when (_lifetimeCts.IsCancellationRequested)
        {
        }
        catch (Exception ex) when (ex is HandyApiException or HttpRequestException or IOException or ObjectDisposedException or OperationCanceledException)
        {
            if (!IsCurrentPollClient(client)) return;
            var failureCount = _pollFailureCounts.GetValueOrDefault(endpoint) + 1;
            _pollFailureCounts[endpoint] = failureCount;
            if (failureCount == 1)
            {
                Log($"Device value polling failed: {endpoint}: {ex.Message}");
            }
            if (failureCount >= 3)
            {
                if (endpoint == "hstp/offset" && !_offsetWriteFailed) _offsetState.Text = "device values unknown";
                if (endpoint == "slider/stroke" && !_strokeWriteFailed) _strokeState.Text = "device values unknown";
            }
        }
        finally
        {
            _deviceCommandGate.Release();
            _devicePollBusy = false;
        }
    }

    private bool IsCurrentPollClient(HandyApiClient client) =>
        ReferenceEquals(_handy, client) && !_handyDisconnecting && !_isClosing && !_lifetimeCts.IsCancellationRequested;

    private async Task UpdateTemperatureAsync(HandyApiClient client)
    {
        if (!IsCurrentPollClient(client)) return;
        var state = await client.GetSliderStateAsync(_lifetimeCts.Token);
        if (!IsCurrentPollClient(client)) return;
        var temperature = state.MotorTemperature;
        var trigger = _temperatureHighTrigger ?? 75;
        var hysteresis = _temperatureHysteresis ?? 10;
        var previousBand = _temperatureBand;
        _temperatureBand = DeviceControlLogic.GetTemperatureBand(previousBand, temperature, trigger, hysteresis);

        _temperatureValue.Text = _temperatureBand switch
        {
            DeviceTemperatureBand.Hot => $"Motor {temperature:F1} °C (High)",
            DeviceTemperatureBand.Warm => $"Motor {temperature:F1} °C (Warm)",
            _ => $"Motor {temperature:F1} °C"
        };
        _temperatureValue.ForeColor = _temperatureBand == DeviceTemperatureBand.Warm ? Color.DarkGoldenrod : SystemColors.ControlText;
        _temperatureValue.BackColor = _temperatureBand == DeviceTemperatureBand.Hot ? Color.MistyRose : SystemColors.Control;
        if (previousBand != _temperatureBand)
            Log($"Motor temperature state: {previousBand} -> {_temperatureBand} ({temperature:F1} °C, trigger {trigger:F1} °C).");
        await Task.CompletedTask;
    }

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

        var expected = _lastKnownPos + (session.IsPaused ? 0 : _sincePosUpdate.Elapsed.TotalSeconds * (session.Speed ?? 1.0));
        var isSeek = Math.Abs(pos - expected) > 0.75;
        _lastKnownPos = pos;
        _sincePosUpdate.Restart();

        var client = _handy;
        if (!_hsspReady || client is null || session.Speed != 1.0) return;

        if (isSeek && _mpv is { IsPaused: false })
        {
            try
            {
                await _deviceCommandGate.WaitAsync(_lifetimeCts.Token);
                try
                {
                    if (!ReferenceEquals(_mpv, session) || !_hsspReady || !ReferenceEquals(_handy, client)
                        || session.IsPaused || session.Speed != 1.0) return;
                    await client.HsspPlayAsync(DeviceTimeMs(pos), EstimatedServerTimeMs(), ct: _lifetimeCts.Token);
                }
                finally
                {
                    _deviceCommandGate.Release();
                }

                Log($"Seek detected, resynced at {pos:F1}s.");
            }
            catch (Exception ex) when (ex is HandyApiException or HttpRequestException or TaskCanceledException or ObjectDisposedException)
            {
                Log($"Resync failed: {ex.Message}");
            }
        }
    }

    private void OnMpvSpeedChanged(MpvSession session)
    {
        if (_isClosing || IsDisposed || !IsHandleCreated) return;
        try
        {
            BeginInvoke(async () =>
            {
                if (_isClosing || !ReferenceEquals(_mpv, session)) return;
                await OnMpvPauseChangedAsync(session, session.IsPaused);
            });
        }
        catch (InvalidOperationException) when (_isClosing || IsDisposed || !IsHandleCreated)
        {
        }
    }

    private async Task SetVideoPausedAsync(bool paused)
    {
        var session = _mpv;
        if (!_mpvAttached || session is null || session.IsPaused == paused || _videoTransportPending) return;

        _videoTransportPending = true;
        UpdateVideoTransportButtons(session);
        try
        {
            await session.SetPauseAsync(paused, _lifetimeCts.Token);
            var wait = Stopwatch.StartNew();
            while (ReferenceEquals(_mpv, session) && session.IsPaused != paused && wait.Elapsed < TimeSpan.FromSeconds(2))
                await Task.Delay(25, _lifetimeCts.Token);

            if (ReferenceEquals(_mpv, session) && session.IsPaused != paused)
                Log($"mpv did not confirm video {(paused ? "pause" : "play")}.");
        }
        catch (Exception ex) when (ex is IOException or ObjectDisposedException or TaskCanceledException)
        {
            Log($"Could not {(paused ? "pause" : "play")} mpv video: {ex.Message}");
        }
        finally
        {
            _videoTransportPending = false;
            UpdateVideoTransportButtons(session);
        }
    }

    private void UpdateVideoTransportButtons(MpvSession? session)
    {
        if (IsDisposed) return;
        if (InvokeRequired)
        {
            try { BeginInvoke(() => UpdateVideoTransportButtons(session)); }
            catch (InvalidOperationException) when (IsDisposed || !IsHandleCreated) { }
            return;
        }

        var attached = _mpvAttached && session is not null && ReferenceEquals(_mpv, session);
        _attachMpvButton.Enabled = !_isClosing && !_mpvAttachInProgress && !attached && _mpv is null;
        _detachMpvButton.Enabled = !_isClosing && !_mpvDetaching && attached;
        _playVideoButton.Enabled = attached && !_videoTransportPending && session!.IsPaused;
        _pauseVideoButton.Enabled = attached && !_videoTransportPending && !session!.IsPaused;
    }

    private async Task OnMpvPauseChangedAsync(MpvSession session, bool paused)
    {
        if (!ReferenceEquals(_mpv, session)) return;
        _videoTransportPending = false;
        UpdateVideoTransportButtons(session);
        var client = _handy;
        if (!_hsspReady || client is null)
        {
            SetStatus(session.IsPaused ? "Video paused (Handy not ready)" : "Video playing (Handy not ready)");
            return;
        }

        try
        {
            await _deviceCommandGate.WaitAsync(_lifetimeCts.Token);
            try
            {
                if (!ReferenceEquals(_mpv, session) || !_hsspReady || !ReferenceEquals(_handy, client)) return;

                if (session.Speed != 1.0)
                {
                    Log("Device synchronization suspended: mpv playback speed must be confirmed at 1x. Restore 1x to resume.");
                    SetStatus("Device sync suspended: requires 1x playback");
                    await client.HsspStopAsync(_lifetimeCts.Token);
                    SetStatus("Device stopped: requires 1x playback");
                }
                else if (session.IsPaused)
                {
                    await client.HsspStopAsync(_lifetimeCts.Token);
                    Log("Playback paused; device stopped.");
                    SetStatus("Paused");
                }
                else
                {
                    await client.HsspPlayAsync(DeviceTimeMs(session.PositionSeconds), EstimatedServerTimeMs(), ct: _lifetimeCts.Token);
                    Log("Playback resumed; device started.");
                    SetStatus("Playing");
                }
            }
            finally
            {
                _deviceCommandGate.Release();
            }
        }
        catch (Exception ex) when (ex is HandyApiException or HttpRequestException or TaskCanceledException or ObjectDisposedException)
        {
            Log($"Play/stop failed: {ex.Message}");
            if (ReferenceEquals(_mpv, session) && session.Speed != 1.0)
                SetStatus("Device stop failed: pause mpv and disconnect Handy");
        }
    }

    private async Task OnSyncTickAsync()
    {
        var client = _handy;
        var session = _mpv;
        if (!_hsspReady || client is null || session is not { IsPaused: false, Speed: 1.0 }) return;
        try
        {
            await _deviceCommandGate.WaitAsync(_lifetimeCts.Token);
            try
            {
                if (!_hsspReady || !ReferenceEquals(_handy, client) || !ReferenceEquals(_mpv, session)
                    || session.IsPaused || session.Speed != 1.0) return;
                await client.HsspSyncTimeAsync(DeviceTimeMs(_lastKnownPos), EstimatedServerTimeMs(), ct: _lifetimeCts.Token);
            }
            finally
            {
                _deviceCommandGate.Release();
            }
        }
        catch (Exception ex) when (ex is HandyApiException or HttpRequestException or TaskCanceledException or ObjectDisposedException)
        {
            Log($"Drift correction failed: {ex.Message}");
        }
    }

    // Detaches from mpv; the Handy connection outlives it so a new attachment can reuse the HSSP setup.
    private async Task DetachMpvAsync(bool stopDevice = true)
    {
        if (_mpvDetaching) return;
        _mpvDetaching = true;
        UpdateVideoTransportButtons(_mpv);
        await _sessionTransitionGate.WaitAsync();
        try
        {
            await DetachMpvCoreAsync(stopDevice);
        }
        finally
        {
            _sessionTransitionGate.Release();
            _mpvDetaching = false;
            UpdateVideoTransportButtons(_mpv);
        }
    }

    private async Task DetachMpvCoreAsync(bool stopDevice = true)
    {
        _syncTimer.Stop();
        var client = _handy;
        _hsspReady = false;
        _mpvAttached = false;
        _videoTransportPending = false;
        _mpv?.Dispose();
        _mpv = null;
        UpdateVideoTransportButtons(null);
        _waveform.SetMediaDuration(0);

        if (stopDevice && client is not null)
        {
            await _deviceCommandGate.WaitAsync();
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
                _deviceCommandGate.Release();
            }
        }

        if (stopDevice && client is not null && ReferenceEquals(_handy, client) && _script is not null)
        {
            try
            {
                await EnsureCurrentScriptInstalledAsync(client, _lifetimeCts.Token);
            }
            catch (Exception ex) when (ex is HandyApiException or IOException or HttpRequestException or TaskCanceledException or ObjectDisposedException)
            {
                if (ReferenceEquals(_handy, client))
                {
                    Log($"Could not install the selected script after stopping: {ex.Message}");
                    await DisconnectHandyAsync();
                }
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
