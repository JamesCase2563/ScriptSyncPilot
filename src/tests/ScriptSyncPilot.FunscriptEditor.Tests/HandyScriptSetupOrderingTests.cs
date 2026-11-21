using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Reflection;
using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ScriptSyncPilot.Common;
using ScriptSyncPilot.HandySync;
using HandyForm = ScriptSyncPilot.HandySync.MainForm;

namespace ScriptSyncPilot.FunscriptEditor.Tests;

[TestClass]
public sealed class HandyScriptSetupOrderingTests
{
    private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;

    [STATestMethod]
    public void ScriptSelectionChangesDuringUpload_OnlySetsUpLatestSelectedScript()
    {
        var firstUploadStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseFirstUpload = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var trace = new ConcurrentQueue<string>();
        var uploads = new ConcurrentQueue<string>();
        var setups = new ConcurrentQueue<string>();
        var uploadCount = 0;
        using var device = new Handler(async (request, cancellationToken) =>
        {
            var path = request.RequestUri!.AbsolutePath;
            if (path.EndsWith("/hssp/stop", StringComparison.Ordinal))
            {
                trace.Enqueue("stop");
                return JsonResponse("{\"result\":\"ok\"}");
            }

            Assert.IsTrue(path.EndsWith("/hssp/setup", StringComparison.Ordinal), $"Unexpected request {path}.");
            using var body = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(cancellationToken));
            var url = body.RootElement.GetProperty("url").GetString()!;
            setups.Enqueue(url);
            trace.Enqueue($"setup:{url}");
            return JsonResponse("{\"result\":\"ok\"}");
        });
        using var hosting = new Handler(async (request, cancellationToken) =>
        {
            var multipart = (MultipartFormDataContent)request.Content!;
            var csv = await multipart.Single().ReadAsStringAsync(cancellationToken);
            uploads.Enqueue(csv);
            trace.Enqueue("upload");
            var number = Interlocked.Increment(ref uploadCount);
            if (number == 1)
            {
                firstUploadStarted.TrySetResult();
                await releaseFirstUpload.Task.WaitAsync(cancellationToken);
            }
            return JsonResponse($"{{\"url\":\"https://example.test/script-{number}.csv\"}}");
        });
        using var client = new HandyApiClient("test-key", device, new Uri("https://example.test/api/"), hostingHandler: hosting);
        var firstScriptPath = WriteScript(10, 20);
        var selectedScriptPath = WriteScript(80, 90);
        try
        {
            using var form = CreateReadyForm(client, firstScriptPath);
            var setup = InvokeTask(form, "ReSetupScriptAsync");
            PumpUntil(() => firstUploadStarted.Task.IsCompleted, TimeSpan.FromSeconds(3));

            Invoke(form, "LoadScript", selectedScriptPath);
            releaseFirstUpload.TrySetResult();
            PumpUntil(() => setup.IsCompleted, TimeSpan.FromSeconds(5));
            setup.GetAwaiter().GetResult();

            Assert.AreEqual(2, uploads.Count);
            Assert.AreNotEqual(uploads.ElementAt(0), uploads.ElementAt(1), "The selected scripts must have distinct CSV payloads.");
            CollectionAssert.AreEqual(new[] { "https://example.test/script-2.csv" }, setups.ToArray());
            Assert.AreEqual(2L, GetField<long>(form, "_installedScriptVersion"));
            Assert.AreSame(client, GetField<object>(form, "_installedScriptClient"));
            Assert.IsTrue(GetField<bool>(form, "_hsspReady"));
            CollectionAssert.AreEqual(new[] { "stop", "upload", "stop", "upload", "setup:https://example.test/script-2.csv" }, trace.ToArray());
        }
        finally
        {
            DeleteScript(firstScriptPath);
            DeleteScript(selectedScriptPath);
        }
    }

    [STATestMethod]
    public void ScriptSelectionChangesDuringSetup_StopsOldSetupBeforeInstallingLatestScript()
    {
        var firstSetupStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseFirstSetup = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var trace = new ConcurrentQueue<string>();
        var uploads = new ConcurrentQueue<string>();
        var setups = new ConcurrentQueue<string>();
        var uploadCount = 0;
        var setupCount = 0;
        using var device = new Handler(async (request, cancellationToken) =>
        {
            var path = request.RequestUri!.AbsolutePath;
            if (path.EndsWith("/hssp/stop", StringComparison.Ordinal))
            {
                trace.Enqueue("stop");
                return JsonResponse("{\"result\":\"ok\"}");
            }

            Assert.IsTrue(path.EndsWith("/hssp/setup", StringComparison.Ordinal), $"Unexpected request {path}.");
            using var body = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(cancellationToken));
            var url = body.RootElement.GetProperty("url").GetString()!;
            setups.Enqueue(url);
            trace.Enqueue($"setup:{url}");
            if (Interlocked.Increment(ref setupCount) == 1)
            {
                firstSetupStarted.TrySetResult();
                await releaseFirstSetup.Task.WaitAsync(cancellationToken);
            }
            return JsonResponse("{\"result\":\"ok\"}");
        });
        using var hosting = new Handler(async (request, cancellationToken) =>
        {
            var multipart = (MultipartFormDataContent)request.Content!;
            var csv = await multipart.Single().ReadAsStringAsync(cancellationToken);
            uploads.Enqueue(csv);
            trace.Enqueue("upload");
            var number = Interlocked.Increment(ref uploadCount);
            return JsonResponse($"{{\"url\":\"https://example.test/script-{number}.csv\"}}");
        });
        using var client = new HandyApiClient("test-key", device, new Uri("https://example.test/api/"), hostingHandler: hosting);
        var firstScriptPath = WriteScript(10, 20);
        var selectedScriptPath = WriteScript(80, 90);
        try
        {
            using var form = CreateReadyForm(client, firstScriptPath);
            var setup = InvokeTask(form, "ReSetupScriptAsync");
            PumpUntil(() => firstSetupStarted.Task.IsCompleted, TimeSpan.FromSeconds(3));

            Invoke(form, "LoadScript", selectedScriptPath);
            releaseFirstSetup.TrySetResult();
            PumpUntil(() => setup.IsCompleted, TimeSpan.FromSeconds(5));
            setup.GetAwaiter().GetResult();

            Assert.AreEqual(2, uploads.Count);
            Assert.AreNotEqual(uploads.ElementAt(0), uploads.ElementAt(1), "The selected scripts must have distinct CSV payloads.");
            CollectionAssert.AreEqual(
                new[] { "https://example.test/script-1.csv", "https://example.test/script-2.csv" },
                setups.ToArray());
            Assert.AreEqual(2L, GetField<long>(form, "_installedScriptVersion"));
            Assert.AreSame(client, GetField<object>(form, "_installedScriptClient"));
            Assert.IsTrue(GetField<bool>(form, "_hsspReady"));
            CollectionAssert.AreEqual(new[]
            {
                "stop", "upload", "setup:https://example.test/script-1.csv",
                "stop", "upload", "setup:https://example.test/script-2.csv"
            }, trace.ToArray());
        }
        finally
        {
            DeleteScript(firstScriptPath);
            DeleteScript(selectedScriptPath);
        }
    }

    private static HandyForm CreateReadyForm(HandyApiClient client, string firstScriptPath)
    {
        var form = new HandyForm(new AppSettings(), _ => client, _ => { }, _ => { });
        _ = form.Handle;
        SetField(form, "_handy", client);
        SetField(form, "_installedScriptClient", client);
        SetField(form, "_installedScriptVersion", 0L);
        SetField(form, "_hsspReady", false);
        Invoke(form, "LoadScript", firstScriptPath);
        Assert.AreEqual(1L, GetField<long>(form, "_scriptVersion"));
        SetField(form, "_hsspReady", true);
        return form;
    }

    private static string WriteScript(int firstPosition, int lastPosition)
    {
        var path = Path.Combine(Path.GetTempPath(), $"ScriptSyncPilot-setup-order-{Guid.NewGuid():N}.funscript");
        File.WriteAllText(path,
            $"{{\"version\":\"1.0\",\"actions\":[{{\"at\":0,\"pos\":{firstPosition}}},{{\"at\":1000,\"pos\":{lastPosition}}}]}}");
        return path;
    }

    private static void DeleteScript(string path)
    {
        if (File.Exists(path)) File.Delete(path);
    }

    private static void PumpUntil(Func<bool> condition, TimeSpan timeout)
    {
        var timer = Stopwatch.StartNew();
        while (!condition() && timer.Elapsed < timeout)
        {
            Application.DoEvents();
            Thread.Sleep(5);
        }
        Assert.IsTrue(condition(), "Timed out waiting for the fake transport scenario.");
    }

    private static T GetField<T>(object target, string name) =>
        (T)target.GetType().GetField(name, PrivateInstance)!.GetValue(target)!;

    private static void SetField(object target, string name, object? value) =>
        target.GetType().GetField(name, PrivateInstance)!.SetValue(target, value);

    private static void Invoke(object target, string method, params object[] arguments) =>
        target.GetType().GetMethod(method, PrivateInstance)!.Invoke(target, arguments);

    private static Task InvokeTask(object target, string method, params object[] arguments) =>
        (Task)target.GetType().GetMethod(method, PrivateInstance)!.Invoke(target, arguments)!;

    private static HttpResponseMessage JsonResponse(string json) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
    };

    private sealed class Handler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> respond)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            respond(request, cancellationToken);
    }
}
