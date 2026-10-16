using System.Reflection;
using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ScriptSyncPilot.Common;
using ScriptSyncPilot.FunscriptEditor;
using ScriptSyncPilot.HandySync;
using MainForm = ScriptSyncPilot.FunscriptEditor.MainForm;
using HandyForm = ScriptSyncPilot.HandySync.MainForm;

namespace ScriptSyncPilot.FunscriptEditor.Tests;

[TestClass]
public sealed class PlaybackSpeedCommandTests
{
    [TestMethod]
    public void HsspSetup_StopsOnlyWhenReplacingSetupForTheSameClient()
    {
        using var client = new HandyApiClient("test-only");
        using var anotherClient = new HandyApiClient("test-only");

        Assert.IsFalse(HandyForm.ShouldStopBeforeHsspSetup(null, client));
        Assert.IsTrue(HandyForm.ShouldStopBeforeHsspSetup(client, client));
        Assert.IsFalse(HandyForm.ShouldStopBeforeHsspSetup(anotherClient, client));
        Assert.IsFalse(HandyForm.ShouldStopOnDisconnect(null, client));
        Assert.IsTrue(HandyForm.ShouldStopOnDisconnect(client, client));
    }

    [STATestMethod]
    public void HandySync_SuspendsUnknownAndNonNormalRatesAndResumesAtCurrentPosition()
    {
        using var form = new HandyForm();
        using var session = new MpvSession();
        using var staleSession = new MpvSession();
        using var client = new HandyApiClient("test-only");
        using var handler = new RecordingHandyHandler();
        using var http = new HttpClient(handler) { BaseAddress = new Uri("https://example.invalid/") };
        var originalHttp = (HttpClient)GetPrivateField(client, "_http")!;
        originalHttp.Dispose();
        SetPrivateField(client, "_http", http);
        SetPrivateField(form, "_mpv", session);
        SetPrivateField(form, "_handy", client);
        SetPrivateField(form, "_hsspReady", true);
        ((DeviceSliderControl)GetPrivateField(form, "_offsetSlider")!).SetValues(350, 0);
        _ = form.Handle;

        Observe(session, "pause", "false");
        InvokeHandyTask(form, "OnMpvPauseChangedAsync", session, false).GetAwaiter().GetResult();
        Assert.AreEqual("hssp/stop", handler.Paths.Last());

        foreach (var rate in new[] { "0.5", "2", "3" })
        {
            Observe(session, "speed", rate);
            InvokeHandySpeedUpdate(form, session);
            Assert.AreEqual("hssp/stop", handler.Paths.Last());
            var count = handler.Paths.Count;
            InvokeHandyTask(form, "OnMpvPositionChangedAsync", session, 10.0).GetAwaiter().GetResult();
            InvokeHandyTask(form, "OnSyncTickAsync").GetAwaiter().GetResult();
            Assert.AreEqual(count, handler.Paths.Count);
        }

        Observe(session, "time-pos", "42");
        Observe(session, "speed", "1");
        InvokeHandySpeedUpdate(form, session);
        Assert.AreEqual("hssp/play", handler.Paths.Last());
        using var body = JsonDocument.Parse(handler.Bodies.Last()!);
        Assert.AreEqual(1.0, body.RootElement.GetProperty("playback_rate").GetDouble());
        Assert.AreEqual(42000L, body.RootElement.GetProperty("start_time").GetInt64());

        Observe(session, "pause", "true");
        InvokeHandySpeedUpdate(form, session);
        Assert.AreEqual("hssp/stop", handler.Paths.Last());
        var beforeStaleUpdate = handler.Paths.Count;
        InvokeHandySpeedUpdate(form, staleSession);
        Assert.AreEqual(beforeStaleUpdate, handler.Paths.Count);

        handler.RejectStops = true;
        Observe(session, "speed", "0.5");
        InvokeHandySpeedUpdate(form, session);
        Assert.AreEqual("Device stop failed: pause mpv and disconnect Handy",
            ((Label)GetPrivateField(form, "_status")!).Text);
    }

    [STATestMethod]
    public void HandySync_QueuedSeekRechecksRateBeforeStartingDevice()
    {
        using var form = new HandyForm();
        using var session = new MpvSession();
        using var client = new HandyApiClient("test-only");
        using var handler = new RecordingHandyHandler();
        using var http = new HttpClient(handler) { BaseAddress = new Uri("https://example.invalid/") };
        ((HttpClient)GetPrivateField(client, "_http")!).Dispose();
        SetPrivateField(client, "_http", http);
        SetPrivateField(form, "_mpv", session);
        SetPrivateField(form, "_handy", client);
        SetPrivateField(form, "_hsspReady", true);
        Observe(session, "pause", "false");
        Observe(session, "speed", "1");
        var gate = (SemaphoreSlim)GetPrivateField(form, "_deviceCommandGate")!;
        gate.Wait();
        var seek = InvokeHandyTask(form, "OnMpvPositionChangedAsync", session, 100.0);
        Assert.IsFalse(seek.IsCompleted);
        Observe(session, "speed", "0.5");
        gate.Release();
        var wait = System.Diagnostics.Stopwatch.StartNew();
        while (!seek.IsCompleted && wait.Elapsed < TimeSpan.FromSeconds(5))
            Application.DoEvents();
        Assert.IsTrue(seek.IsCompleted, "Queued seek did not complete after releasing the device gate.");
        seek.GetAwaiter().GetResult();
        Assert.AreEqual(0, handler.Paths.Count);
    }

