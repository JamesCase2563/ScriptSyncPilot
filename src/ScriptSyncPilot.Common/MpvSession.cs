using System.Collections.Concurrent;
using System.IO.Pipes;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

namespace ScriptSyncPilot.Common;

/// <summary>
/// Attaches to an already-running mpv instance's JSON IPC named pipe and
/// exposes playback position, pause state, and seeks.
///
/// Deliberately does not launch mpv itself: dewarping fisheye/equirect VR
/// video needs VR-Flat-Player's mpv.conf, mpv360 shader and head-tracked
/// viewport, none of which a plain `mpv &lt;file&gt;` provides. Attaching to the
/// player that is already showing the (correctly projected) video keeps a
/// client in sync with what the user actually sees, with no duplicated VR
/// pipeline. Point pipePath at VR-Flat-Player's BridgeConfig.Mpv.IpcPipe
/// (default \\.\pipe\vrheadtrack), or any other mpv started with a matching
/// --input-ipc-server.
/// </summary>
public sealed class MpvSession : IDisposable
{
    public event Action<double>? PositionChanged;
    public event Action<bool>? PauseChanged;
    public event Action<double>? DurationChanged;
    public event Action<double>? SpeedChanged;
    public event Action<double?>? FrameRateChanged;
    public event Action? Disconnected;

    private NamedPipeClientStream? _pipe;
    private CancellationTokenSource? _readCts;
    private AttachReadState? _readState;
    private volatile bool _disposed;
    private readonly ConcurrentDictionary<int, TaskCompletionSource<double>> _doublePropertyQueries = new();
    private readonly SemaphoreSlim _writeGate = new(1, 1);
    private int _nextRequestId;

    public bool IsPaused { get; private set; } = true;
    public double PositionSeconds { get; private set; }
    public double DurationSeconds { get; private set; }
    public double? Speed { get; private set; }
    public double? FrameRate { get; private set; }
    private double? EstimatedFrameRate { get; set; }
    private double? ContainerFrameRate { get; set; }

    public async Task AttachAsync(string pipePath, CancellationToken ct)
    {
        const string prefix = @"\\.\pipe\";
        var pipeName = pipePath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
            ? pipePath[prefix.Length..]
            : pipePath;

        _pipe = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
        try
        {
            await _pipe.ConnectAsync(5000, ct);
        }
        catch (TimeoutException)
        {
            CleanupFailedAttach();
            throw new IOException(
                $"Could not connect to mpv IPC pipe '{pipePath}'. Start mpv (or VR-Flat-Player) with " +
                $"--input-ipc-server={pipePath} first.");
        }
        catch
        {
            CleanupFailedAttach();
            throw;
        }

        var readState = new AttachReadState();
        _readState = readState;
        _readCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        _ = ReadLoopAsync(_readCts.Token, readState);

        try
        {
            await SendAsync(new { command = new object[] { "observe_property", 1, "time-pos" } }, ct);
            await SendAsync(new { command = new object[] { "observe_property", 2, "pause" } }, ct);
            await SendAsync(new { command = new object[] { "observe_property", 3, "duration" } }, ct);
            await SendAsync(new { command = new object[] { "observe_property", 4, "speed" } }, ct);
            await SendAsync(new { command = new object[] { "observe_property", 5, "estimated-vf-fps" } }, ct);
            await SendAsync(new { command = new object[] { "observe_property", 6, "container-fps" } }, ct);
            readState.AttachCompleted.TrySetResult(true);
        }
        catch
        {
            CleanupFailedAttach(readState);
            throw;
        }
    }

    public Task SeekAsync(double seconds, CancellationToken ct) =>
        SendAsync(new { command = new object[] { "seek", seconds, "absolute+exact" } }, ct);

    public Task<double> GetPositionAsync(CancellationToken ct) => GetDoublePropertyAsync("time-pos", ct);

    public async Task<double?> GetFrameRateAsync(CancellationToken ct)
    {
        try
        {
            var estimated = await GetDoublePropertyAsync("estimated-vf-fps", ct);
            if (double.IsFinite(estimated) && estimated > 0) return estimated;
        }
        catch (IOException) when (!ct.IsCancellationRequested)
        {
            // Fall back to the file header rate when no measured rate is available.
        }

        try
        {
            var container = await GetDoublePropertyAsync("container-fps", ct);
            return double.IsFinite(container) && container > 0 ? container : null;
        }
        catch (IOException) when (!ct.IsCancellationRequested)
        {
            return null;
        }
    }

    private async Task<double> GetDoublePropertyAsync(string property, CancellationToken ct)
    {
        var requestId = Interlocked.Increment(ref _nextRequestId);
        var response = new TaskCompletionSource<double>(TaskCreationOptions.RunContinuationsAsynchronously);
        if (!_doublePropertyQueries.TryAdd(requestId, response))
            throw new InvalidOperationException($"Could not register the mpv '{property}' query.");

        try
        {
            await SendAsync(new { command = new object[] { "get_property", property }, request_id = requestId }, ct);
            return await response.Task.WaitAsync(ct);
        }
        finally
        {
            _doublePropertyQueries.TryRemove(requestId, out _);
        }
    }

    /// <summary>Seeks by a relative offset (negative to go backwards), frame-accurate rather than keyframe-snapped.</summary>
    public Task SeekRelativeAsync(double deltaSeconds, CancellationToken ct) =>
        SendAsync(new { command = new object[] { "seek", deltaSeconds, "relative+exact" } }, ct);

