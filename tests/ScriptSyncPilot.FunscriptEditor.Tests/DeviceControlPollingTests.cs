using System.Diagnostics;
using System.Net;
using System.Reflection;
using System.Text;
using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ScriptSyncPilot.HandySync;

namespace ScriptSyncPilot.FunscriptEditor.Tests;

[TestClass]
public sealed class DeviceControlPollingTests
{
    private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;

    [STATestMethod]
    public void StrokeChangeDuringInflightWriteProducesOneLatestFollowup()
    {
        using var form = new ScriptSyncPilot.HandySync.MainForm();
        using var handler = new BlockingStrokeHandler();
        using var client = new HandyApiClient("test-key", handler, new Uri("https://example.test/api/"));
        InitializeWritableForm(form, client);
        var slider = (DeviceSliderControl)GetPrivateField(form, "_strokeSlider")!;

        slider.SetValues(10, 90);
        InvokePrivate(form, "ScheduleStrokeWrite");
        PumpUntil(() => handler.FirstWriteStarted.Task.IsCompleted, TimeSpan.FromSeconds(3));
        slider.SetValues(25, 75);
        InvokePrivate(form, "ScheduleStrokeWrite");
        handler.ReleaseFirstWrite.TrySetResult();
        PumpUntil(() => handler.Bodies.Count == 2, TimeSpan.FromSeconds(3));

        Assert.AreEqual(2, handler.Bodies.Count);
        using var first = JsonDocument.Parse(handler.Bodies[0]);
        using var latest = JsonDocument.Parse(handler.Bodies[1]);
        Assert.AreEqual(0.1, first.RootElement.GetProperty("min").GetDouble());
        Assert.AreEqual(0.25, latest.RootElement.GetProperty("min").GetDouble());
        Assert.AreEqual(0.75, latest.RootElement.GetProperty("max").GetDouble());
    }

    [STATestMethod]
    public void PendingOffsetAndStrokeWritesAreCancelledWhenDisconnectBegins()
    {
        using var form = new ScriptSyncPilot.HandySync.MainForm();
        using var handler = new RecordingHandler((_, _, _) => Task.FromResult(JsonResponse("{\"result\":\"ok\"}")));
        using var client = new HandyApiClient("test-key", handler, new Uri("https://example.test/api/"));
        InitializeWritableForm(form, client);
        var slider = (DeviceSliderControl)GetPrivateField(form, "_strokeSlider")!;
        var offset = (DeviceSliderControl)GetPrivateField(form, "_offsetSlider")!;
        _ = form.Handle;

        slider.SetValues(15, 85);
        offset.SetValues(150, 0);
        InvokePrivate(form, "ScheduleOffsetWrite");
        InvokePrivate(form, "ScheduleStrokeWrite");
        var disconnect = InvokePrivateTask(form, "DisconnectHandyAsync");
        PumpUntil(() => disconnect.IsCompleted, TimeSpan.FromSeconds(3));
        disconnect.GetAwaiter().GetResult();
        PumpFor(TimeSpan.FromMilliseconds(300));

        Assert.AreEqual(0, handler.Requests.Count(request => request.Method == "PUT"));
    }

    [STATestMethod]
    public void DisconnectRejectsNewWritesWhileStopIsInProgress()
    {
        using var form = new ScriptSyncPilot.HandySync.MainForm();
        var stopStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseStop = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var handler = new RecordingHandler(async (request, _, cancellationToken) =>
        {
            if (request.RequestUri!.AbsolutePath == "/api/hssp/stop")
            {
                stopStarted.TrySetResult();
                await releaseStop.Task.WaitAsync(cancellationToken);
                return JsonResponse("{\"result\":\"ok\"}");
            }

            throw new InvalidOperationException($"Unexpected request during disconnect: {request.RequestUri}");
        });
        using var client = new HandyApiClient("test-key", handler, new Uri("https://example.test/api/"));
        InitializeWritableForm(form, client);
        SetPrivateField(form, "_installedScriptClient", client);
        _ = form.Handle;

        var disconnect = InvokePrivateTask(form, "DisconnectHandyAsync");
        PumpUntil(() => stopStarted.Task.IsCompleted, TimeSpan.FromSeconds(3));
        InvokePrivate(form, "ScheduleOffsetWrite");
        InvokePrivate(form, "ScheduleStrokeWrite");
        releaseStop.TrySetResult();
        PumpUntil(() => disconnect.IsCompleted, TimeSpan.FromSeconds(3));
        disconnect.GetAwaiter().GetResult();
        PumpFor(TimeSpan.FromMilliseconds(300));

        Assert.AreEqual(1, handler.Requests.Count(request => request.Path == "hssp/stop"));
        Assert.AreEqual(0, handler.Requests.Count(request => request.Method == "PUT" && request.Path != "hssp/stop"));
    }

    [STATestMethod]
    public void DisconnectCancelsInflightPlayAndSkipsQueuedSeekBeforeStop()
    {
        using var form = new ScriptSyncPilot.HandySync.MainForm();
        var playStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var playCanceled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var handler = new RecordingHandler(async (request, _, cancellationToken) =>
        {
            if (request.RequestUri!.AbsolutePath == "/api/hssp/play")
            {
                playStarted.TrySetResult();
                try
                {
                    await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    playCanceled.TrySetResult();
                    throw;
                }
            }

            if (request.RequestUri!.AbsolutePath == "/api/hssp/stop")
                return JsonResponse("{\"result\":\"ok\"}");

            throw new InvalidOperationException($"Unexpected request during disconnect: {request.RequestUri}");
        });
        using var client = new HandyApiClient("test-key", handler, new Uri("https://example.test/api/"));
        using var session = new ScriptSyncPilot.Common.MpvSession();
        typeof(ScriptSyncPilot.Common.MpvSession).GetProperty(nameof(ScriptSyncPilot.Common.MpvSession.IsPaused))!
            .SetValue(session, false);
        typeof(ScriptSyncPilot.Common.MpvSession).GetProperty(nameof(ScriptSyncPilot.Common.MpvSession.Speed))!
            .SetValue(session, 1.0);
        InitializeWritableForm(form, client);
        SetPrivateField(form, "_hsspReady", true);
        SetPrivateField(form, "_installedScriptClient", client);
        SetPrivateField(form, "_mpv", session);
        _ = form.Handle;

        var play = InvokePrivateTask(form, "OnMpvPauseChangedAsync", session, false);
        PumpUntil(() => playStarted.Task.IsCompleted, TimeSpan.FromSeconds(3));
        var queuedSeek = InvokePrivateTask(form, "OnMpvPositionChangedAsync", session, 10d);
        Assert.IsFalse(queuedSeek.IsCompleted);

        var drainTimer = Stopwatch.StartNew();
        var disconnect = InvokePrivateTask(form, "DisconnectHandyAsync");
        PumpUntil(() => disconnect.IsCompleted && play.IsCompleted && queuedSeek.IsCompleted, TimeSpan.FromSeconds(3));
        disconnect.GetAwaiter().GetResult();
        play.GetAwaiter().GetResult();
        queuedSeek.GetAwaiter().GetResult();

        Assert.IsTrue(drainTimer.Elapsed < TimeSpan.FromSeconds(3));
        Assert.IsTrue(playCanceled.Task.IsCompleted);
        CollectionAssert.AreEqual(new[] { "hssp/play", "hssp/stop" }, handler.Requests.Select(request => request.Path).ToArray());
    }

    [STATestMethod]
    public void StalePlaybackFailureDoesNotSuspendNewerQueuedCommand()
    {
        using var form = new ScriptSyncPilot.HandySync.MainForm();
        var playStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releasePlay = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var handler = new RecordingHandler(async (request, count, _) =>
        {
            if (count == 1)
            {
                playStarted.TrySetResult();
                await releasePlay.Task;
                throw new HttpRequestException("simulated stale play failure");
            }
            return JsonResponse("{\"result\":\"ok\"}");
        });
        using var client = new HandyApiClient("test-key", handler, new Uri("https://example.test/api/"));
        using var session = new ScriptSyncPilot.Common.MpvSession();
        typeof(ScriptSyncPilot.Common.MpvSession).GetProperty(nameof(ScriptSyncPilot.Common.MpvSession.IsPaused))!
            .SetValue(session, false);
        typeof(ScriptSyncPilot.Common.MpvSession).GetProperty(nameof(ScriptSyncPilot.Common.MpvSession.Speed))!
            .SetValue(session, 1.0);
        InitializeWritableForm(form, client);
        SetPrivateField(form, "_mpv", session);
        SetPrivateField(form, "_hsspReady", true);
        _ = form.Handle;
        var syncTimer = (System.Windows.Forms.Timer)GetPrivateField(form, "_syncTimer")!;
        syncTimer.Start();

        var failedPlay = InvokePrivateTask(form, "OnMpvPauseChangedAsync", session, false);
        PumpUntil(() => playStarted.Task.IsCompleted, TimeSpan.FromSeconds(2));
        var newerSync = InvokePrivateTask(form, "OnSyncTickAsync");
        Assert.IsFalse(newerSync.IsCompleted, "The newer command must be queued behind the in-flight play.");

        releasePlay.TrySetResult();
        PumpUntil(() => failedPlay.IsCompleted && newerSync.IsCompleted, TimeSpan.FromSeconds(3));
        failedPlay.GetAwaiter().GetResult();
        newerSync.GetAwaiter().GetResult();
        Application.DoEvents();

        Assert.AreEqual(2, handler.Requests.Count);
        Assert.AreEqual("hssp/synctime", handler.Requests[1].Path);
        Assert.IsTrue((bool)GetPrivateField(form, "_hsspReady")!, "An older failure must not suspend readiness after a newer command runs.");
        Assert.IsTrue(syncTimer.Enabled, "An older failure must not stop synchronization after a newer command runs.");
        Assert.IsFalse(((TextBox)GetPrivateField(form, "_log")!).Text.Contains("simulated stale play failure", StringComparison.Ordinal));
        syncTimer.Stop();
    }

