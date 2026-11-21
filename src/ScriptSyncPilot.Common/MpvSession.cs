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
    public event Action? Disconnected;

    private NamedPipeClientStream? _pipe;
    private CancellationTokenSource? _readCts;
    private volatile bool _disposed;

    public bool IsPaused { get; private set; } = true;
    public double PositionSeconds { get; private set; }
    public double DurationSeconds { get; private set; }
    public double? Speed { get; private set; }

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

        _readCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        _ = ReadLoopAsync(_readCts.Token);

        try
        {
            await SendAsync(new { command = new object[] { "observe_property", 1, "time-pos" } }, ct);
            await SendAsync(new { command = new object[] { "observe_property", 2, "pause" } }, ct);
            await SendAsync(new { command = new object[] { "observe_property", 3, "duration" } }, ct);
            await SendAsync(new { command = new object[] { "observe_property", 4, "speed" } }, ct);
        }
        catch
        {
            CleanupFailedAttach();
            throw;
        }
    }

    public Task SeekAsync(double seconds, CancellationToken ct) =>
        SendAsync(new { command = new object[] { "seek", seconds, "absolute+exact" } }, ct);

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
        if (_pipe is not { IsConnected: true })
            throw new IOException("mpv IPC pipe is not connected.");
        var json = JsonSerializer.Serialize(command) + "\n";
        var bytes = Encoding.UTF8.GetBytes(json);
        await _pipe.WriteAsync(bytes, ct);
        await _pipe.FlushAsync(ct);
    }

    private void CleanupFailedAttach()
    {
        var pipe = _pipe;
        _pipe = null;
        var readCts = _readCts;
        _readCts = null;
        readCts?.Cancel();
        pipe?.Dispose();
        readCts?.Dispose();
    }

    private async Task ReadLoopAsync(CancellationToken ct)
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
            if (!_disposed)
                Disconnected?.Invoke();
        }
    }

    private void HandleLine(string line)
    {
        if (line.Length == 0) return;
        using var doc = JsonDocument.Parse(line);
        var root = doc.RootElement;
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
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        var readCts = _readCts;
        _readCts = null;
        readCts?.Cancel();
        _pipe?.Dispose();
        _pipe = null;
        readCts?.Dispose();
    }
}
