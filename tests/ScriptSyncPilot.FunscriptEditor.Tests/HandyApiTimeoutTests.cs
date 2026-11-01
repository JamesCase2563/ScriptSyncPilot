using System.Net;
using System.Net.Http;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Reflection;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ScriptSyncPilot.Common;
using ScriptSyncPilot.HandySync;
using System.Windows.Forms;

namespace ScriptSyncPilot.FunscriptEditor.Tests;

[TestClass]
public sealed class HandyApiTimeoutTests
{
    private static readonly TimeSpan ShortTimeout = TimeSpan.FromMilliseconds(100);
    private const string SyntheticSecret = "synthetic-handler-secret";

    [TestMethod]
    public async Task RestRequestTimeout_CancelsTransport_AndReturnsSafeTimeoutMessage()
    {
        using var handler = new BlockingHandler();
        using var client = CreateClient(handler, controlTimeout: ShortTimeout);

        var request = client.IsConnectedAsync(CancellationToken.None);
        await handler.Started.Task.WaitAsync(TimeSpan.FromSeconds(2));
        var error = await Assert.ThrowsExceptionAsync<HandyApiTimeoutException>(() => request);

        Assert.IsTrue(handler.CancellationObserved.Task.IsCompleted);
        Assert.AreEqual("connected", error.Operation);
        Assert.AreEqual("connected: request timed out.", error.Message);
        Assert.IsNull(error.InnerException, "Transport exception details must not be retained in the public timeout error.");
        Assert.IsFalse(error.Message.Contains(SyntheticSecret, StringComparison.Ordinal));
    }

    [TestMethod]
    public async Task DisconnectStop_IgnoresCanceledRequestLifetime_ButKeepsControlTimeout()
    {
        using var handler = new BlockingHandler();
        using var client = CreateClient(handler, controlTimeout: ShortTimeout);
        client.CancelPendingRequests();

        var stopwatch = Stopwatch.StartNew();
        var request = client.HsspStopForDisconnectAsync();
        await handler.Started.Task.WaitAsync(TimeSpan.FromSeconds(2));
        var error = await Assert.ThrowsExceptionAsync<HandyApiTimeoutException>(() => request);

        Assert.IsTrue(handler.CancellationObserved.Task.IsCompleted);
        Assert.AreEqual("hssp/stop: request timed out.", error.Message);
        Assert.IsTrue(stopwatch.Elapsed < TimeSpan.FromSeconds(2));
        Assert.IsFalse(error.Message.Contains(SyntheticSecret, StringComparison.Ordinal));
    }

    [TestMethod]
    public async Task ClockSampling_BoundsEachSampleIndividually_AndAbortsAtTheStalledSample()
    {
        using var handler = new ServerTimeHandler(stallAtRequest: 3);
        using var client = CreateClient(handler, serverTimeTimeout: ShortTimeout);

        var stopwatch = Stopwatch.StartNew();
        var error = await Assert.ThrowsExceptionAsync<HandyApiTimeoutException>(
            () => client.EstimateClockOffsetMsAsync(15, CancellationToken.None));

        Assert.AreEqual("servertime: request timed out.", error.Message);
        Assert.AreEqual(3, handler.RequestCount, "Sampling must abort at the stalled sample without issuing the remaining requests.");
        Assert.IsTrue(stopwatch.Elapsed < TimeSpan.FromSeconds(2),
            "A stalled sample must fail within its per-sample budget, not the aggregate sampling bound.");
        Assert.IsFalse(error.Message.Contains(SyntheticSecret, StringComparison.Ordinal));
    }

    [TestMethod]
    public async Task HostingUploadTimeout_CancelsTransport_AndReturnsSafeTimeoutMessage()
    {
        using var deviceHandler = new NeverUsedHandler();
        using var hostingHandler = new BlockingHandler();
        using var client = CreateClient(deviceHandler, hostingHandler, uploadTimeout: ShortTimeout);

        var request = client.UploadScriptCsvAsync("0,0", CancellationToken.None);
        await hostingHandler.Started.Task.WaitAsync(TimeSpan.FromSeconds(2));
        var error = await Assert.ThrowsExceptionAsync<HandyApiTimeoutException>(() => request);

        Assert.IsTrue(hostingHandler.CancellationObserved.Task.IsCompleted);
        Assert.AreEqual("script upload: request timed out.", error.Message);
        Assert.IsFalse(error.Message.Contains(SyntheticSecret, StringComparison.Ordinal));
    }

