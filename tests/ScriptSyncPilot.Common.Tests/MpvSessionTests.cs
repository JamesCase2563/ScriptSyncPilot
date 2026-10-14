using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ScriptSyncPilot.Common;

namespace ScriptSyncPilot.Common.Tests;

[TestClass]
public sealed class MpvSessionTests
{
    [TestMethod]
    public async Task Attach_ObservesSpeedWithDistinctIdWithoutChangingPlayerSpeed()
    {
        using var endpoint = new PipeEndpoint();
        Assert.IsNull(endpoint.Session.Speed);
        await endpoint.ConnectAsync();

        var names = new[] { "time-pos", "pause", "duration", "speed" };
        for (var index = 0; index < names.Length; index++)
            await endpoint.AssertCommandAsync("observe_property", index + 1, names[index]);
        await endpoint.Attachment.WaitAsync(endpoint.Token);

        await endpoint.Session.SeekAsync(7, endpoint.Token);
        await endpoint.AssertCommandAsync("seek", 7, "absolute+exact");
        Assert.IsNull(endpoint.Session.Speed);
    }

    [TestMethod]
    public async Task SpeedObservations_ReportConfiguredRateIncludingOutsideEditorBounds()
    {
        using var endpoint = new PipeEndpoint();
        var speeds = Channel.CreateUnbounded<double>();
        endpoint.Session.SpeedChanged += speed => speeds.Writer.TryWrite(speed);
        await endpoint.ConnectAndDrainAsync();

        foreach (var speed in new[] { 1.0, 0.5, 0.01, 3.0, 100.0 })
        {
            await endpoint.SendPropertyAsync("speed", speed);
            Assert.AreEqual(speed, await speeds.Reader.ReadAsync(endpoint.Token));
            Assert.AreEqual((double?)speed, endpoint.Session.Speed);
        }
    }

    [TestMethod]
    public async Task InvalidSpeedObservations_PreserveStateAndReaderContinues()
    {
        using var endpoint = new PipeEndpoint();
        var speeds = Channel.CreateUnbounded<double>();
        var durations = Channel.CreateUnbounded<double>();
        endpoint.Session.SpeedChanged += speed => speeds.Writer.TryWrite(speed);
        endpoint.Session.DurationChanged += duration => durations.Writer.TryWrite(duration);
        await endpoint.ConnectAndDrainAsync();

        await endpoint.SendPropertyAsync("speed", 0.75);
        Assert.AreEqual(0.75, await speeds.Reader.ReadAsync(endpoint.Token));
        foreach (var value in new[] { "null", "\"fast\"", "true", "{}", "[]", "0", "-1", "1e400" })
            await endpoint.SendRawAsync($"{{\"event\":\"property-change\",\"name\":\"speed\",\"data\":{value}}}");
        await endpoint.SendRawAsync("{malformed");
        await endpoint.SendPropertyAsync("duration", 12);
        Assert.AreEqual(12.0, await durations.Reader.ReadAsync(endpoint.Token));
        Assert.AreEqual((double?)0.75, endpoint.Session.Speed);
        Assert.IsFalse(speeds.Reader.TryRead(out _));

        await endpoint.SendPropertyAsync("speed", 1.25);
        Assert.AreEqual(1.25, await speeds.Reader.ReadAsync(endpoint.Token));
        Assert.AreEqual((double?)1.25, endpoint.Session.Speed);
    }

    [TestMethod]
    public async Task SetSpeed_WritesStructuredCommandWithoutOptimisticStateChange()
    {
        using var endpoint = new PipeEndpoint();
        await endpoint.ConnectAndDrainAsync();
        foreach (var speed in new[] { 0.01, 0.5, 1.0, 2.0, 3.0, 100.0 })
        {
            await endpoint.Session.SetSpeedAsync(speed, endpoint.Token);
            await endpoint.AssertCommandAsync("set_property", "speed", speed);
            Assert.IsNull(endpoint.Session.Speed);
        }
    }