    /// <summary>Steps exactly one video frame; also pauses, mirroring mpv's own frame-step behavior.</summary>
    public Task StepFrameAsync(bool forward, CancellationToken ct) =>
        SendAsync(new { command = new object[] { forward ? "frame-step" : "frame-back-step" } }, ct);

    public Task SetPauseAsync(bool paused, CancellationToken ct) =>
        SendAsync(new { command = new object[] { "set_property", "pause", paused } }, ct);

    public Task SetSpeedAsync(double speed, CancellationToken ct)
    {
        if (!double.IsFinite(speed) || speed <= 0)
            throw new ArgumentOutOfRangeException(nameof(speed), "Speed must be finite and greater than zero.");
        return SendAsync(new { command = new object[] { "set_property", "speed", speed } }, ct);
    }

    private async Task SendAsync(object command, CancellationToken ct)
    {
        await _writeGate.WaitAsync(ct);
        try
        {
            if (_pipe is not { IsConnected: true })
                throw new IOException("mpv IPC pipe is not connected.");
            var json = JsonSerializer.Serialize(command) + "\n";
            var bytes = Encoding.UTF8.GetBytes(json);
            await _pipe.WriteAsync(bytes, ct);
            await _pipe.FlushAsync(ct);
        }
        finally
        {
            _writeGate.Release();
        }
    }

    private void CleanupFailedAttach(AttachReadState? readState = null)
    {
        (readState ?? _readState)?.AttachCompleted.TrySetResult(false);
        var pipe = _pipe;
        _pipe = null;
        var readCts = _readCts;
        _readCts = null;
        readCts?.Cancel();
        pipe?.Dispose();
        readCts?.Dispose();
    }

    private async Task ReadLoopAsync(CancellationToken ct, AttachReadState readState)
    {
        if (_pipe is null) return;
        using var reader = new StreamReader(_pipe, Encoding.UTF8, leaveOpen: true);
        try
        {
            while (!ct.IsCancellationRequested)
            {
                var line = await reader.ReadLineAsync(ct);
                if (line is null) break; // pipe closed: mpv exited
                try { HandleLine(line); }
                catch (JsonException) { /* ignore malformed/partial IPC lines */ }
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (IOException)
        {
        }
        catch (SocketException)
        {
        }
        catch (ObjectDisposedException)
        {
        }
        finally
        {
            if (await readState.AttachCompleted.Task.ConfigureAwait(false) && !_disposed)
                Disconnected?.Invoke();
        }
    }

    private sealed class AttachReadState
    {
        public TaskCompletionSource<bool> AttachCompleted { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    private void HandleLine(string line)
    {
        if (line.Length == 0) return;
        using var doc = JsonDocument.Parse(line);
        var root = doc.RootElement;
        if (root.TryGetProperty("request_id", out var requestIdElement)
            && requestIdElement.TryGetInt32(out var requestId)
            && _doublePropertyQueries.TryGetValue(requestId, out var doublePropertyQuery))
        {
            if (root.TryGetProperty("error", out var error)
                && error.ValueKind == JsonValueKind.String
                && error.GetString() == "success"
                && root.TryGetProperty("data", out var position)
                && position.ValueKind == JsonValueKind.Number
                && position.TryGetDouble(out var seconds)
                && double.IsFinite(seconds))
                doublePropertyQuery.TrySetResult(seconds);
            else
                doublePropertyQuery.TrySetException(new IOException("mpv could not read the requested numeric property."));
            return;
        }
        if (!root.TryGetProperty("event", out var evt) || evt.GetString() != "property-change") return;
        if (!root.TryGetProperty("name", out var name) || !root.TryGetProperty("data", out var data)) return;

        switch (name.GetString())
        {
            case "time-pos" when data.ValueKind == JsonValueKind.Number:
                PositionSeconds = data.GetDouble();
                PositionChanged?.Invoke(PositionSeconds);
                break;
            case "pause" when data.ValueKind is JsonValueKind.True or JsonValueKind.False:
                IsPaused = data.GetBoolean();
                PauseChanged?.Invoke(IsPaused);
                break;
            case "duration" when data.ValueKind == JsonValueKind.Number:
                DurationSeconds = data.GetDouble();
                DurationChanged?.Invoke(DurationSeconds);
                break;
            case "speed" when data.ValueKind == JsonValueKind.Number
                && data.TryGetDouble(out var speed) && double.IsFinite(speed) && speed > 0:
                Speed = speed;
                SpeedChanged?.Invoke(speed);
                break;
            case "estimated-vf-fps":
                EstimatedFrameRate = ReadPositiveNumber(data);
                UpdateFrameRate();
                break;
            case "container-fps":
                ContainerFrameRate = ReadPositiveNumber(data);
                UpdateFrameRate();
                break;
        }
    }

    private static double? ReadPositiveNumber(JsonElement value) =>
        value.ValueKind == JsonValueKind.Number && value.TryGetDouble(out var number)
        && double.IsFinite(number) && number > 0 ? number : null;

    private void UpdateFrameRate()
    {
        var frameRate = EstimatedFrameRate ?? ContainerFrameRate;
        if (FrameRate == frameRate) return;
        FrameRate = frameRate;
        FrameRateChanged?.Invoke(frameRate);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _readState?.AttachCompleted.TrySetResult(false);
        var readCts = _readCts;
        _readCts = null;
        readCts?.Cancel();
        _pipe?.Dispose();
        _pipe = null;
        foreach (var query in _doublePropertyQueries.Values)
            query.TrySetException(new ObjectDisposedException(nameof(MpvSession)));
        readCts?.Dispose();
    }
}
