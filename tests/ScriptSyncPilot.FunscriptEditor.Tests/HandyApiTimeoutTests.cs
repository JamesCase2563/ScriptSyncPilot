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

    [DataTestMethod]
    [DataRow("connected", false)]
    [DataRow("servertime", false)]
    [DataRow("script upload", false)]
    [DataRow("hssp/play", false)]
    [DataRow("hssp/play", true)]
    public async Task NonCooperativeTransport_HasHardBudget_AndDefersDisposal(string operation, bool failLate)
    {
        using var handler = new NonCooperativeHandler(failLate);
        using var unusedHandler = new NeverUsedHandler();
        using var client = operation == "script upload"
            ? CreateClient(unusedHandler, handler, uploadTimeout: ShortTimeout)
            : CreateClient(handler, controlTimeout: ShortTimeout, serverTimeTimeout: ShortTimeout);
        var stopwatch = Stopwatch.StartNew();
        var request = operation switch
        {
            "connected" => client.IsConnectedAsync(CancellationToken.None),
            "servertime" => client.GetServerTimeMsAsync(CancellationToken.None),
            "script upload" => client.UploadScriptCsvAsync("0,0", CancellationToken.None),
            _ => client.HsspPlayAsync(0, 0)
        };
        try
        {
            await handler.Started.Task.WaitAsync(TimeSpan.FromSeconds(2));
            var error = await Assert.ThrowsExceptionAsync<HandyApiTimeoutException>(
                () => request.WaitAsync(TimeSpan.FromSeconds(2)));
            Assert.AreEqual($"{operation}: request timed out.", error.Message);
            Assert.IsTrue(stopwatch.Elapsed < TimeSpan.FromSeconds(2));
            Assert.IsFalse(client.PendingRequestsCompleted.IsCompleted);
            client.Dispose();
            Assert.IsFalse(handler.Disposed.Task.IsCompleted, "Dispose must not interrupt late transport work.");
        }
        finally
        {
            handler.Release.TrySetResult();
            await client.PendingRequestsCompleted.WaitAsync(TimeSpan.FromSeconds(2));
        }
        await handler.Disposed.Task.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.IsFalse(request.IsCompletedSuccessfully, "Late success or failure must not replace the timeout.");
    }

    [DataTestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task NonCooperativeTransport_CancellationHasHardWait_AndIsNotTimeout(bool cancelLifetime)
    {
        using var handler = new NonCooperativeHandler();
        using var client = CreateClient(handler, controlTimeout: TimeSpan.FromSeconds(5));
        using var cancellation = new CancellationTokenSource();
        var request = client.IsConnectedAsync(cancellation.Token);
        try
        {
            await handler.Started.Task.WaitAsync(TimeSpan.FromSeconds(2));
            if (cancelLifetime) client.CancelPendingRequests();
            else cancellation.Cancel();
            var error = await Assert.ThrowsExceptionAsync<HandyApiRequestCanceledException>(
                () => request.WaitAsync(TimeSpan.FromSeconds(2)));
            Assert.AreEqual("connected: request canceled.", error.Message);
            Assert.AreEqual(cancellation.Token, error.CancellationToken);
            Assert.IsInstanceOfType<HandyApiRequestCanceledException>(error);
            Assert.AreEqual(cancelLifetime,
                ((HandyApiRequestCanceledException)error).IsLifecycleCancellation);
            Assert.IsFalse(client.PendingRequestsCompleted.IsCompleted);
        }
        finally
        {
            handler.Release.TrySetResult();
            await client.PendingRequestsCompleted.WaitAsync(TimeSpan.FromSeconds(2));
        }
    }

    [TestMethod]
    public async Task NonCooperativeResponseRead_HasHardBudget_AndRetainsContentUntilCompletion()
    {
        using var content = new NonCooperativeContent();
        using var handler = new ContentHandler(content);
        using var client = CreateClient(handler, controlTimeout: ShortTimeout);
        var request = client.GetInfoAsync(CancellationToken.None);
        try
        {
            await content.Started.Task.WaitAsync(TimeSpan.FromSeconds(2));
            var error = await Assert.ThrowsExceptionAsync<HandyApiTimeoutException>(
                () => request.WaitAsync(TimeSpan.FromSeconds(2)));
            Assert.AreEqual("info: request timed out.", error.Message);
            client.Dispose();
            Assert.IsFalse(content.Disposed, "The response must remain alive while its body is being read.");
            Assert.IsFalse(client.PendingRequestsCompleted.IsCompleted);
        }
        finally
        {
            content.Release.TrySetResult();
            await client.PendingRequestsCompleted.WaitAsync(TimeSpan.FromSeconds(2));
        }
        Assert.IsTrue(content.Disposed);
        Assert.IsFalse(request.IsCompletedSuccessfully);
    }

    [TestMethod]
    public async Task RestRequestTimeout_CancelsTransport_AndReturnsSafeTimeoutMessage()
    {
        using var handler = new BlockingHandler();
        using var client = CreateClient(handler, controlTimeout: ShortTimeout);

        var request = client.IsConnectedAsync(CancellationToken.None);
        await handler.Started.Task.WaitAsync(TimeSpan.FromSeconds(2));
        var error = await Assert.ThrowsExceptionAsync<HandyApiTimeoutException>(() => request);

        await handler.CancellationObserved.Task.WaitAsync(TimeSpan.FromSeconds(2));
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

        await handler.CancellationObserved.Task.WaitAsync(TimeSpan.FromSeconds(2));
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

        await hostingHandler.CancellationObserved.Task.WaitAsync(TimeSpan.FromSeconds(2));
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

        await content.CancellationObserved.Task.WaitAsync(TimeSpan.FromSeconds(2));
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
        var error = await Assert.ThrowsExceptionAsync<HandyApiRequestCanceledException>(() => request);

        await handler.CancellationObserved.Task.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.AreEqual("connected: request canceled.", error.Message);
        Assert.IsFalse(error.Message.Contains("timed out", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(error.Message.Contains(SyntheticSecret, StringComparison.Ordinal));
    }

    [STATestMethod]
    public void NonCooperativeConnectTimeout_ReturnsWithClientAndGateRetainedUntilTransportCompletes()
    {
        using var handler = new NonCooperativeHandler();
        using var hostingHandler = new NeverUsedHandler();
        using var client = CreateClient(handler, hostingHandler, controlTimeout: ShortTimeout);
        using var form = CreateConnectForm(client, ShortTimeout);
        var gate = GetField<SemaphoreSlim>(form, "_deviceCommandGate");
        try
        {
            var stopwatch = Stopwatch.StartNew();
            InvokeAndPumpConnect(form);
            Assert.IsTrue(stopwatch.Elapsed < TimeSpan.FromSeconds(2));
            Assert.IsFalse(hostingHandler.RequestStarted);
            Assert.IsFalse(GetField<bool>(form, "_hsspReady"));
            Assert.IsTrue(GetField<bool>(form, "_handyDisconnecting"));
            Assert.IsFalse(GetField<Button>(form, "_connectButton").Enabled);
            Assert.AreEqual(0, gate.CurrentCount);
            Assert.IsFalse(handler.Disposed.Task.IsCompleted);
            Assert.IsTrue(GetField<TextBox>(form, "_log").Text.Contains("connected: request timed out.", StringComparison.Ordinal));
            Assert.AreEqual("Handy error; see log", GetField<Label>(form, "_status").Text);
        }
        finally
        {
            handler.Release.TrySetResult();
            var stopwatch = Stopwatch.StartNew();
            while ((!handler.Disposed.Task.IsCompleted || GetField<bool>(form, "_handyDisconnecting"))
                   && stopwatch.Elapsed < TimeSpan.FromSeconds(2))
            {
                Application.DoEvents();
                Thread.Sleep(1);
            }
        }
        Assert.IsTrue(handler.Disposed.Task.IsCompleted);
        Assert.IsFalse(GetField<bool>(form, "_handyDisconnecting"));
        Assert.AreEqual(1, gate.CurrentCount);
        Assert.IsTrue(GetField<Button>(form, "_connectButton").Enabled);
        Assert.AreEqual("Handy error; see log", GetField<Label>(form, "_status").Text,
            "Completing deferred teardown must keep the connect-failure status instead of reporting Idle.");
        Assert.IsNull(GetField<string?>(form, "_disconnectCompletionStatus"));
    }

    [STATestMethod]
    public void NonCooperativePlayTimeout_DrainsAndReleasesGateAfterUiDisposalWithoutPumping()
    {
        using var handler = new NonCooperativeHandler(failLate: true);
        using var client = CreateClient(handler, controlTimeout: ShortTimeout);
        using var session = new MpvSession();
        typeof(MpvSession).GetProperty(nameof(MpvSession.IsPaused))!.SetValue(session, false);
        typeof(MpvSession).GetProperty(nameof(MpvSession.Speed))!.SetValue(session, 1.0);
        using var form = CreateConnectForm(client);
        SetField(form, "_handy", client);
        SetField(form, "_mpv", session);
        SetField(form, "_hsspReady", true);
        var gate = GetField<SemaphoreSlim>(form, "_deviceCommandGate");
        try
        {
            var operation = (Task)typeof(ScriptSyncPilot.HandySync.MainForm)
                .GetMethod("OnMpvPauseChangedAsync", BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(form, [session, false])!;
            var stopwatch = Stopwatch.StartNew();
            while (!operation.IsCompleted && stopwatch.Elapsed < TimeSpan.FromSeconds(2))
            {
                Application.DoEvents();
                Thread.Sleep(1);
            }
            Assert.IsTrue(operation.IsCompleted);
            operation.GetAwaiter().GetResult();
            Assert.IsFalse(GetField<bool>(form, "_hsspReady"));
            Assert.AreEqual(0, gate.CurrentCount);
            form.Dispose();
            client.Dispose();
            Assert.IsFalse(handler.Disposed.Task.IsCompleted);
        }
        finally
        {
            var drain = client.PendingRequestsCompleted;
            handler.Release.TrySetResult();
            Assert.IsTrue(drain.Wait(TimeSpan.FromSeconds(2)), "Late transport cleanup must not require the disposed UI context.");
        }
        Assert.IsTrue(SpinWait.SpinUntil(() => gate.CurrentCount == 1 && handler.Disposed.Task.IsCompleted,
            TimeSpan.FromSeconds(2)));
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

    [STATestMethod]
    public void SyncTimeout_ClearsReadyStateAndStopsPeriodicSync()
    {
        using var handler = new BlockingHandler();
        using var hostingHandler = new NeverUsedHandler();
        using var client = CreateClient(handler, hostingHandler, controlTimeout: ShortTimeout);
        using var session = new MpvSession();
        typeof(MpvSession).GetProperty(nameof(MpvSession.IsPaused))!.SetValue(session, false);
        typeof(MpvSession).GetProperty(nameof(MpvSession.Speed))!.SetValue(session, 1.0);
        using var form = CreateConnectForm(client);
        SetField(form, "_handy", client);
        SetField(form, "_mpv", session);
        SetField(form, "_hsspReady", true);
        GetField<System.Windows.Forms.Timer>(form, "_syncTimer").Start();

        var operation = (Task)typeof(ScriptSyncPilot.HandySync.MainForm)
            .GetMethod("OnSyncTickAsync", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(form, null)!;
        var timer = Stopwatch.StartNew();
        while (!operation.IsCompleted && timer.Elapsed < TimeSpan.FromSeconds(3))
        {
            Application.DoEvents();
            Thread.Sleep(1);
        }
        Assert.IsTrue(operation.IsCompleted, "Sync operation did not finish within the test bound.");
        operation.GetAwaiter().GetResult();
        Application.DoEvents();

        Assert.IsTrue(handler.Started.Task.IsCompleted);
        Assert.IsFalse(GetField<bool>(form, "_hsspReady"));
        Assert.IsFalse(GetField<System.Windows.Forms.Timer>(form, "_syncTimer").Enabled);
        Assert.IsTrue(GetField<Label>(form, "_status").Text.Contains("device state is unknown", StringComparison.Ordinal));
        Assert.IsTrue(GetField<TextBox>(form, "_log").Text.Contains("hssp/synctime: request timed out.", StringComparison.Ordinal));
    }

    [STATestMethod]
    public void CallerCanceledPlaybackCommand_ReportsCanceledAndUnknownDeviceState()
    {
        using var handler = new BlockingHandler();
        using var client = CreateClient(handler, controlTimeout: TimeSpan.FromSeconds(5));
        using var cancellation = new CancellationTokenSource();
        using var session = CreatePlayingSession();
        using var form = CreateConnectForm(client);
        SetField(form, "_handy", client);
        SetField(form, "_mpv", session);
        SetField(form, "_hsspReady", true);
        _ = form.Handle;
        var syncTimer = GetField<System.Windows.Forms.Timer>(form, "_syncTimer");
        syncTimer.Start();

        var request = client.HsspPlayAsync(100, 200, ct: cancellation.Token);
        PumpUntil(() => handler.Started.Task.IsCompleted, TimeSpan.FromSeconds(2));
        cancellation.Cancel();
        PumpUntil(() => request.IsCompleted, TimeSpan.FromSeconds(2));
        var error = Assert.ThrowsException<HandyApiRequestCanceledException>(() => request.GetAwaiter().GetResult());
        Assert.IsFalse(error.IsLifecycleCancellation);

        typeof(ScriptSyncPilot.HandySync.MainForm).GetMethod("HandlePlaybackCommandFailure", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(form, [session, client, "Play/stop failed", error]);

        Assert.IsFalse(GetField<bool>(form, "_hsspReady"));
        Assert.IsFalse(syncTimer.Enabled);
        Application.DoEvents();
        Assert.AreEqual("Handy command canceled; device state is unknown", GetField<Label>(form, "_status").Text);
        Assert.IsTrue(GetField<TextBox>(form, "_log").Text.Contains("hssp/play: request canceled.", StringComparison.Ordinal));
    }

    [STATestMethod]
    public void LifecycleCanceledPlaybackCommand_IsSilentAndLeavesTeardownStateAlone()
    {
        using var handler = new BlockingHandler();
        using var client = CreateClient(handler, controlTimeout: TimeSpan.FromSeconds(5));
        using var session = CreatePlayingSession();
        using var form = CreateConnectForm(client);
        SetField(form, "_handy", client);
        SetField(form, "_mpv", session);
        SetField(form, "_hsspReady", true);
        _ = form.Handle;
        var syncTimer = GetField<System.Windows.Forms.Timer>(form, "_syncTimer");
        syncTimer.Start();
        var statusBefore = GetField<Label>(form, "_status").Text;
        var logBefore = GetField<TextBox>(form, "_log").Text;

        var request = client.HsspPlayAsync(100, 200);
        PumpUntil(() => handler.Started.Task.IsCompleted, TimeSpan.FromSeconds(2));
        client.CancelPendingRequests();
        PumpUntil(() => request.IsCompleted, TimeSpan.FromSeconds(2));
        var error = Assert.ThrowsException<HandyApiRequestCanceledException>(() => request.GetAwaiter().GetResult());
        Assert.IsTrue(error.IsLifecycleCancellation);

        typeof(ScriptSyncPilot.HandySync.MainForm).GetMethod("HandlePlaybackCommandFailure", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(form, [session, client, "Play/stop failed", error]);

        Assert.IsTrue(GetField<bool>(form, "_hsspReady"));
        Assert.IsTrue(syncTimer.Enabled);
        Assert.AreEqual(statusBefore, GetField<Label>(form, "_status").Text);
        Assert.AreEqual(logBefore, GetField<TextBox>(form, "_log").Text);
        syncTimer.Stop();
    }

    [STATestMethod]
    public void OrdinaryPlaybackFailure_ReportsUnknownDeviceStateAndStopsSync()
    {
        using var client = CreateClient(new NeverUsedHandler());
        using var session = CreatePlayingSession();
        using var form = CreateConnectForm(client);
        SetField(form, "_handy", client);
        SetField(form, "_mpv", session);
        SetField(form, "_hsspReady", true);
        var syncTimer = GetField<System.Windows.Forms.Timer>(form, "_syncTimer");
        syncTimer.Start();
        var error = new HandyApiException("hssp/play: HTTP 503.");

        typeof(ScriptSyncPilot.HandySync.MainForm).GetMethod("HandlePlaybackCommandFailure", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(form, [session, client, "Play/stop failed", error]);

        Assert.IsFalse(GetField<bool>(form, "_hsspReady"));
        Assert.IsFalse(syncTimer.Enabled);
        Application.DoEvents();
        Assert.AreEqual("Handy command failed; device state is unknown", GetField<Label>(form, "_status").Text);
        Assert.IsTrue(GetField<TextBox>(form, "_log").Text.Contains(error.Message, StringComparison.Ordinal));
    }

    [STATestMethod]
    public void StaleSeekTimeout_AfterSessionReplaced_DoesNotAlterCurrentSession() =>
        VerifyStaleTimeoutIgnored("OnMpvPositionChangedAsync", session => [session, 10d], replaceClient: false);

    [STATestMethod]
    public void StalePlayTimeout_AfterSessionReplaced_DoesNotAlterCurrentSession() =>
        VerifyStaleTimeoutIgnored("OnMpvPauseChangedAsync", session => [session, false], replaceClient: false);

    [STATestMethod]
    public void StaleSyncTimeout_AfterSessionReplaced_DoesNotAlterCurrentSession() =>
        VerifyStaleTimeoutIgnored("OnSyncTickAsync", _ => null, replaceClient: false);

    [STATestMethod]
    public void StaleSyncTimeout_AfterClientReplaced_DoesNotAlterCurrentClient() =>
        VerifyStaleTimeoutIgnored("OnSyncTickAsync", _ => null, replaceClient: true);

    private static void VerifyStaleTimeoutIgnored(string method, Func<MpvSession, object?[]?> args, bool replaceClient)
    {
        using var handler = new BlockingHandler();
        using var hostingHandler = new NeverUsedHandler();
        using var client = CreateClient(handler, hostingHandler, controlTimeout: ShortTimeout);
        using var replacementClient = CreateClient(new NeverUsedHandler(), controlTimeout: ShortTimeout);
        using var session = CreatePlayingSession();
        using var replacementSession = CreatePlayingSession();
        using var form = CreateConnectForm(client);
        SetField(form, "_handy", client);
        SetField(form, "_mpv", session);
        SetField(form, "_hsspReady", true);
        var syncTimer = GetField<System.Windows.Forms.Timer>(form, "_syncTimer");
        syncTimer.Start();

        var operation = (Task)typeof(ScriptSyncPilot.HandySync.MainForm)
            .GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(form, args(session))!;
        PumpUntil(() => handler.Started.Task.IsCompleted, TimeSpan.FromSeconds(2));
        if (replaceClient) SetField(form, "_handy", replacementClient);
        else SetField(form, "_mpv", replacementSession);
        var statusBefore = GetField<Label>(form, "_status").Text;
        PumpUntil(() => operation.IsCompleted, TimeSpan.FromSeconds(3));
        operation.GetAwaiter().GetResult();
        Application.DoEvents();

        Assert.IsTrue(GetField<bool>(form, "_hsspReady"), "A stale timeout must not clear readiness for the current target.");
        Assert.IsTrue(syncTimer.Enabled, "A stale timeout must not stop periodic sync for the current target.");
        Assert.AreEqual(statusBefore, GetField<Label>(form, "_status").Text);
        Assert.IsFalse(GetField<TextBox>(form, "_log").Text.Contains("timed out", StringComparison.Ordinal),
            "A stale failure must not be reported against the current target.");
        syncTimer.Stop();
    }

    private static MpvSession CreatePlayingSession()
    {
        var session = new MpvSession();
        typeof(MpvSession).GetProperty(nameof(MpvSession.IsPaused))!.SetValue(session, false);
        typeof(MpvSession).GetProperty(nameof(MpvSession.Speed))!.SetValue(session, 1.0);
        return session;
    }

    private static void PumpUntil(Func<bool> condition, TimeSpan timeout)
    {
        var timer = Stopwatch.StartNew();
        while (!condition() && timer.Elapsed < timeout)
        {
            Application.DoEvents();
            Thread.Sleep(1);
        }
        Assert.IsTrue(condition(), "Condition was not met within the test bound.");
    }

    private static ScriptSyncPilot.HandySync.MainForm CreateConnectForm(HandyApiClient client, TimeSpan? drainTimeout = null)
    {
        var form = new ScriptSyncPilot.HandySync.MainForm(
            new AppSettings { ConnectionKey = "synthetic-connection-key" }, _ => client, _ => { }, _ => { }, drainTimeout);
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

    private sealed class NonCooperativeHandler(bool failLate = false) : HttpMessageHandler
    {
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Disposed { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Started.TrySetResult();
            await Release.Task.ConfigureAwait(false);
            if (failLate) throw new HttpRequestException(SyntheticSecret);
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{}") };
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) Disposed.TrySetResult();
            base.Dispose(disposing);
        }
    }

    private sealed class NonCooperativeContent : HttpContent
    {
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public bool Disposed { get; private set; }

        protected override async Task SerializeToStreamAsync(Stream stream, TransportContext? context)
        {
            Started.TrySetResult();
            await Release.Task.ConfigureAwait(false);
            await stream.WriteAsync(System.Text.Encoding.UTF8.GetBytes("{\"result\":{}}"));
        }

        protected override bool TryComputeLength(out long length)
        {
            length = 0;
            return false;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) Disposed = true;
            base.Dispose(disposing);
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