    [STATestMethod]
    [DataRow("seek")]
    [DataRow("play")]
    [DataRow("sync")]
    public void QueuedPlaybackCallbackIsSuppressedAfterMpvSessionReplacement(string producer)
    {
        using var form = new ScriptSyncPilot.HandySync.MainForm();
        using var handler = new RecordingHandler((_, _, _) => Task.FromResult(JsonResponse("{\"result\":\"ok\"}")));
        using var client = new HandyApiClient("test-key", handler, new Uri("https://example.test/api/"));
        using var staleSession = new ScriptSyncPilot.Common.MpvSession();
        using var replacementSession = new ScriptSyncPilot.Common.MpvSession();
        foreach (var session in new[] { staleSession, replacementSession })
        {
            typeof(ScriptSyncPilot.Common.MpvSession).GetProperty(nameof(ScriptSyncPilot.Common.MpvSession.IsPaused))!
                .SetValue(session, false);
            typeof(ScriptSyncPilot.Common.MpvSession).GetProperty(nameof(ScriptSyncPilot.Common.MpvSession.Speed))!
                .SetValue(session, 1.0);
        }
        InitializeWritableForm(form, client);
        SetPrivateField(form, "_mpv", staleSession);
        SetPrivateField(form, "_hsspReady", true);
        _ = form.Handle;
        var originalStatus = ((Label)GetPrivateField(form, "_status")!).Text;

        var gate = (SemaphoreSlim)GetPrivateField(form, "_deviceCommandGate")!;
        Assert.IsTrue(gate.Wait(0));
        Task queued = producer switch
        {
            "seek" => InvokePrivateTask(form, "OnMpvPositionChangedAsync", staleSession, 10d),
            "play" => InvokePrivateTask(form, "OnMpvPauseChangedAsync", staleSession, false),
            _ => InvokePrivateTask(form, "OnSyncTickAsync")
        };
        Assert.IsFalse(queued.IsCompleted, "The callback must be waiting behind the device-command gate.");
        SetPrivateField(form, "_mpv", replacementSession);
        gate.Release();
        PumpUntil(() => queued.IsCompleted, TimeSpan.FromSeconds(3));
        queued.GetAwaiter().GetResult();

        Assert.AreSame(replacementSession, GetPrivateField(form, "_mpv"));
        Assert.IsTrue((bool)GetPrivateField(form, "_hsspReady")!);
        Assert.AreEqual(originalStatus, ((Label)GetPrivateField(form, "_status")!).Text,
            "A queued callback from the replaced session must not change the current status.");
        Assert.AreEqual(0, handler.Requests.Count, "A queued callback from the replaced session must send no HSSP command.");
    }

    [STATestMethod]
    public void FirstSyncAfterMpvSessionReplacementUsesReplacementPosition()
    {
        using var form = new ScriptSyncPilot.HandySync.MainForm();
        using var handler = new RecordingHandler((_, _, _) => Task.FromResult(JsonResponse("{\"result\":\"ok\"}")));
        using var client = new HandyApiClient("test-key", handler, new Uri("https://example.test/api/"));
        using var replacementSession = new ScriptSyncPilot.Common.MpvSession();
        typeof(ScriptSyncPilot.Common.MpvSession).GetProperty(nameof(ScriptSyncPilot.Common.MpvSession.IsPaused))!
            .SetValue(replacementSession, false);
        typeof(ScriptSyncPilot.Common.MpvSession).GetProperty(nameof(ScriptSyncPilot.Common.MpvSession.Speed))!
            .SetValue(replacementSession, 1.0);
        typeof(ScriptSyncPilot.Common.MpvSession).GetProperty(nameof(ScriptSyncPilot.Common.MpvSession.PositionSeconds))!
            .SetValue(replacementSession, 8.5);
        InitializeWritableForm(form, client);
        SetPrivateField(form, "_mpv", replacementSession);
        SetPrivateField(form, "_hsspReady", true);
        SetPrivateField(form, "_lastKnownPos", 42d);

        InvokePrivateTask(form, "OnSyncTickAsync").GetAwaiter().GetResult();

        Assert.AreEqual(1, handler.Requests.Count);
        Assert.AreEqual("hssp/synctime", handler.Requests[0].Path);
        using var body = JsonDocument.Parse(handler.Requests[0].Body!);
        Assert.AreEqual(8500L, body.RootElement.GetProperty("current_time").GetInt64(),
            "A new mpv session's first sync must not reuse the previous session's cached position.");
    }

    [STATestMethod]
    [DataRow("play")]
    [DataRow("seek")]
    [DataRow("rate-stop-session")]
    [DataRow("rate-stop-client")]
    public void LateSuccessfulPlaybackCommandDoesNotReportForReplacementSession(string producer)
    {
        using var form = new ScriptSyncPilot.HandySync.MainForm();
        var requestStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseRequest = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var handler = new RecordingHandler(async (_, _, _) =>
        {
            requestStarted.TrySetResult();
            await releaseRequest.Task;
            return JsonResponse("{\"result\":\"ok\"}");
        });
        using var client = new HandyApiClient("test-key", handler, new Uri("https://example.test/api/"));
        using var replacementClient = new HandyApiClient("test-key");
        using var staleSession = new ScriptSyncPilot.Common.MpvSession();
        using var replacementSession = new ScriptSyncPilot.Common.MpvSession();
        foreach (var session in new[] { staleSession, replacementSession })
        {
            typeof(ScriptSyncPilot.Common.MpvSession).GetProperty(nameof(ScriptSyncPilot.Common.MpvSession.IsPaused))!
                .SetValue(session, false);
            typeof(ScriptSyncPilot.Common.MpvSession).GetProperty(nameof(ScriptSyncPilot.Common.MpvSession.Speed))!
                .SetValue(session, 1.0);
        }
        InitializeWritableForm(form, client);
        SetPrivateField(form, "_mpv", staleSession);
        SetPrivateField(form, "_hsspReady", true);
        _ = form.Handle;
        var originalStatus = ((Label)GetPrivateField(form, "_status")!).Text;

        if (producer.StartsWith("rate-stop", StringComparison.Ordinal))
            typeof(ScriptSyncPilot.Common.MpvSession).GetProperty(nameof(ScriptSyncPilot.Common.MpvSession.Speed))!
                .SetValue(staleSession, 0.5);
        var operation = producer == "seek"
            ? InvokePrivateTask(form, "OnMpvPositionChangedAsync", staleSession, 10d)
            : InvokePrivateTask(form, "OnMpvPauseChangedAsync", staleSession, false);
        PumpUntil(() => requestStarted.Task.IsCompleted, TimeSpan.FromSeconds(2));
        if (producer == "rate-stop-client") SetPrivateField(form, "_handy", replacementClient);
        else SetPrivateField(form, "_mpv", replacementSession);
        releaseRequest.TrySetResult();
        PumpUntil(() => operation.IsCompleted, TimeSpan.FromSeconds(3));
        operation.GetAwaiter().GetResult();
        Application.DoEvents();

        Assert.AreSame(producer == "rate-stop-client" ? staleSession : replacementSession, GetPrivateField(form, "_mpv"));
        Assert.AreSame(producer == "rate-stop-client" ? replacementClient : client, GetPrivateField(form, "_handy"));
        Assert.IsTrue((bool)GetPrivateField(form, "_hsspReady")!);
        Assert.AreEqual(originalStatus, ((Label)GetPrivateField(form, "_status")!).Text,
            "A successful old-session command must not set status for the replacement session.");
        var log = ((TextBox)GetPrivateField(form, "_log")!).Text;
        Assert.IsFalse(log.Contains("Playback resumed; device started.", StringComparison.Ordinal));
        Assert.IsFalse(log.Contains("Seek detected, resynced", StringComparison.Ordinal));
        Assert.IsFalse(log.Contains("Device synchronization suspended:", StringComparison.Ordinal));
        if (producer.StartsWith("rate-stop", StringComparison.Ordinal))
            Assert.AreEqual("hssp/stop", handler.Requests.Single().Path);
    }

    [STATestMethod]
    public void DisconnectDrainHasHardDeadlineAndRetainsClientUntilLateWorkCompletes()
    {
        var playStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releasePlay = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var handler = new RecordingHandler(async (request, _, _) =>
        {
            if (request.RequestUri!.AbsolutePath == "/api/hssp/play")
            {
                playStarted.TrySetResult();
                await releasePlay.Task; // Deliberately ignore cancellation to model a non-cooperative transport.
                return JsonResponse("{\"result\":\"ok\"}");
            }

            if (request.RequestUri!.AbsolutePath == "/api/hssp/stop")
                return JsonResponse("{\"result\":\"ok\"}");

            throw new InvalidOperationException($"Unexpected request during disconnect: {request.RequestUri}");
        });
        using var client = new HandyApiClient("test-key", handler, new Uri("https://example.test/api/"));
        using var session = new ScriptSyncPilot.Common.MpvSession();
        typeof(ScriptSyncPilot.Common.MpvSession).GetProperty(nameof(ScriptSyncPilot.Common.MpvSession.IsPaused))!
            .SetValue(session, false);
        typeof(ScriptSyncPilot.Common.MpvSession).GetProperty(nameof(ScriptSyncPilot.Common.MpvSession.Speed))!
            .SetValue(session, 1.0);
        using var form = new ScriptSyncPilot.HandySync.MainForm(new ScriptSyncPilot.HandySync.AppSettings(),
            _ => client, _ => { }, _ => { }, TimeSpan.FromMilliseconds(100));
        InitializeWritableForm(form, client);
        SetPrivateField(form, "_hsspReady", true);
        SetPrivateField(form, "_installedScriptClient", client);
        SetPrivateField(form, "_mpv", session);
        _ = form.Handle;

        var play = InvokePrivateTask(form, "OnMpvPauseChangedAsync", session, false);
        PumpUntil(() => playStarted.Task.IsCompleted, TimeSpan.FromSeconds(2));
        var disconnectTimer = Stopwatch.StartNew();
        var disconnect = InvokePrivateTask(form, "DisconnectHandyAsync");
        PumpUntil(() => disconnect.IsCompleted, TimeSpan.FromSeconds(2));
        disconnect.GetAwaiter().GetResult();

        Assert.IsTrue(disconnectTimer.Elapsed < TimeSpan.FromSeconds(1));
        Assert.IsTrue(play.IsCompleted, "Cancellation must end the command wait even while the transport remains pending.");
        Assert.AreEqual(0, ((SemaphoreSlim)GetPrivateField(form, "_deviceCommandGate")!).CurrentCount,
            "The command gate must remain owned until the underlying transport completes.");
        Assert.IsFalse(handler.IsDisposed, "The client must remain alive while the active command can still use it.");
        Assert.IsFalse(handler.Requests.Any(request => request.Path == "hssp/stop"),
            "Stop must wait until the serialized command releases the gate.");
        Assert.IsTrue((bool)GetPrivateField(form, "_handyDisconnecting")!,
            "Reconnect remains blocked while deferred teardown owns the captured client.");
        Assert.IsTrue(((Label)GetPrivateField(form, "_status")!).Text.Contains("Disconnect pending", StringComparison.Ordinal));

        releasePlay.TrySetResult();
        PumpUntil(() => play.IsCompleted && handler.IsDisposed, TimeSpan.FromSeconds(3));
        play.GetAwaiter().GetResult();
        PumpUntil(() => !(bool)GetPrivateField(form, "_handyDisconnecting")!, TimeSpan.FromSeconds(2));

        CollectionAssert.AreEqual(new[] { "hssp/play", "hssp/stop" }, handler.Requests.Select(request => request.Path).ToArray());
        Assert.IsTrue(handler.IsDisposed, "The captured client is disposed after the late operation is observed and stop is attempted.");
        Assert.IsFalse(((TextBox)GetPrivateField(form, "_log")!).Text.Contains("Playback resumed; device started.", StringComparison.Ordinal),
            "The late transport completion must not be reported as successful play after disconnect.");
    }