    [TestMethod]
    public async Task SetSpeed_InvalidValuesAndCancelledWriteDoNotSendCommands()
    {
        using var endpoint = new PipeEndpoint();
        await endpoint.ConnectAndDrainAsync();
        foreach (var speed in new[] { 0.0, -1.0, double.NaN, double.PositiveInfinity, double.NegativeInfinity })
            Assert.ThrowsException<ArgumentOutOfRangeException>(() => endpoint.Session.SetSpeedAsync(speed, endpoint.Token));

        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        await AssertCancelledAsync(() => endpoint.Session.SetSpeedAsync(0.5, cancelled.Token));
        await endpoint.Session.SetPauseAsync(true, endpoint.Token);
        await endpoint.AssertCommandAsync("set_property", "pause", true);
        Assert.IsNull(endpoint.Session.Speed);
    }

    [TestMethod]
    public async Task ExistingPropertiesAndPlaybackCommands_RetainMediaTimeSemantics()
    {
        using var endpoint = new PipeEndpoint();
        var positions = Channel.CreateUnbounded<double>();
        var pauses = Channel.CreateUnbounded<bool>();
        var durations = Channel.CreateUnbounded<double>();
        endpoint.Session.PositionChanged += value => positions.Writer.TryWrite(value);
        endpoint.Session.PauseChanged += value => pauses.Writer.TryWrite(value);
        endpoint.Session.DurationChanged += value => durations.Writer.TryWrite(value);
        await endpoint.ConnectAndDrainAsync();
        await endpoint.SendPropertyAsync("speed", 0.5);
        await endpoint.SendPropertyAsync("time-pos", 12.5);
        await endpoint.SendPropertyAsync("pause", false);
        await endpoint.SendPropertyAsync("duration", 60);
        Assert.AreEqual(12.5, await positions.Reader.ReadAsync(endpoint.Token));
        Assert.IsFalse(await pauses.Reader.ReadAsync(endpoint.Token));
        Assert.AreEqual(60.0, await durations.Reader.ReadAsync(endpoint.Token));
        Assert.AreEqual(12.5, endpoint.Session.PositionSeconds);
        Assert.IsFalse(endpoint.Session.IsPaused);
        Assert.AreEqual(60.0, endpoint.Session.DurationSeconds);

        await endpoint.Session.SeekAsync(20, endpoint.Token);
        await endpoint.AssertCommandAsync("seek", 20, "absolute+exact");
        await endpoint.Session.SeekRelativeAsync(-1, endpoint.Token);
        await endpoint.AssertCommandAsync("seek", -1, "relative+exact");
        await endpoint.Session.StepFrameAsync(true, endpoint.Token);
        await endpoint.AssertCommandAsync("frame-step");
        await endpoint.Session.StepFrameAsync(false, endpoint.Token);
        await endpoint.AssertCommandAsync("frame-back-step");
        await endpoint.Session.SetPauseAsync(true, endpoint.Token);
        await endpoint.AssertCommandAsync("set_property", "pause", true);
    }

    [TestMethod]
    public async Task ServerDisconnect_NotifiesOnceAndSpeedWriteReportsTransportFailure()
    {
        using var endpoint = new PipeEndpoint();
        var disconnected = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var notifications = 0;
        endpoint.Session.Disconnected += () =>
        {
            Interlocked.Increment(ref notifications);
            disconnected.TrySetResult();
        };
        await endpoint.ConnectAndDrainAsync();
        endpoint.Server.Dispose();
        await disconnected.Task.WaitAsync(endpoint.Token);
        await Assert.ThrowsExceptionAsync<IOException>(() => endpoint.Session.SetSpeedAsync(0.5, endpoint.Token));
        Assert.AreEqual(1, Volatile.Read(ref notifications));
    }

    [TestMethod]
    public async Task LifetimeCancellation_EndsObservationWithoutHanging()
    {
        using var endpoint = new PipeEndpoint();
        using var lifetime = new CancellationTokenSource();
        var disconnected = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        endpoint.Session.Disconnected += () => disconnected.TrySetResult();
        await endpoint.ConnectAndDrainAsync(lifetime.Token);
        lifetime.Cancel();
        await disconnected.Task.WaitAsync(endpoint.Token);
    }

