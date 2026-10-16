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
    public void PendingStrokeWriteIsCancelledWhenDisconnectBegins()
    {
        using var form = new ScriptSyncPilot.HandySync.MainForm();
        using var handler = new RecordingHandler((_, _, _) => Task.FromResult(JsonResponse("{\"result\":\"ok\"}")));
        using var client = new HandyApiClient("test-key", handler, new Uri("https://example.test/api/"));
        InitializeWritableForm(form, client);
        var slider = (DeviceSliderControl)GetPrivateField(form, "_strokeSlider")!;
        _ = form.Handle;

        slider.SetValues(15, 85);
        InvokePrivate(form, "ScheduleStrokeWrite");
        var disconnect = InvokePrivateTask(form, "DisconnectHandyAsync");
        PumpUntil(() => disconnect.IsCompleted, TimeSpan.FromSeconds(3));
        disconnect.GetAwaiter().GetResult();
        PumpFor(TimeSpan.FromMilliseconds(300));

        Assert.AreEqual(0, handler.Requests.Count(request => request.Path == "slider/stroke"));
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
    public void PollFailuresPreserveValuesAndConnectionThenMarkUnknown()
    {
        using var form = new ScriptSyncPilot.HandySync.MainForm();
        using var handler = new RecordingHandler((_, _, _) => Task.FromResult(JsonResponse(
            "{\"error\":{\"code\":1002,\"name\":\"DeviceTimeout\"}}")));
        using var client = new HandyApiClient("test-key", handler, new Uri("https://example.test/api/"));
        InitializePollingForm(form, client);
        var offset = (DeviceSliderControl)GetPrivateField(form, "_offsetSlider")!;
        var stroke = (DeviceSliderControl)GetPrivateField(form, "_strokeSlider")!;
        offset.SetValues(33, 0);
        stroke.SetValues(20, 80);

        for (var attempt = 0; attempt < 3; attempt++)
            InvokePrivateTask(form, "PollDeviceValuesAsync").GetAwaiter().GetResult();

        Assert.AreSame(client, GetPrivateField(form, "_handy"));
        Assert.AreEqual(33, offset.LowerValue);
        Assert.AreEqual(20, stroke.LowerValue);
        Assert.AreEqual(80, stroke.UpperValue);
        Assert.AreEqual("device values unknown", ((Label)GetPrivateField(form, "_offsetState")!).Text);
        Assert.AreEqual("device values unknown", ((Label)GetPrivateField(form, "_strokeState")!).Text);
        Assert.IsTrue((bool)GetPrivateField(form, "_pollFailureLogged")!);
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
    public void FailedOptionalReadsLeaveControlsUsableAndNoSliderSuppressesStrokeRead()
    {
        using var form = new ScriptSyncPilot.HandySync.MainForm();
        using var handler = new RecordingHandler((request, _, _) => Task.FromResult(request.RequestUri!.AbsolutePath switch
        {
            "/api/capabilities" => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable),
            "/api/settings/slider" => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable),
            "/api/hstp/offset" => JsonResponse("{\"result\":{\"offset\":125}}"),
            "/api/slider/stroke" => JsonResponse("{\"result\":{\"min\":0.2,\"max\":0.8,\"min_absolute\":25,\"max_absolute\":85}}"),
            _ => throw new InvalidOperationException("Unexpected request.")
        }));
        using var client = new HandyApiClient("test-key", handler, new Uri("https://example.test/api/"));
        InitializePollingForm(form, client);

        InvokePrivateTask(form, "ReadAndAdoptDeviceControlsAsync", client).GetAwaiter().GetResult();

        Assert.IsTrue(((DeviceSliderControl)GetPrivateField(form, "_strokeSlider")!).Enabled);
        Assert.AreEqual("applied", ((Label)GetPrivateField(form, "_offsetState")!).Text);
        Assert.AreEqual("applied", ((Label)GetPrivateField(form, "_strokeState")!).Text);
        Assert.AreEqual(string.Empty, ((ToolTip)GetPrivateField(form, "_deviceToolTip")!).GetToolTip((Control)GetPrivateField(form, "_strokeSlider")!));

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

        Assert.IsFalse(((DeviceSliderControl)GetPrivateField(noSliderForm, "_strokeSlider")!).Enabled);
        Assert.IsFalse(noSliderHandler.Requests.Any(request => request.Path == "slider/stroke"));
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

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var count = Requests.Count + 1;
            var body = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
            Requests.Add(new RequestRecord(request.Method.Method, request.RequestUri!.AbsolutePath.TrimStart('/').Replace("api/", "", StringComparison.Ordinal), body));
            return await respond(request, count, cancellationToken);
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
+            using var parsed = JsonDocument.Parse(body);
+            var min = parsed.RootElement.GetProperty("min").GetDouble();
+            var max = parsed.RootElement.GetProperty("max").GetDouble();
+            return JsonResponse(JsonSerializer.Serialize(new
+            {
+                result = new { min, max, min_absolute = min * 100, max_absolute = max * 100 }
+            }));
+        }
+    }
+
+    private sealed record RequestRecord(string Method, string Path, string? Body);
+}