    [STATestMethod]
    public void DisconnectContinuesCleanupAfterUnexpectedStopFailure()
    {
        using var form = new ScriptSyncPilot.HandySync.MainForm();
        using var handler = new RecordingHandler((_, _, _) =>
            throw new InvalidOperationException("synthetic-secret-from-transport"));
        using var client = new HandyApiClient("test-key", handler, new Uri("https://example.test/api/"));
        InitializeWritableForm(form, client);
        SetPrivateField(form, "_installedScriptClient", client);
        _ = form.Handle;

        var disconnect = InvokePrivateTask(form, "DisconnectHandyAsync");
        PumpUntil(() => disconnect.IsCompleted, TimeSpan.FromSeconds(3));
        disconnect.GetAwaiter().GetResult();
        PumpFor(TimeSpan.FromMilliseconds(20));

        Assert.IsNull(GetPrivateField(form, "_handy"));
        Assert.IsFalse((bool)GetPrivateField(form, "_handyDisconnecting")!);
        Assert.AreEqual("not connected", ((Label)GetPrivateField(form, "_offsetState")!).Text);
        Assert.AreEqual("Idle", ((Label)GetPrivateField(form, "_status")!).Text);
        var logText = ((TextBox)GetPrivateField(form, "_log")!).Text;
        Assert.IsTrue(logText.Contains("Could not stop Handy playback", StringComparison.Ordinal));
        Assert.IsTrue(logText.Contains("device state is unknown", StringComparison.Ordinal));
        Assert.IsFalse(logText.Contains("synthetic-secret-from-transport", StringComparison.Ordinal));
    }

    [STATestMethod]
    public void ClosingCompletesAfterStopFailureWithoutShowingShutdownDialog()
    {
        var shutdownDialogCount = 0;
        using var handler = new RecordingHandler((_, _, _) =>
            throw new InvalidOperationException("synthetic-secret-from-transport"));
        using var client = new HandyApiClient("test-key", handler, new Uri("https://example.test/api/"));
        using var form = new ScriptSyncPilot.HandySync.MainForm(new ScriptSyncPilot.HandySync.AppSettings(),
            _ => client, _ => { }, _ => shutdownDialogCount++);
        InitializeWritableForm(form, client);
        SetPrivateField(form, "_installedScriptClient", client);
        _ = form.Handle;

        var closingArgs = new FormClosingEventArgs(CloseReason.UserClosing, false);
        typeof(Form).GetMethod("OnFormClosing", PrivateInstance)!.Invoke(form, [closingArgs]);

        Assert.IsTrue(closingArgs.Cancel, "The first close event should defer closing until asynchronous cleanup completes.");
        PumpUntil(() => form.IsDisposed, TimeSpan.FromSeconds(3));

        Assert.AreEqual(0, shutdownDialogCount);
        Assert.IsTrue(handler.Requests.Any(request => request.Path == "hssp/stop"));
        Assert.IsTrue(form.IsDisposed, "The form should close after the failed stop attempt and cleanup.");
    }

    [STATestMethod]
    public void DisconnectStopTimeoutAgainstUnavailableServiceCompletesCleanupWithinBudget()
    {
        using var form = new ScriptSyncPilot.HandySync.MainForm();
        var stopCanceled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var handler = new RecordingHandler(async (request, _, cancellationToken) =>
        {
            if (request.RequestUri!.AbsolutePath != "/api/hssp/stop")
                throw new InvalidOperationException($"Unexpected request during disconnect: {request.RequestUri}");
            try
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                stopCanceled.TrySetResult();
                throw new TaskCanceledException("synthetic-secret-from-transport");
            }
            throw new InvalidOperationException("The stalled stop should only finish through cancellation.");
        });
        using var client = new HandyApiClient("test-key", handler, new Uri("https://example.test/api/"),
            controlRequestTimeout: TimeSpan.FromMilliseconds(100));
        InitializeWritableForm(form, client);
        SetPrivateField(form, "_installedScriptClient", client);
        _ = form.Handle;

        var teardownTimer = Stopwatch.StartNew();
        var disconnect = InvokePrivateTask(form, "DisconnectHandyAsync");
        PumpUntil(() => disconnect.IsCompleted && !(bool)GetPrivateField(form, "_handyDisconnecting")!,
            TimeSpan.FromSeconds(3));
        disconnect.GetAwaiter().GetResult();