    [TestMethod]
    public async Task Attach_PreCancelledTokenDoesNotConnect()
    {
        using var session = new MpvSession();
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        await AssertCancelledAsync(() =>
            session.AttachAsync($"ssp-mpv-missing-{Guid.NewGuid():N}", cancelled.Token));
        Assert.IsNull(session.Speed);
    }

    private static async Task AssertCancelledAsync(Func<Task> action)
    {
        try { await action(); }
        catch (OperationCanceledException) { return; }
        Assert.Fail("Expected cancellation.");
    }

    private sealed class PipeEndpoint : IDisposable
    {
        private readonly string _name = $"ssp-mpv-test-{Guid.NewGuid():N}";
        private readonly CancellationTokenSource _timeout = new(TimeSpan.FromSeconds(10));
        private readonly StreamReader _reader;
        private readonly Channel<string> _commands = Channel.CreateUnbounded<string>();
        private Task _readTask = Task.CompletedTask;

        public NamedPipeServerStream Server { get; }
        public MpvSession Session { get; } = new();
        public CancellationToken Token => _timeout.Token;
        public Task Attachment { get; private set; } = Task.CompletedTask;

        public PipeEndpoint()
        {
            Server = new NamedPipeServerStream(_name, PipeDirection.InOut, 1,
                PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
            _reader = new StreamReader(Server, Encoding.UTF8, leaveOpen: true);
        }

        public async Task ConnectAsync(CancellationToken? lifetime = null)
        {
            var connection = Server.WaitForConnectionAsync(Token);
            Attachment = Session.AttachAsync($@"\\.\pipe\{_name}", lifetime ?? Token);
            await connection;
            _readTask = ReadCommandsAsync();
        }

        private async Task ReadCommandsAsync()
        {
            try
            {
                while (await _reader.ReadLineAsync(Token).ConfigureAwait(false) is { } line)
                    _commands.Writer.TryWrite(line);
                _commands.Writer.TryComplete();
            }
            catch (Exception exception) when (exception is OperationCanceledException or IOException or ObjectDisposedException)
            {
                _commands.Writer.TryComplete(exception);
            }
        }

        public async Task ConnectAndDrainAsync(CancellationToken? lifetime = null)
        {
            await ConnectAsync(lifetime);
            foreach (var name in new[] { "time-pos", "pause", "duration", "speed" })
            {
                using var command = JsonDocument.Parse(await _commands.Reader.ReadAsync(Token));
                Assert.AreEqual("observe_property", command.RootElement.GetProperty("command")[0].GetString());
                Assert.AreEqual(name, command.RootElement.GetProperty("command")[2].GetString());
            }
            await Attachment.WaitAsync(Token);
        }

        public async Task AssertCommandAsync(params object[] expected)
        {
            var line = await _commands.Reader.ReadAsync(Token);
            using var document = JsonDocument.Parse(line);
            var actual = document.RootElement.GetProperty("command");
            Assert.AreEqual(expected.Length, actual.GetArrayLength());
            for (var index = 0; index < expected.Length; index++)
            {
                switch (expected[index])
                {
                    case string text: Assert.AreEqual(text, actual[index].GetString()); break;
                    case bool boolean: Assert.AreEqual(boolean, actual[index].GetBoolean()); break;
                    default: Assert.AreEqual(Convert.ToDouble(expected[index]), actual[index].GetDouble()); break;
                }
            }
        }

        public Task SendPropertyAsync(string name, object value) =>
            SendRawAsync(JsonSerializer.Serialize(new { @event = "property-change", name, data = value }));

        public async Task SendRawAsync(string line)
        {
            await Server.WriteAsync(Encoding.UTF8.GetBytes(line + "\n"), Token);
            await Server.FlushAsync(Token);
        }

        public void Dispose()
        {
            Session.Dispose();
            _timeout.Cancel();
            Server.Dispose();
            _readTask.GetAwaiter().GetResult();
            _reader.Dispose();
            _timeout.Dispose();
        }
    }
}