    private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;

    private static object? GetPrivateField(object target, string name) =>
        target.GetType().GetField(name, PrivateInstance)!.GetValue(target);

    private static void SetPrivateField(object target, string name, object? value) =>
        target.GetType().GetField(name, PrivateInstance)!.SetValue(target, value);

    private static Task InvokeHandyTask(HandyForm form, string method, params object[] arguments) =>
        (Task)typeof(HandyForm).GetMethod(method, PrivateInstance)!.Invoke(form, arguments)!;

    private static void InvokeHandySpeedUpdate(HandyForm form, MpvSession session)
    {
        typeof(HandyForm).GetMethod("OnMpvSpeedChanged", PrivateInstance)!.Invoke(form, new object[] { session });
        Application.DoEvents();
    }

    private static void Observe(MpvSession session, string property, string value) =>
        typeof(MpvSession).GetMethod("HandleLine", PrivateInstance)!.Invoke(session,
            new object[] { $"{{\"event\":\"property-change\",\"name\":\"{property}\",\"data\":{value}}}" });

    private sealed class RecordingHandyHandler : HttpMessageHandler
    {
        public List<string> Paths { get; } = new();
        public List<string?> Bodies { get; } = new();
        public bool RejectStops { get; set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Paths.Add(request.RequestUri!.AbsolutePath.TrimStart('/'));
            Bodies.Add(request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken));
            return new HttpResponseMessage(RejectStops && Paths.Last() == "hssp/stop"
                ? System.Net.HttpStatusCode.ServiceUnavailable : System.Net.HttpStatusCode.OK);
        }
    }

    [STATestMethod]
    public void SpeedObservation_RoundsStoredValueBeforeSpinnerStep()
    {
        using var form = new MainForm();
        using var session = new MpvSession();
        const BindingFlags privateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
        var sessionField = typeof(MainForm).GetField("_mpv", privateInstance)!;
        var speedInput = (NumericUpDown)typeof(MainForm).GetField("_speedInput", privateInstance)!.GetValue(form)!;
        var wheelFilter = (IMessageFilter)typeof(MainForm).GetField("_wheelFilter", privateInstance)!.GetValue(form)!;
        try
        {
            _ = form.Handle;
            sessionField.SetValue(form, session);
            typeof(MainForm).GetMethod("OnMpvSpeedChanged", privateInstance)!.Invoke(form, new object[] { session, 1.234 });
            Application.DoEvents();

            Assert.AreEqual(1.23m, speedInput.Value);
            sessionField.SetValue(form, null);
            speedInput.UpButton();
            Assert.AreEqual(1.28m, speedInput.Value);
        }
        finally
        {
            sessionField.SetValue(form, null);
            Application.RemoveMessageFilter(wheelFilter);
        }
    }

    [TestMethod]
    public async Task SendPlaybackSpeedAsync_ReportsCurrentSessionFailureWithoutThrowing()
    {
        var session = new object();
        var messages = new List<string>();

        await MainForm.SendPlaybackSpeedAsync(
            session,
            static (_, _) => Task.FromException(new IOException("pipe closed")),
            CancellationToken.None,
            capturedSession => ReferenceEquals(session, capturedSession),
            messages.Add);

        CollectionAssert.AreEqual(new[] { "Speed change failed: pipe closed" }, messages);
    }

    [TestMethod]
    public async Task SendPlaybackSpeedAsync_SuppressesFailureAfterSessionReplacement()
    {
        var session = new object();
        var messages = new List<string>();

        await MainForm.SendPlaybackSpeedAsync(
            session,
            static (_, _) => Task.FromException(new IOException("old pipe closed")),
            CancellationToken.None,
            static _ => false,
            messages.Add);

        Assert.AreEqual(0, messages.Count);
    }

    [TestMethod]
    public async Task SendPlaybackSpeedAsync_LateCompletionUsesCapturedSessionAndDoesNotReportStaleFailure()
    {
        var capturedSession = new object();
        var activeSession = capturedSession;
        var writeStarted = new TaskCompletionSource<object>(TaskCreationOptions.RunContinuationsAsynchronously);
        var completeWrite = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var messages = new List<string>();
        var write = MainForm.SendPlaybackSpeedAsync(
            capturedSession,
            async (targetSession, _) =>
            {
                writeStarted.SetResult(targetSession);
                await completeWrite.Task;
                throw new IOException("old pipe closed");
            },
            CancellationToken.None,
            targetSession => ReferenceEquals(activeSession, targetSession),
            messages.Add);

        Assert.AreSame(capturedSession, await writeStarted.Task);
        activeSession = new object();
        completeWrite.SetResult(true);
        await write;

        Assert.AreEqual(0, messages.Count);
    }

    [TestMethod]
    public void ApplySpeedUpdateIfCurrentSession_DropsQueuedUpdateAfterReplacement()
    {
        var oldSession = new object();
        var activeSession = oldSession;
        var displayedSpeed = 1.25;
        Action queuedObservation = () => MainForm.ApplySpeedUpdateIfCurrentSession(
            activeSession,
            oldSession,
            isClosing: false,
            () => displayedSpeed = 0.5);

        activeSession = new object();
        queuedObservation();

        Assert.AreEqual(1.25, displayedSpeed);
    }
}