        Assert.IsTrue(teardownTimer.Elapsed < TimeSpan.FromSeconds(3));
        Assert.IsTrue(stopCanceled.Task.IsCompleted, "The stalled stop request must be canceled by its control timeout.");
        Assert.AreEqual(1, handler.Requests.Count(request => request.Path == "hssp/stop"));
        Assert.IsNull(GetPrivateField(form, "_handy"));
        Assert.IsFalse((bool)GetPrivateField(form, "_handyDisconnecting")!);
        Assert.AreEqual("Idle", ((Label)GetPrivateField(form, "_status")!).Text);
        var logText = ((TextBox)GetPrivateField(form, "_log")!).Text;
        Assert.IsTrue(logText.Contains("Could not stop Handy playback: hssp/stop: request timed out.", StringComparison.Ordinal));
        Assert.IsFalse(logText.Contains("synthetic-secret-from-transport", StringComparison.Ordinal));
    }

    [STATestMethod]
    public void DisconnectStopThatIgnoresCancellationRemainsPendingUntilDeferredTeardownReleasesGate()
        => VerifyNonCooperativeStopTeardown(TimeSpan.FromSeconds(10));

    [STATestMethod]
    public void DisconnectStopHardRequestTimeoutRetainsGateUntilUnderlyingTransportCompletes()
        => VerifyNonCooperativeStopTeardown(TimeSpan.FromMilliseconds(100));

    private static void VerifyNonCooperativeStopTeardown(TimeSpan controlTimeout)
    {
        var stopStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseStop = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var handler = new RecordingHandler(async (request, _, _) =>
        {
            if (request.RequestUri!.AbsolutePath != "/api/hssp/stop")
                throw new InvalidOperationException($"Unexpected request during disconnect: {request.RequestUri}");
            stopStarted.TrySetResult();
            await releaseStop.Task; // Deliberately ignore cancellation to exercise the independent hard wait.
            return JsonResponse("{\"result\":\"ok\"}");
        });
        using var client = new HandyApiClient("test-key", handler, new Uri("https://example.test/api/"),
            controlRequestTimeout: controlTimeout);
        using var form = new ScriptSyncPilot.HandySync.MainForm();
        InitializeWritableForm(form, client);
        SetPrivateField(form, "_installedScriptClient", client);
        _ = form.Handle;

        var disconnect = InvokePrivateTask(form, "DisconnectHandyAsync");
        PumpUntil(() => stopStarted.Task.IsCompleted, TimeSpan.FromSeconds(3));
        PumpUntil(() => disconnect.IsCompleted, TimeSpan.FromSeconds(4));
        disconnect.GetAwaiter().GetResult();

        Assert.IsFalse(handler.IsDisposed, "The client must remain alive while the stop transport is still using it.");
        Assert.IsTrue((bool)GetPrivateField(form, "_handyDisconnecting")!,
            "Disconnect must remain pending until deferred stop teardown releases the gate.");
        Assert.IsTrue(((Label)GetPrivateField(form, "_status")!).Text.Contains("Disconnect pending", StringComparison.Ordinal));
        Assert.AreEqual(0, ((SemaphoreSlim)GetPrivateField(form, "_deviceCommandGate")!).CurrentCount,
            "The command gate must remain owned while the stop transport is pending.");

        releaseStop.TrySetResult();
        PumpUntil(() => handler.IsDisposed && !(bool)GetPrivateField(form, "_handyDisconnecting")!, TimeSpan.FromSeconds(3));

        Assert.AreEqual(1, handler.Requests.Count(request => request.Path == "hssp/stop"));
        Assert.AreEqual(1, ((SemaphoreSlim)GetPrivateField(form, "_deviceCommandGate")!).CurrentCount,
            "Deferred teardown releases the command gate after disposing the client.");
    }

    [STATestMethod]
    public void ClosingCompletesWhenStopTimesOutAgainstUnavailableServiceWithoutShutdownDialog()
    {
        var shutdownDialogCount = 0;
        var stopCanceled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var handler = new RecordingHandler(async (request, _, cancellationToken) =>
        {
            if (request.RequestUri!.AbsolutePath != "/api/hssp/stop")
                throw new InvalidOperationException($"Unexpected request during close: {request.RequestUri}");
            try
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                stopCanceled.TrySetResult();
                throw new TaskCanceledException("synthetic-secret-from-transport");
            }
            throw new InvalidOperationException("The stalled stop should only finish through cancellation.");
        });
        using var client = new HandyApiClient("test-key", handler, new Uri("https://example.test/api/"),
            controlRequestTimeout: TimeSpan.FromMilliseconds(100));
        using var form = new ScriptSyncPilot.HandySync.MainForm(new ScriptSyncPilot.HandySync.AppSettings(),
            _ => client, _ => { }, _ => shutdownDialogCount++);
        InitializeWritableForm(form, client);
        SetPrivateField(form, "_installedScriptClient", client);
        _ = form.Handle;

        var closingArgs = new FormClosingEventArgs(CloseReason.UserClosing, false);
        typeof(Form).GetMethod("OnFormClosing", PrivateInstance)!.Invoke(form, [closingArgs]);

        Assert.IsTrue(closingArgs.Cancel, "The first close event should defer closing until asynchronous cleanup completes.");
        PumpUntil(() => form.IsDisposed, TimeSpan.FromSeconds(3));

        Assert.AreEqual(0, shutdownDialogCount);
        Assert.IsTrue(stopCanceled.Task.IsCompleted, "The stalled stop request must be canceled by its control timeout.");
        Assert.AreEqual(1, handler.Requests.Count(request => request.Path == "hssp/stop"));
        Assert.IsTrue(form.IsDisposed, "The form should close after the timed-out stop attempt and cleanup.");
    }

    [STATestMethod]
    public void CanceledScriptInstallGateWaitDoesNotReleaseUnownedPermit()
    {
        using var handler = new RecordingHandler((_, _, _) => Task.FromResult(JsonResponse("{\"result\":\"ok\"}")));
        using var client = new HandyApiClient("test-key", handler, new Uri("https://example.test/api/"));
        using var form = new ScriptSyncPilot.HandySync.MainForm(new ScriptSyncPilot.HandySync.AppSettings(),
            _ => client, _ => { }, _ => { }, TimeSpan.FromMilliseconds(100));
        InitializeWritableForm(form, client);
        SetPrivateField(form, "_script", new ScriptSyncPilot.Common.Funscript());
        var deviceGate = (SemaphoreSlim)GetPrivateField(form, "_deviceCommandGate")!;
        Assert.IsTrue(deviceGate.Wait(0));

        using var queuedCancellation = new CancellationTokenSource();
        var queued = InvokePrivateTask(form, "EnsureCurrentScriptInstalledAsync", client, queuedCancellation.Token);
        Assert.IsFalse(queued.IsCompleted);
        queuedCancellation.Cancel();
        PumpUntil(() => queued.IsCompleted, TimeSpan.FromSeconds(2));
        Assert.IsTrue(queued.IsCanceled);

        var alreadyCanceled = InvokePrivateTask(form, "EnsureCurrentScriptInstalledAsync", client, new CancellationToken(true));
        PumpUntil(() => alreadyCanceled.IsCompleted, TimeSpan.FromSeconds(2));
        Assert.IsTrue(alreadyCanceled.IsCanceled);

        PumpFor(TimeSpan.FromMilliseconds(100));
        Assert.AreEqual(0, deviceGate.CurrentCount, "A canceled wait must not release the permit held by another owner.");
        deviceGate.Release();
        Assert.AreEqual(1, deviceGate.CurrentCount);
        Assert.AreEqual(0, handler.Requests.Count);
    }

    [STATestMethod]
    public void ShutdownDeadlineAbandonsDetachWithoutPostCloseUiOrDeviceWork()
    {
        var stopStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseStop = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var handler = new RecordingHandler(async (request, count, _) =>
        {
            if (request.RequestUri!.AbsolutePath != "/api/hssp/stop")
                throw new InvalidOperationException($"Unexpected request after shutdown: {request.RequestUri}");
            if (count == 1)
            {
                stopStarted.TrySetResult();
                await releaseStop.Task; // Non-cooperative: holds the session transition past the shutdown deadline.
            }
            return JsonResponse("{\"result\":\"ok\"}");
        });
        using var client = new HandyApiClient("test-key", handler, new Uri("https://example.test/api/"));
        using var form = new ScriptSyncPilot.HandySync.MainForm(new ScriptSyncPilot.HandySync.AppSettings(),
            _ => client, _ => { }, _ => { }, TimeSpan.FromMilliseconds(100));
        InitializeWritableForm(form, client);
        SetPrivateField(form, "_installedScriptClient", client);
        SetPrivateField(form, "_script", new ScriptSyncPilot.Common.Funscript());
        SetPrivateField(form, "_mpv", new ScriptSyncPilot.Common.MpvSession());
        _ = form.Handle;

        var detach = InvokePrivateTask(form, "DetachMpvAsync", true);
        PumpUntil(() => stopStarted.Task.IsCompleted, TimeSpan.FromSeconds(2));
        var closingArgs = new FormClosingEventArgs(CloseReason.UserClosing, false);
        typeof(Form).GetMethod("OnFormClosing", PrivateInstance)!.Invoke(form, [closingArgs]);
        var shutdownTimer = Stopwatch.StartNew();
        PumpUntil(() => form.IsDisposed, TimeSpan.FromSeconds(5));
        Assert.IsTrue(shutdownTimer.Elapsed >= TimeSpan.FromSeconds(1.5),
            "Shutdown must have proceeded only after the session-transition deadline expired.");

        releaseStop.TrySetResult();
        PumpUntil(() => detach.IsCompleted && handler.IsDisposed, TimeSpan.FromSeconds(3));

        Assert.IsTrue(detach.IsCompletedSuccessfully, "The abandoned transition must not fault after Close().");
        Assert.IsNull(GetPrivateField(form, "_mpv"));
        Assert.IsFalse(handler.Requests.Any(request => request.Path != "hssp/stop"),
            "The abandoned transition must not reinstall the script or issue other device work after shutdown.");
    }

    [STATestMethod]
    public void ShutdownCancelsDetachWaitingForDeviceGateSoTeardownOwnsFinalStop()
    {
        using var handler = new RecordingHandler((_, _, _) => Task.FromResult(JsonResponse("{\"result\":\"ok\"}")));
        using var client = new HandyApiClient("test-key", handler, new Uri("https://example.test/api/"));
        using var form = new ScriptSyncPilot.HandySync.MainForm(new ScriptSyncPilot.HandySync.AppSettings(),
            _ => client, _ => { }, _ => { }, TimeSpan.FromMilliseconds(100));
        InitializeWritableForm(form, client);
        SetPrivateField(form, "_installedScriptClient", client);
        SetPrivateField(form, "_mpv", new ScriptSyncPilot.Common.MpvSession());
        _ = form.Handle;
        var deviceGate = (SemaphoreSlim)GetPrivateField(form, "_deviceCommandGate")!;
        Assert.IsTrue(deviceGate.Wait(0));

        var detach = InvokePrivateTask(form, "DetachMpvAsync", true);
        Assert.IsFalse(detach.IsCompleted);
        var shutdownTimer = Stopwatch.StartNew();
        var closingArgs = new FormClosingEventArgs(CloseReason.UserClosing, false);
        typeof(Form).GetMethod("OnFormClosing", PrivateInstance)!.Invoke(form, [closingArgs]);
        PumpUntil(() => detach.IsCompleted && form.IsDisposed, TimeSpan.FromSeconds(3));

        Assert.IsTrue(shutdownTimer.Elapsed < TimeSpan.FromSeconds(1.5),
            "Shutdown must not wait for the session-transition deadline when the detach is only queued for the device gate.");
        Assert.IsTrue(detach.IsCompletedSuccessfully);
        Assert.AreEqual(0, handler.Requests.Count, "The canceled detach must not issue its own stop.");

        deviceGate.Release();
        PumpUntil(() => handler.IsDisposed, TimeSpan.FromSeconds(3));
        Assert.AreEqual(1, handler.Requests.Count(request => request.Path == "hssp/stop"),
            "Deferred Handy teardown issues the single final stop.");
    }

    [STATestMethod]
    public void RepeatedDisconnectSharesOneTeardownAndALaterDisconnectIsSafe()
    {
        using var form = new ScriptSyncPilot.HandySync.MainForm();
        var stopStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseStop = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var handler = new RecordingHandler(async (request, _, cancellationToken) =>
        {
            if (request.RequestUri!.AbsolutePath != "/api/hssp/stop")
                throw new InvalidOperationException($"Unexpected request during disconnect: {request.RequestUri}");
            stopStarted.TrySetResult();
            await releaseStop.Task.WaitAsync(cancellationToken);
            return JsonResponse("{\"result\":\"ok\"}");
        });
        using var client = new HandyApiClient("test-key", handler, new Uri("https://example.test/api/"));
        InitializeWritableForm(form, client);
        SetPrivateField(form, "_installedScriptClient", client);
        _ = form.Handle;

        var first = InvokePrivateTask(form, "DisconnectHandyAsync");
        PumpUntil(() => stopStarted.Task.IsCompleted, TimeSpan.FromSeconds(3));
        var second = InvokePrivateTask(form, "DisconnectHandyAsync");
        Assert.AreSame(first, second, "A disconnect requested during teardown must share the in-progress teardown task.");

        releaseStop.TrySetResult();
        PumpUntil(() => first.IsCompleted, TimeSpan.FromSeconds(3));
        first.GetAwaiter().GetResult();
        Assert.AreEqual(1, handler.Requests.Count(request => request.Path == "hssp/stop"));

        var third = InvokePrivateTask(form, "DisconnectHandyAsync");
        PumpUntil(() => third.IsCompleted, TimeSpan.FromSeconds(3));
        third.GetAwaiter().GetResult();
        PumpFor(TimeSpan.FromMilliseconds(100));

        Assert.AreEqual(1, handler.Requests.Count, "A disconnect after teardown must not issue any further device requests.");
        Assert.IsNull(GetPrivateField(form, "_handy"));
        Assert.IsFalse((bool)GetPrivateField(form, "_handyDisconnecting")!);
        Assert.AreEqual("Idle", ((Label)GetPrivateField(form, "_status")!).Text);
    }

    [STATestMethod]
    public void PendingWritesRecheckFirmwareSupportAfterAcquiringGate()
    {
        using var form = new ScriptSyncPilot.HandySync.MainForm();
        using var handler = new RecordingHandler((_, _, _) => Task.FromResult(JsonResponse("{\"result\":\"ok\"}")));
        using var client = new HandyApiClient("test-key", handler, new Uri("https://example.test/api/"));
        InitializeWritableForm(form, client);
        var gate = (SemaphoreSlim)GetPrivateField(form, "_deviceCommandGate")!;
        gate.Wait();
        ((DeviceSliderControl)GetPrivateField(form, "_offsetSlider")!).SetValues(125, 0);
        ((DeviceSliderControl)GetPrivateField(form, "_strokeSlider")!).SetValues(20, 80);
        InvokePrivate(form, "ScheduleOffsetWrite");
        InvokePrivate(form, "ScheduleStrokeWrite");
        PumpFor(TimeSpan.FromMilliseconds(250));

        SetPrivateField(form, "_deviceControlsSupported", false);
        gate.Release();
        PumpFor(TimeSpan.FromMilliseconds(350));

        Assert.AreEqual(0, handler.Requests.Count(request => request.Method == "PUT"));
    }

    [STATestMethod]
    public void PollSkipsBusyGateAndDoesNotRunOverlappingTicks()
    {
        using var form = new ScriptSyncPilot.HandySync.MainForm();
        using var handler = new RecordingHandler(async (_, count, _) =>
        {
            if (count == 1) await Task.Delay(150);
            return count switch
            {
                1 => JsonResponse("{\"result\":{\"offset\":125}}"),
                2 => JsonResponse("{\"result\":{\"min\":0.25,\"max\":0.75,\"min_absolute\":25,\"max_absolute\":85}}"),
                _ => throw new InvalidOperationException("Unexpected overlapping poll request.")
            };
        });
        using var client = new HandyApiClient("test-key", handler, new Uri("https://example.test/api/"));
        InitializePollingForm(form, client);
        var gate = (SemaphoreSlim)GetPrivateField(form, "_deviceCommandGate")!;
        gate.Wait();
        InvokePrivateTask(form, "PollDeviceValuesAsync").GetAwaiter().GetResult();
        Assert.AreEqual(0, handler.Requests.Count);
        gate.Release();

        var firstPoll = InvokePrivateTask(form, "PollDeviceValuesAsync");
        PumpUntil(() => handler.Requests.Count == 1, TimeSpan.FromSeconds(2));
        InvokePrivateTask(form, "PollDeviceValuesAsync").GetAwaiter().GetResult();
        Assert.AreEqual(1, handler.Requests.Count);
        PumpUntil(() => firstPoll.IsCompleted, TimeSpan.FromSeconds(3));
        firstPoll.GetAwaiter().GetResult();
        Assert.AreEqual(2, handler.Requests.Count);
    }

    [STATestMethod]
    public void PollStopsUsingClientWhenDisconnectBeginsDuringOffsetGet()
    {
        using var form = new ScriptSyncPilot.HandySync.MainForm();
        var offsetReadStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseOffsetRead = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var handler = new RecordingHandler(async (request, _, cancellationToken) =>
        {
            if (request.RequestUri!.AbsolutePath == "/api/hstp/offset")
            {
                offsetReadStarted.TrySetResult();
                await releaseOffsetRead.Task.WaitAsync(cancellationToken);
                return JsonResponse("{\"result\":{\"offset\":250}}");
            }
            if (request.RequestUri.AbsolutePath == "/api/slider/stroke")
                return JsonResponse("{\"result\":{\"min\":0.3,\"max\":0.7,\"min_absolute\":35,\"max_absolute\":80}}");
            if (request.RequestUri.AbsolutePath == "/api/slider/state")
                return JsonResponse("{\"result\":{\"motor_temp\":30}}");
            throw new InvalidOperationException($"Unexpected request: {request.RequestUri}");
        });
        using var client = new HandyApiClient("test-key", handler, new Uri("https://example.test/api/"));
        InitializePollingForm(form, client);
        var offset = (DeviceSliderControl)GetPrivateField(form, "_offsetSlider")!;
        var stroke = (DeviceSliderControl)GetPrivateField(form, "_strokeSlider")!;
        offset.SetValues(100, 0);
        stroke.SetValues(20, 80);
        SetPrivateField(form, "_lastDeviceOffset", 100);
        SetPrivateField(form, "_lastDeviceStroke", new HandyApiClient.SliderStroke
        {
            Min = 0.2,
            Max = 0.8,
            MinAbsolute = 25,
            MaxAbsolute = 85
        });

        var poll = InvokePrivateTask(form, "PollDeviceValuesAsync");
        PumpUntil(() => offsetReadStarted.Task.IsCompleted, TimeSpan.FromSeconds(3));
        SetPrivateField(form, "_handyDisconnecting", true);
        SetPrivateField(form, "_handy", null);
        releaseOffsetRead.TrySetResult();
        PumpUntil(() => poll.IsCompleted, TimeSpan.FromSeconds(3));
        poll.GetAwaiter().GetResult();

        Assert.AreEqual(100, offset.LowerValue);
        Assert.AreEqual(20, stroke.LowerValue);
        Assert.AreEqual(80, stroke.UpperValue);
        Assert.AreEqual(1, handler.Requests.Count(request => request.Path == "hstp/offset"));
        Assert.IsFalse(handler.Requests.Any(request => request.Path == "slider/stroke" || request.Path == "slider/state"));
    }

    [STATestMethod]
    public void UnopposedPollAdoptsExternalValuesWithoutIssuingWrites()
    {
        using var form = new ScriptSyncPilot.HandySync.MainForm();
        using var handler = new RecordingHandler((request, _, _) => Task.FromResult(request.RequestUri!.AbsolutePath switch
        {
            "/api/hstp/offset" => JsonResponse("{\"result\":{\"offset\":250}}"),
            "/api/slider/stroke" => JsonResponse("{\"result\":{\"min\":0.25,\"max\":0.75,\"min_absolute\":25,\"max_absolute\":85}}"),
            _ => throw new InvalidOperationException("Unexpected polling request.")
        }));
        using var client = new HandyApiClient("test-key", handler, new Uri("https://example.test/api/"));
        InitializePollingForm(form, client);
        SetPrivateField(form, "_lastDeviceOffset", 100);
        SetPrivateField(form, "_lastDeviceStroke", new HandyApiClient.SliderStroke
        {
            Min = 0.1,
            Max = 0.9,
            MinAbsolute = 15,
            MaxAbsolute = 95
        });

        InvokePrivateTask(form, "PollDeviceValuesAsync").GetAwaiter().GetResult();

        var offset = (DeviceSliderControl)GetPrivateField(form, "_offsetSlider")!;
        var stroke = (DeviceSliderControl)GetPrivateField(form, "_strokeSlider")!;
        var settings = (AppSettings)GetPrivateField(form, "_settings")!;
        Assert.AreEqual(250, offset.LowerValue);
        Assert.AreEqual(25, stroke.LowerValue);
        Assert.AreEqual(75, stroke.UpperValue);
        Assert.AreEqual(250, settings.DeviceOffsetMs);
        Assert.AreEqual(25, settings.StrokeMinPercent);
        Assert.AreEqual(75, settings.StrokeMaxPercent);
        Assert.AreEqual(2, handler.Requests.Count);
        Assert.IsTrue(handler.Requests.All(request => request.Method == "GET"));
    }

    [STATestMethod]
    public void PollDuringStationaryDragDoesNotAdvanceBaselinesOrMillimetres()
    {
        using var form = new ScriptSyncPilot.HandySync.MainForm();
        using var handler = new RecordingHandler((request, _, _) => Task.FromResult(request.RequestUri!.AbsolutePath switch
        {
            "/api/hstp/offset" => JsonResponse("{\"result\":{\"offset\":250}}"),
            "/api/slider/stroke" => JsonResponse("{\"result\":{\"min\":0.3,\"max\":0.75,\"min_absolute\":35,\"max_absolute\":85}}"),
            _ => throw new InvalidOperationException("Unexpected polling request.")
        }));
        using var client = new HandyApiClient("test-key", handler, new Uri("https://example.test/api/"));
        InitializePollingForm(form, client);
        var offset = (DeviceSliderControl)GetPrivateField(form, "_offsetSlider")!;
        var stroke = (DeviceSliderControl)GetPrivateField(form, "_strokeSlider")!;
        offset.SetValues(100, 0);
        stroke.SetValues(20, 80);
        SetPrivateField(form, "_lastDeviceOffset", 100);
        SetPrivateField(form, "_lastDeviceStroke", new HandyApiClient.SliderStroke
        {
            Min = 0.2,
            Max = 0.8,
            MinAbsolute = 25,
            MaxAbsolute = 85
        });
        SetPrivateField(form, "_strokeMinAbsolute", 25d);
        SetPrivateField(form, "_strokeMaxAbsolute", 85d);
        InvokePrivate(form, "UpdateStrokeLabel");
        SetPrivateField(offset, "_dragging", true);
        SetPrivateField(stroke, "_dragging", true);

        InvokePrivateTask(form, "PollDeviceValuesAsync").GetAwaiter().GetResult();

        Assert.AreEqual(100, offset.LowerValue);
        Assert.AreEqual(20, stroke.LowerValue);
        Assert.AreEqual(80, stroke.UpperValue);
        Assert.AreEqual(100, GetPrivateField(form, "_lastDeviceOffset"));
        var heldBaseline = (HandyApiClient.SliderStroke)GetPrivateField(form, "_lastDeviceStroke")!;
        Assert.AreEqual(0.2, heldBaseline.Min);
        Assert.AreEqual(25d, GetPrivateField(form, "_strokeMinAbsolute"));
        Assert.IsTrue(((Label)GetPrivateField(form, "_strokeValue")!).Text.Contains("25.0-85.0 mm"));

        SetPrivateField(offset, "_dragging", false);
        SetPrivateField(stroke, "_dragging", false);
        InvokePrivateTask(form, "PollDeviceValuesAsync").GetAwaiter().GetResult();

        Assert.AreEqual(250, offset.LowerValue);
        Assert.AreEqual(30, stroke.LowerValue);
        Assert.AreEqual(75, stroke.UpperValue);
        Assert.IsTrue(((Label)GetPrivateField(form, "_strokeValue")!).Text.Contains("35.0-85.0 mm"));
    }

    [STATestMethod]
    public void PollFailuresPreserveValuesAndConnectionThenMarkUnknown()
    {
        using var form = new ScriptSyncPilot.HandySync.MainForm();
        using var handler = new RecordingHandler((_, _, _) => Task.FromResult(JsonResponse(
            "{\"error\":{\"code\":1002,\"name\":\"DeviceTimeout\"}}")));
        using var client = new HandyApiClient("test-key", handler, new Uri("https://example.test/api/"));
        InitializePollingForm(form, client);
        _ = form.Handle;
        var offset = (DeviceSliderControl)GetPrivateField(form, "_offsetSlider")!;
        var stroke = (DeviceSliderControl)GetPrivateField(form, "_strokeSlider")!;
        offset.SetValues(33, 0);
        stroke.SetValues(20, 80);
        var initialStrokeState = ((Label)GetPrivateField(form, "_strokeState")!).Text;

        for (var attempt = 0; attempt < 3; attempt++)
        {
            InvokePrivateTask(form, "PollDeviceValuesAsync").GetAwaiter().GetResult();
            Application.DoEvents();
        }

        Assert.AreSame(client, GetPrivateField(form, "_handy"));
        Assert.AreEqual(33, offset.LowerValue);
        Assert.AreEqual(20, stroke.LowerValue);
        Assert.AreEqual(80, stroke.UpperValue);
        Assert.AreEqual("device values unknown", ((Label)GetPrivateField(form, "_offsetState")!).Text);
        Assert.AreEqual(initialStrokeState, ((Label)GetPrivateField(form, "_strokeState")!).Text);
        var log = (TextBox)GetPrivateField(form, "_log")!;
        Assert.AreEqual(1, log.Text.Split("Device value polling failed:", StringSplitOptions.None).Length - 1);
    }

    [STATestMethod]
    public void SuccessfulPollRecoversFailedInitialReadsWhenNoLocalEditOccurred()
    {
        using var form = new ScriptSyncPilot.HandySync.MainForm();
        var offsetReadCount = 0;
        var strokeReadCount = 0;
        using var handler = new RecordingHandler((request, _, _) => Task.FromResult(request.RequestUri!.AbsolutePath switch
        {
            "/api/capabilities" => JsonResponse("{\"result\":{\"slider\":1}}"),
            "/api/settings/slider" => JsonResponse("{\"result\":{\"x_limit_start\":0,\"x_limit_stop\":110,\"x_min_speed\":30,\"x_max_speed\":400}}"),
            "/api/hstp/offset" => offsetReadCount++ == 0
                ? JsonResponse("{\"error\":{\"code\":1002,\"name\":\"DeviceTimeout\"}}")
                : JsonResponse("{\"result\":{\"offset\":425}}"),
            "/api/slider/stroke" => strokeReadCount++ == 0
                ? JsonResponse("{\"error\":{\"code\":1002,\"name\":\"DeviceTimeout\"}}")
                : JsonResponse("{\"result\":{\"min\":0.3,\"max\":0.8,\"min_absolute\":35,\"max_absolute\":90}}"),
            _ => throw new InvalidOperationException("Unexpected request.")
        }));
        using var client = new HandyApiClient("test-key", handler, new Uri("https://example.test/api/"));
        InitializePollingForm(form, client);
        SetPrivateField(form, "_lastDeviceOffset", null);
        SetPrivateField(form, "_lastDeviceStroke", null);

        InvokePrivateTask(form, "ReadAndAdoptDeviceControlsAsync", client).GetAwaiter().GetResult();
        ((System.Windows.Forms.Timer)GetPrivateField(form, "_devicePollTimer")!).Stop();
        Assert.AreEqual("not applied", ((Label)GetPrivateField(form, "_offsetState")!).Text);
        Assert.AreEqual("not applied", ((Label)GetPrivateField(form, "_strokeState")!).Text);

        InvokePrivateTask(form, "PollDeviceValuesAsync").GetAwaiter().GetResult();

        var offset = (DeviceSliderControl)GetPrivateField(form, "_offsetSlider")!;
        var stroke = (DeviceSliderControl)GetPrivateField(form, "_strokeSlider")!;
        var settings = (AppSettings)GetPrivateField(form, "_settings")!;
        Assert.AreEqual(425, offset.LowerValue);
        Assert.AreEqual(30, stroke.LowerValue);
        Assert.AreEqual(80, stroke.UpperValue);
        Assert.AreEqual(425, settings.DeviceOffsetMs);
        Assert.AreEqual(30, settings.StrokeMinPercent);
        Assert.AreEqual(80, settings.StrokeMaxPercent);
        Assert.AreEqual("applied", ((Label)GetPrivateField(form, "_offsetState")!).Text);
        Assert.AreEqual("applied", ((Label)GetPrivateField(form, "_strokeState")!).Text);
        Assert.IsFalse((bool)GetPrivateField(form, "_offsetInitialReadFailed")!);
        Assert.IsFalse((bool)GetPrivateField(form, "_strokeInitialReadFailed")!);
    }

    [STATestMethod]
    public void PollDoesNotReplaceAUserValueAfterItsWriteFailed()
    {
        using var form = new ScriptSyncPilot.HandySync.MainForm();
        using var handler = new RecordingHandler((request, _, _) => Task.FromResult(request.RequestUri!.AbsolutePath switch
        {
            "/api/hstp/offset" => JsonResponse("{\"result\":{\"offset\":300}}"),
            "/api/slider/stroke" => JsonResponse("{\"result\":{\"min\":0.3,\"max\":0.7,\"min_absolute\":35,\"max_absolute\":80}}"),
            "/api/slider/state" => JsonResponse("{\"result\":{\"motor_temp\":30}}"),
            _ => throw new InvalidOperationException("Unexpected request.")
        }));
        using var client = new HandyApiClient("test-key", handler, new Uri("https://example.test/api/"));
        InitializePollingForm(form, client);
        var offset = (DeviceSliderControl)GetPrivateField(form, "_offsetSlider")!;
        var stroke = (DeviceSliderControl)GetPrivateField(form, "_strokeSlider")!;
        offset.SetValues(200, 0);
        stroke.SetValues(20, 80);
        SetPrivateField(form, "_offsetWriteFailed", true);
        SetPrivateField(form, "_strokeWriteFailed", true);
        ((Label)GetPrivateField(form, "_offsetState")!).Text = "not applied";
        ((Label)GetPrivateField(form, "_strokeState")!).Text = "not applied";

        for (var attempt = 0; attempt < 3; attempt++)
            InvokePrivateTask(form, "PollDeviceValuesAsync").GetAwaiter().GetResult();

        Assert.AreEqual(200, offset.LowerValue);
        Assert.AreEqual(20, stroke.LowerValue);
        Assert.AreEqual(80, stroke.UpperValue);
        Assert.AreEqual("not applied", ((Label)GetPrivateField(form, "_offsetState")!).Text);
        Assert.AreEqual("not applied", ((Label)GetPrivateField(form, "_strokeState")!).Text);
        Assert.AreEqual(125, GetPrivateField(form, "_lastDeviceOffset"));
        var lastStroke = (HandyApiClient.SliderStroke)GetPrivateField(form, "_lastDeviceStroke")!;
        Assert.AreEqual(0.25, lastStroke.Min);
        Assert.AreEqual(0.75, lastStroke.Max);
    }

    [STATestMethod]
    public void SuccessfulUnchangedPollRestoresUnknownStatesAfterFailures()
    {
        using var form = new ScriptSyncPilot.HandySync.MainForm();
        var returnFailure = true;
        using var handler = new RecordingHandler((request, _, _) => Task.FromResult(
            returnFailure
                ? JsonResponse("{\"error\":{\"code\":1002,\"name\":\"DeviceTimeout\"}}")
                : request.RequestUri!.AbsolutePath switch
                {
                    "/api/hstp/offset" => JsonResponse("{\"result\":{\"offset\":125}}"),
                    "/api/slider/stroke" => JsonResponse("{\"result\":{\"min\":0.25,\"max\":0.75,\"min_absolute\":25,\"max_absolute\":85}}"),
                    _ => throw new InvalidOperationException("Unexpected request.")
                }));
        using var client = new HandyApiClient("test-key", handler, new Uri("https://example.test/api/"));
        InitializePollingForm(form, client);
        _ = form.Handle;
        var offset = (DeviceSliderControl)GetPrivateField(form, "_offsetSlider")!;
        var stroke = (DeviceSliderControl)GetPrivateField(form, "_strokeSlider")!;
        offset.SetValues(125, 0);
        stroke.SetValues(25, 75);
        var initialStrokeState = ((Label)GetPrivateField(form, "_strokeState")!).Text;

        for (var attempt = 0; attempt < 3; attempt++)
        {
            InvokePrivateTask(form, "PollDeviceValuesAsync").GetAwaiter().GetResult();
            Application.DoEvents();
        }
        Assert.AreEqual("device values unknown", ((Label)GetPrivateField(form, "_offsetState")!).Text);
        Assert.AreEqual(initialStrokeState, ((Label)GetPrivateField(form, "_strokeState")!).Text);

        returnFailure = false;
        InvokePrivateTask(form, "PollDeviceValuesAsync").GetAwaiter().GetResult();

        Assert.AreEqual(125, offset.LowerValue);
        Assert.AreEqual(25, stroke.LowerValue);
        Assert.AreEqual(75, stroke.UpperValue);
        Assert.AreEqual("applied", ((Label)GetPrivateField(form, "_offsetState")!).Text);
        Assert.AreEqual(initialStrokeState, ((Label)GetPrivateField(form, "_strokeState")!).Text);
    }

    [STATestMethod]
    [DataRow("hstp/offset")]
    [DataRow("slider/stroke")]
    [DataRow("slider/state")]
    public void PollFailuresOnlyMarkTheFailingControlsUnknownAndRecoverIndependently(string failingEndpoint)
    {
        using var form = new ScriptSyncPilot.HandySync.MainForm();
        var returnFailure = true;
        using var handler = new RecordingHandler((request, _, _) =>
        {
            var endpoint = request.RequestUri!.AbsolutePath.Replace("/api/", string.Empty);
            return Task.FromResult(returnFailure && endpoint == failingEndpoint
                ? JsonResponse("{\"error\":{\"code\":1002,\"name\":\"DeviceTimeout\"}}")
                : endpoint switch
                {
                    "hstp/offset" => JsonResponse("{\"result\":{\"offset\":125}}"),
                    "slider/stroke" => JsonResponse("{\"result\":{\"min\":0.25,\"max\":0.75,\"min_absolute\":25,\"max_absolute\":85}}"),
                    "slider/state" => JsonResponse("{\"result\":{\"motor_temp\":30}}"),
                    _ => throw new InvalidOperationException("Unexpected request.")
                });
        });
        using var client = new HandyApiClient("test-key", handler, new Uri("https://example.test/api/"));
        InitializePollingForm(form, client);
        var offsetState = (Label)GetPrivateField(form, "_offsetState")!;
        var strokeState = (Label)GetPrivateField(form, "_strokeState")!;
        offsetState.Text = "applied";
        strokeState.Text = "applied";
        _ = form.Handle;

        for (var attempt = 0; attempt < 9; attempt++)
        {
            InvokePrivateTask(form, "PollDeviceValuesAsync").GetAwaiter().GetResult();
            Application.DoEvents();
        }

        Assert.AreEqual(failingEndpoint == "hstp/offset" ? "device values unknown" : "applied", offsetState.Text);
        Assert.AreEqual(failingEndpoint == "slider/stroke" ? "device values unknown" : "applied", strokeState.Text);
        Assert.AreSame(client, GetPrivateField(form, "_handy"));
        var log = (TextBox)GetPrivateField(form, "_log")!;
        Assert.AreEqual(1, log.Text.Split($"Device value polling failed: {failingEndpoint}:", StringSplitOptions.None).Length - 1);

        returnFailure = false;
        for (var attempt = 0; attempt < 3; attempt++)
            InvokePrivateTask(form, "PollDeviceValuesAsync").GetAwaiter().GetResult();

        Assert.AreEqual("applied", offsetState.Text);
        Assert.AreEqual("applied", strokeState.Text);
    }

    [STATestMethod]
    public void PollDoesNotStartAfterDisconnectOrCloseBegins()
    {
        using var form = new ScriptSyncPilot.HandySync.MainForm();
        using var handler = new RecordingHandler((_, _, _) => Task.FromResult(JsonResponse("{\"result\":{\"offset\":0}}")));
        using var client = new HandyApiClient("test-key", handler, new Uri("https://example.test/api/"));
        InitializePollingForm(form, client);

        SetPrivateField(form, "_handyDisconnecting", true);
        InvokePrivateTask(form, "PollDeviceValuesAsync").GetAwaiter().GetResult();
        SetPrivateField(form, "_handyDisconnecting", false);
        SetPrivateField(form, "_isClosing", true);
        InvokePrivateTask(form, "PollDeviceValuesAsync").GetAwaiter().GetResult();

        Assert.AreEqual(0, handler.Requests.Count);
    }

    [STATestMethod]
    [DataRow(true, true)]
    [DataRow(false, true)]
    [DataRow(true, false)]
    public void DisconnectClearsDeviceSpecificStrokeDetails(bool controlsSupported, bool hasSlider)
    {
        using var form = new ScriptSyncPilot.HandySync.MainForm();
        using var client = new HandyApiClient("test-key", new RecordingHandler((_, _, _) =>
            Task.FromResult(JsonResponse("{\"result\":\"ok\"}"))), new Uri("https://example.test/api/"));
        SetPrivateField(form, "_handy", client);
        SetPrivateField(form, "_deviceControlsSupported", controlsSupported);
        SetPrivateField(form, "_hasDeviceSlider", hasSlider);
        SetPrivateField(form, "_strokeMinAbsolute", 25d);
        SetPrivateField(form, "_strokeMaxAbsolute", 85d);
        SetPrivateField(form, "_temperatureHighTrigger", 80d);
        SetPrivateField(form, "_temperatureHysteresis", 10d);
        var stroke = (DeviceSliderControl)GetPrivateField(form, "_strokeSlider")!;
        var offset = (DeviceSliderControl)GetPrivateField(form, "_offsetSlider")!;
        offset.Enabled = controlsSupported;
        stroke.Enabled = controlsSupported && hasSlider;
        ((Label)GetPrivateField(form, "_offsetState")!).Text = controlsSupported ? "applied" : "unsupported";
        ((Label)GetPrivateField(form, "_strokeState")!).Text = hasSlider ? "unsupported" : "no slider";
        stroke.SetValues(20, 80);
        InvokePrivate(form, "UpdateStrokeLabel");
        var toolTip = (ToolTip)GetPrivateField(form, "_deviceToolTip")!;
        toolTip.SetToolTip(stroke, "Previous device physical settings.");
        _ = form.Handle;

        var disconnect = InvokePrivateTask(form, "DisconnectHandyAsync");
        PumpUntil(() => disconnect.IsCompleted, TimeSpan.FromSeconds(3));
        disconnect.GetAwaiter().GetResult();

        Assert.IsTrue(offset.Enabled);
        Assert.IsTrue(stroke.Enabled);
        Assert.AreEqual("not connected", ((Label)GetPrivateField(form, "_offsetState")!).Text);
        Assert.AreEqual("not connected", ((Label)GetPrivateField(form, "_strokeState")!).Text);
        Assert.IsNull(GetPrivateField(form, "_strokeMinAbsolute"));
        Assert.IsNull(GetPrivateField(form, "_strokeMaxAbsolute"));
        Assert.IsNull(GetPrivateField(form, "_temperatureHighTrigger"));
        Assert.IsNull(GetPrivateField(form, "_temperatureHysteresis"));
        Assert.AreEqual("20-80 %", ((Label)GetPrivateField(form, "_strokeValue")!).Text);
        Assert.AreEqual(string.Empty, toolTip.GetToolTip(stroke));
        Assert.AreEqual("Motor -- °C", ((Label)GetPrivateField(form, "_temperatureValue")!).Text);
    }

    [STATestMethod]
    public void TemperatureIsPolledEveryThirdTickWithoutPlaybackCommands()
    {
        using var form = new ScriptSyncPilot.HandySync.MainForm();
        var temperatureReadCount = 0;
        using var handler = new RecordingHandler((request, _, _) =>
        {
            var path = request.RequestUri!.AbsolutePath;
            if (path == "/api/slider/state")
            {
                var temperature = temperatureReadCount++ switch { 0 => 70, 1 => 80, _ => 69 };
                return Task.FromResult(JsonResponse($"{{\"result\":{{\"motor_temp\":{temperature}}}}}"));
            }
            return Task.FromResult(path switch
            {
                "/api/hstp/offset" => JsonResponse("{\"result\":{\"offset\":125}}"),
                "/api/slider/stroke" => JsonResponse("{\"result\":{\"min\":0.25,\"max\":0.75,\"min_absolute\":25,\"max_absolute\":85}}"),
                _ => throw new InvalidOperationException($"Unexpected request: {request.Method} {request.RequestUri}")
            });
        });
        using var client = new HandyApiClient("test-key", handler, new Uri("https://example.test/api/"));
        InitializePollingForm(form, client);
        SetPrivateField(form, "_temperatureHighTrigger", 80d);
        SetPrivateField(form, "_temperatureHysteresis", 10d);
        _ = form.Handle;

        for (var tick = 0; tick < 9; tick++)
        {
            InvokePrivateTask(form, "PollDeviceValuesAsync").GetAwaiter().GetResult();
            Application.DoEvents();
        }

        Assert.AreEqual(3, handler.Requests.Count(request => request.Path == "slider/state"));
        Assert.AreEqual(21, handler.Requests.Count(request => request.Method == "GET"));
        var log = (TextBox)GetPrivateField(form, "_log")!;
        Assert.AreEqual(3, log.Text.Split("Motor temperature state:", StringSplitOptions.None).Length - 1);
        Assert.IsFalse(handler.Requests.Any(request => request.Path.StartsWith("hssp/", StringComparison.Ordinal)));
    }

    [STATestMethod]
    public void MissingTemperatureSettingsUseFallbackTrigger()
    {
        using var form = new ScriptSyncPilot.HandySync.MainForm();
        using var handler = new RecordingHandler((_, _, _) => Task.FromResult(
            JsonResponse("{\"result\":{\"motor_temp\":68}}")));
        using var client = new HandyApiClient("test-key", handler, new Uri("https://example.test/api/"));
        SetPrivateField(form, "_handy", client);
        SetPrivateField(form, "_deviceControlsSupported", true);
        SetPrivateField(form, "_temperatureHighTrigger", null);
        SetPrivateField(form, "_temperatureHysteresis", null);

        InvokePrivateTask(form, "UpdateTemperatureAsync", client).GetAwaiter().GetResult();

        Assert.AreEqual(DeviceTemperatureBand.Warm, GetPrivateField(form, "_temperatureBand"));
        Assert.AreEqual("Motor 68.0 °C (Warm)", ((Label)GetPrivateField(form, "_temperatureValue")!).Text);
    }

    [STATestMethod]
    public void TemperatureReadFailureClearsPreviousMeasurementAndRestoresNeutralColors()
    {
        using var form = new ScriptSyncPilot.HandySync.MainForm();
        var responseCount = 0;
        using var handler = new RecordingHandler((request, _, _) => Task.FromResult(request.RequestUri!.AbsolutePath switch
        {
            "/api/slider/state" => responseCount++ == 0
                ? JsonResponse("{\"result\":{\"motor_temp\":82}}")
                : new HttpResponseMessage(HttpStatusCode.ServiceUnavailable),
            "/api/hstp/offset" => JsonResponse("{\"result\":{\"offset\":125}}"),
            "/api/slider/stroke" => JsonResponse("{\"result\":{\"min\":0.25,\"max\":0.75,\"min_absolute\":25,\"max_absolute\":85}}"),
            _ => throw new InvalidOperationException("Unexpected polling request.")
        }));
        using var client = new HandyApiClient("test-key", handler, new Uri("https://example.test/api/"));
        InitializePollingForm(form, client);
        SetPrivateField(form, "_temperatureHighTrigger", 80d);
        SetPrivateField(form, "_temperatureHysteresis", 10d);
        _ = form.Handle;

        InvokePrivateTask(form, "UpdateTemperatureAsync", client).GetAwaiter().GetResult();
        var temperature = (Label)GetPrivateField(form, "_temperatureValue")!;
        Assert.AreEqual("Motor 82.0 °C (High)", temperature.Text);
        Assert.AreEqual(DeviceTemperatureBand.Hot, GetPrivateField(form, "_temperatureBand"));

        SetPrivateField(form, "_devicePollTick", 2);
        InvokePrivateTask(form, "PollDeviceValuesAsync").GetAwaiter().GetResult();

        Assert.AreEqual("Motor -- °C (unavailable)", temperature.Text);
        Assert.AreEqual(DeviceTemperatureBand.Normal, GetPrivateField(form, "_temperatureBand"));
        Assert.AreEqual(SystemColors.ControlText, temperature.ForeColor);
        Assert.AreEqual(SystemColors.Control, temperature.BackColor);
    }

    [STATestMethod]
    public void FailedOffsetAndStrokeWritesAreMarkedNotConfirmed()
    {
        using var form = new ScriptSyncPilot.HandySync.MainForm();
        using var handler = new RecordingHandler((_, _, _) => Task.FromResult(
            new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)));
        using var client = new HandyApiClient("test-key", handler, new Uri("https://example.test/api/"));
        InitializeWritableForm(form, client);
        _ = form.Handle;
        var offset = (DeviceSliderControl)GetPrivateField(form, "_offsetSlider")!;
        var stroke = (DeviceSliderControl)GetPrivateField(form, "_strokeSlider")!;
        offset.SetValues(175, 0);
        stroke.SetValues(25, 75);
        InvokePrivate(form, "ScheduleOffsetWrite");
        InvokePrivate(form, "ScheduleStrokeWrite");
        var offsetState = (Label)GetPrivateField(form, "_offsetState")!;
        var strokeState = (Label)GetPrivateField(form, "_strokeState")!;
        PumpUntil(() => offsetState.Text == "not confirmed" && strokeState.Text == "not confirmed", TimeSpan.FromSeconds(4));

        var log = (TextBox)GetPrivateField(form, "_log")!;
        Assert.IsTrue(log.Text.Contains("Device offset write failed:", StringComparison.Ordinal));
        Assert.IsTrue(log.Text.Contains("Device stroke write failed:", StringComparison.Ordinal));
        Assert.IsTrue(handler.Requests.Any(request => request.Method == "PUT" && request.Path == "hstp/offset"));
        Assert.IsTrue(handler.Requests.Any(request => request.Method == "PUT" && request.Path == "slider/stroke"));
    }

    [STATestMethod]
    public void FailedOptionalReadsLeaveControlsUsableAndNoSliderSuppressesStrokeRead()
    {
        using var form = new ScriptSyncPilot.HandySync.MainForm();
        using var handler = new RecordingHandler((request, _, _) => Task.FromResult(request.RequestUri!.AbsolutePath switch
        {
            "/api/capabilities" => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable),
            "/api/settings/slider" => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable),
            "/api/hstp/offset" => JsonResponse("{\"result\":{\"offset\":125}}"),
            "/api/slider/stroke" => JsonResponse("{\"result\":{\"min\":0.2,\"max\":0.8,\"min_absolute\":25,\"max_absolute\":85}}"),
            "/api/slider/state" => JsonResponse("{\"result\":{\"motor_temp\":77}}"),
            _ => throw new InvalidOperationException("Unexpected request.")
        }));
        using var client = new HandyApiClient("test-key", handler, new Uri("https://example.test/api/"));
        InitializePollingForm(form, client);
        SetPrivateField(form, "_temperatureHighTrigger", 80d);
        SetPrivateField(form, "_temperatureHysteresis", 10d);
        var tooltip = (ToolTip)GetPrivateField(form, "_deviceToolTip")!;
        var strokeSliderForTooltip = (Control)GetPrivateField(form, "_strokeSlider")!;
        tooltip.SetToolTip(strokeSliderForTooltip, "Previous device physical slider settings.");

        InvokePrivateTask(form, "ReadAndAdoptDeviceControlsAsync", client).GetAwaiter().GetResult();
        ((System.Windows.Forms.Timer)GetPrivateField(form, "_devicePollTimer")!).Stop();

        Assert.IsTrue(((DeviceSliderControl)GetPrivateField(form, "_strokeSlider")!).Enabled);
        Assert.AreEqual("applied", ((Label)GetPrivateField(form, "_offsetState")!).Text);
        Assert.AreEqual("applied", ((Label)GetPrivateField(form, "_strokeState")!).Text);
        Assert.IsNull(GetPrivateField(form, "_temperatureHighTrigger"));
        Assert.IsNull(GetPrivateField(form, "_temperatureHysteresis"));
        Assert.AreEqual(string.Empty, tooltip.GetToolTip(strokeSliderForTooltip));
        InvokePrivateTask(form, "UpdateTemperatureAsync", client).GetAwaiter().GetResult();
        Assert.AreEqual("Motor 77.0 °C (High)", ((Label)GetPrivateField(form, "_temperatureValue")!).Text);

        using var noSliderForm = new ScriptSyncPilot.HandySync.MainForm();
        using var noSliderHandler = new RecordingHandler((request, _, _) => Task.FromResult(request.RequestUri!.AbsolutePath switch
        {
            "/api/capabilities" => JsonResponse("{\"result\":{\"slider\":0}}"),
            "/api/settings/slider" => JsonResponse("{\"result\":{\"x_limit_start\":0,\"x_limit_stop\":110,\"x_min_speed\":30,\"x_max_speed\":400}}"),
            "/api/hstp/offset" => JsonResponse("{\"result\":{\"offset\":125}}"),
            _ => throw new InvalidOperationException("Stroke endpoint must be suppressed when no slider is present.")
        }));
        using var noSliderClient = new HandyApiClient("test-key", noSliderHandler, new Uri("https://example.test/api/"));
        InitializePollingForm(noSliderForm, noSliderClient);

        InvokePrivateTask(noSliderForm, "ReadAndAdoptDeviceControlsAsync", noSliderClient).GetAwaiter().GetResult();
        ((System.Windows.Forms.Timer)GetPrivateField(noSliderForm, "_devicePollTimer")!).Stop();

        Assert.IsFalse(((DeviceSliderControl)GetPrivateField(noSliderForm, "_strokeSlider")!).Enabled);
        Assert.IsFalse(noSliderHandler.Requests.Any(request => request.Path == "slider/stroke"));
        InvokePrivate(noSliderForm, "ScheduleStrokeWrite");
        Assert.IsFalse(noSliderHandler.Requests.Any(request => request.Method == "PUT"));
    }

    private static void InitializeWritableForm(ScriptSyncPilot.HandySync.MainForm form, HandyApiClient client)
    {
        SetPrivateField(form, "_handy", client);
        SetPrivateField(form, "_deviceControlsSupported", true);
        SetPrivateField(form, "_hasDeviceSlider", true);
    }

    private static void InitializePollingForm(ScriptSyncPilot.HandySync.MainForm form, HandyApiClient client)
    {
        SetPrivateField(form, "_handy", client);
        SetPrivateField(form, "_deviceControlsSupported", true);
        SetPrivateField(form, "_hasDeviceSlider", true);
        SetPrivateField(form, "_lastDeviceOffset", 125);
        SetPrivateField(form, "_lastDeviceStroke", new HandyApiClient.SliderStroke
        {
            Min = 0.25,
            Max = 0.75,
            MinAbsolute = 25,
            MaxAbsolute = 85
        });
    }

    private static void PumpUntil(Func<bool> condition, TimeSpan timeout)
    {
        var timer = Stopwatch.StartNew();
        while (!condition() && timer.Elapsed < timeout)
        {
            Application.DoEvents();
            Thread.Sleep(5);
        }
        Assert.IsTrue(condition(), "Timed out while pumping the WinForms message loop.");
    }

    private static void PumpFor(TimeSpan duration)
    {
        var timer = Stopwatch.StartNew();
        while (timer.Elapsed < duration)
        {
            Application.DoEvents();
            Thread.Sleep(5);
        }
    }

    private static object? GetPrivateField(object target, string name) =>
        target.GetType().GetField(name, PrivateInstance)!.GetValue(target);

    private static void SetPrivateField(object target, string name, object? value) =>
        target.GetType().GetField(name, PrivateInstance)!.SetValue(target, value);

    private static void InvokePrivate(object target, string name) =>
        target.GetType().GetMethod(name, PrivateInstance)!.Invoke(target, null);

    private static Task InvokePrivateTask(object target, string name, params object[] arguments) =>
        (Task)target.GetType().GetMethod(name, PrivateInstance)!.Invoke(target, arguments)!;

    private static HttpResponseMessage JsonResponse(string json) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(json, Encoding.UTF8, "application/json")
    };

    private sealed class RecordingHandler(Func<HttpRequestMessage, int, CancellationToken, Task<HttpResponseMessage>> respond) : HttpMessageHandler
    {
        public List<RequestRecord> Requests { get; } = [];
        public bool IsDisposed { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var count = Requests.Count + 1;
            var body = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
            Requests.Add(new RequestRecord(request.Method.Method, request.RequestUri!.AbsolutePath.TrimStart('/').Replace("api/", "", StringComparison.Ordinal), body));
            return await respond(request, count, cancellationToken);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) IsDisposed = true;
            base.Dispose(disposing);
        }
    }

    private sealed class BlockingStrokeHandler : HttpMessageHandler
    {
        public TaskCompletionSource FirstWriteStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource ReleaseFirstWrite { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public List<string> Bodies { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var body = await request.Content!.ReadAsStringAsync(cancellationToken);
            Bodies.Add(body);
            if (Bodies.Count == 1)
            {
                FirstWriteStarted.TrySetResult();
                await ReleaseFirstWrite.Task;
            }
            using var parsed = JsonDocument.Parse(body);
            var min = parsed.RootElement.GetProperty("min").GetDouble();
            var max = parsed.RootElement.GetProperty("max").GetDouble();
            return JsonResponse(JsonSerializer.Serialize(new
            {
                result = new { min, max, min_absolute = min * 100, max_absolute = max * 100 }
            }));
        }
    }

    private sealed record RequestRecord(string Method, string Path, string? Body);
}