    [TestMethod]
    public async Task ResponseBodyTransferTimeout_CancelsContentRead()
    {
        using var content = new BlockingContent();
        using var handler = new ContentHandler(content);
        using var client = CreateClient(handler, controlTimeout: ShortTimeout);

        var request = client.GetInfoAsync(CancellationToken.None);
        await content.Started.Task.WaitAsync(TimeSpan.FromSeconds(2));
        var error = await Assert.ThrowsExceptionAsync<HandyApiTimeoutException>(() => request);

        Assert.IsTrue(content.CancellationObserved.Task.IsCompleted);
        Assert.AreEqual("info: request timed out.", error.Message);
        Assert.IsFalse(error.Message.Contains(SyntheticSecret, StringComparison.Ordinal));
    }

    [TestMethod]
    public async Task CallerCancellation_RemainsCancellation_AndIsNotReportedAsTimeout()
    {
        using var handler = new BlockingHandler();
        using var client = CreateClient(handler, controlTimeout: TimeSpan.FromSeconds(5));
        using var cancellation = new CancellationTokenSource();

        var request = client.IsConnectedAsync(cancellation.Token);
        await handler.Started.Task.WaitAsync(TimeSpan.FromSeconds(2));
        cancellation.Cancel();
        var error = await Assert.ThrowsExceptionAsync<TaskCanceledException>(() => request);

        Assert.IsTrue(handler.CancellationObserved.Task.IsCompleted);
        Assert.AreEqual("connected: request canceled.", error.Message);
        Assert.IsFalse(error.Message.Contains("timed out", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(error.Message.Contains(SyntheticSecret, StringComparison.Ordinal));
    }

    [STATestMethod]
    public void FirmwareReadTimeout_AbortsConnectBeforeModeUploadSetupOrPlay()
    {
        using var deviceHandler = new ConnectSequenceHandler(blockedPath: "/api/info");
        using var hostingHandler = new NeverUsedHandler();
        var client = CreateClient(deviceHandler, hostingHandler, controlTimeout: ShortTimeout, uploadTimeout: ShortTimeout);
        using var form = CreateConnectForm(client);

        InvokeAndPumpConnect(form);
        Application.DoEvents();

        CollectionAssert.AreEqual(new[] { "/api/connected", "/api/info" }, deviceHandler.Paths.ToArray());
        Assert.IsFalse(hostingHandler.RequestStarted);
        Assert.IsFalse(GetField<bool>(form, "_hsspReady"));
        Assert.IsTrue(GetField<TextBox>(form, "_log").Text.Contains("info: request timed out.", StringComparison.Ordinal));
    }

    [STATestMethod]
    public void UploadTimeout_DoesNotSendHsspSetupOrPlay_AndDoesNotLeaveHsspReady()
    {
        using var deviceHandler = new ConnectSequenceHandler();
        using var hostingHandler = new BlockingHandler();
        var client = CreateClient(deviceHandler, hostingHandler, controlTimeout: ShortTimeout, uploadTimeout: ShortTimeout);
        using var form = CreateConnectForm(client);

        InvokeAndPumpConnect(form);
        Application.DoEvents();

        Assert.IsTrue(hostingHandler.Started.Task.IsCompleted);
        Assert.IsFalse(deviceHandler.Paths.Any(path => path.EndsWith("/hssp/setup", StringComparison.Ordinal)));
        Assert.IsFalse(deviceHandler.Paths.Any(path => path.EndsWith("/hssp/play", StringComparison.Ordinal)));
        Assert.IsFalse(GetField<bool>(form, "_hsspReady"));
        Assert.IsTrue(GetField<TextBox>(form, "_log").Text.Contains("script upload: request timed out.", StringComparison.Ordinal));
    }

    [STATestMethod]
    public void PlayTimeout_ClearsReadyStateAndDoesNotReportPlaybackSuccess()
    {
        using var handler = new BlockingHandler();
        using var hostingHandler = new NeverUsedHandler();
        using var client = CreateClient(handler, hostingHandler, controlTimeout: ShortTimeout);
        var session = new MpvSession();
        typeof(MpvSession).GetProperty(nameof(MpvSession.IsPaused))!.SetValue(session, false);
        typeof(MpvSession).GetProperty(nameof(MpvSession.Speed))!.SetValue(session, 1.0);
        using var form = CreateConnectForm(client);
        SetField(form, "_handy", client);
        SetField(form, "_mpv", session);
        SetField(form, "_hsspReady", true);
        GetField<System.Windows.Forms.Timer>(form, "_syncTimer").Start();

        var operation = (Task)typeof(ScriptSyncPilot.HandySync.MainForm)
            .GetMethod("OnMpvPauseChangedAsync", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(form, [session, false])!;
        var timer = Stopwatch.StartNew();
        while (!operation.IsCompleted && timer.Elapsed < TimeSpan.FromSeconds(3))
        {
            Application.DoEvents();
            Thread.Sleep(1);
        }
        Assert.IsTrue(operation.IsCompleted, "Play operation did not finish within the test bound.");
        operation.GetAwaiter().GetResult();
        Application.DoEvents();

        Assert.IsTrue(handler.Started.Task.IsCompleted);
        Assert.IsFalse(GetField<bool>(form, "_hsspReady"));
        Assert.IsFalse(GetField<System.Windows.Forms.Timer>(form, "_syncTimer").Enabled);
        Assert.IsTrue(GetField<Label>(form, "_status").Text.Contains("device state is unknown", StringComparison.Ordinal));
        Assert.IsTrue(GetField<TextBox>(form, "_log").Text.Contains("hssp/play: request timed out.", StringComparison.Ordinal));
        session.Dispose();
    }

    private static ScriptSyncPilot.HandySync.MainForm CreateConnectForm(HandyApiClient client)
    {
        var form = new ScriptSyncPilot.HandySync.MainForm(
            new AppSettings { ConnectionKey = "synthetic-connection-key" }, _ => client, _ => { }, _ => { });
        _ = form.Handle;
        typeof(ScriptSyncPilot.HandySync.MainForm).GetField("_script", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(form, new Funscript());
        return form;
    }

    private static void InvokeAndPumpConnect(ScriptSyncPilot.HandySync.MainForm form)
    {
        var connect = (Task)typeof(ScriptSyncPilot.HandySync.MainForm).GetMethod("ConnectHandyAsync", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(form, null)!;
        var timer = Stopwatch.StartNew();
        while (!connect.IsCompleted && timer.Elapsed < TimeSpan.FromSeconds(5))
        {
            Application.DoEvents();
            Thread.Sleep(1);
        }

        Assert.IsTrue(connect.IsCompleted, "ConnectHandyAsync did not finish within the test bound.");
        connect.GetAwaiter().GetResult();
    }

    private static T GetField<T>(object instance, string name) =>
        (T)instance.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(instance)!;

    private static void SetField(object instance, string name, object value) =>
        instance.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(instance, value);

    private static HandyApiClient CreateClient(HttpMessageHandler deviceHandler,
        HttpMessageHandler? hostingHandler = null, TimeSpan? controlTimeout = null, TimeSpan? uploadTimeout = null,
        TimeSpan? serverTimeTimeout = null) =>
        new("test-key", deviceHandler, new Uri("https://example.test/api/"), hostingHandler,
            controlTimeout, serverTimeTimeout, uploadTimeout);

    private sealed class BlockingHandler : HttpMessageHandler
    {
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource CancellationObserved { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Started.TrySetResult();
            try
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                CancellationObserved.TrySetResult();
                throw new TaskCanceledException(SyntheticSecret);
            }

            throw new InvalidOperationException("The blocking handler should only finish through cancellation.");
        }
    }

    private sealed class ContentHandler(HttpContent content) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = content });
    }

    private sealed class ConnectSequenceHandler(string? blockedPath = null) : HttpMessageHandler
    {
        public ConcurrentQueue<string> Paths { get; } = new();

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var path = request.RequestUri!.AbsolutePath;
            Paths.Enqueue(path);
            if (path == blockedPath)
            {
                try
                {
                    await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    throw new TaskCanceledException(SyntheticSecret);
                }
            }

            var body = path switch
            {
                "/api/connected" => "{\"result\":{\"connected\":true}}",
                "/api/info" => "{\"result\":{\"fw_status\":0,\"fw_version\":\"4.3.0\",\"hw_model_name\":\"test\"}}",
                "/api/servertime" => "{\"server_time\":1000}",
                "/api/capabilities" => "{\"result\":{\"slider\":0}}",
                "/api/settings/slider" => "{\"result\":{}}",
                "/api/hstp/offset" => "{\"result\":{\"offset\":0}}",
                _ => ""
            };
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body) };
        }
    }

    private sealed class ServerTimeHandler(int stallAtRequest) : HttpMessageHandler
    {
        private int _requestCount;

        public int RequestCount => Volatile.Read(ref _requestCount);

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var requestNumber = Interlocked.Increment(ref _requestCount);
            if (requestNumber >= stallAtRequest)
            {
                try
                {
                    await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    throw new TaskCanceledException(SyntheticSecret);
                }
            }

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent($"{{\"server_time\":{1000 + requestNumber}}}")
            };
        }
    }

    private sealed class NeverUsedHandler : HttpMessageHandler
    {
        public bool RequestStarted { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestStarted = true;
            throw new AssertFailedException("Unexpected request.");
        }
    }

    private sealed class BlockingContent : HttpContent
    {
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource CancellationObserved { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context) => Task.CompletedTask;

        protected override async Task SerializeToStreamAsync(Stream stream, TransportContext? context,
            CancellationToken cancellationToken)
        {
            Started.TrySetResult();
            try
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                CancellationObserved.TrySetResult();
                throw new TaskCanceledException(SyntheticSecret);
            }
        }

        protected override bool TryComputeLength(out long length)
        {
            length = 0;
            return false;
        }
    }

}
