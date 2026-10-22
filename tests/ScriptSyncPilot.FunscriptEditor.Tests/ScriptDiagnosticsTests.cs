using System.Diagnostics;
using System.Net;
using System.Reflection;
using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ScriptSyncPilot.Common;
using ScriptSyncPilot.HandySync;

namespace ScriptSyncPilot.FunscriptEditor.Tests;

[TestClass]
[DoNotParallelize]
public sealed class ScriptDiagnosticsTests
{
    private const string Url = "https://example.test/private-script.csv?token=synthetic-bearer";
    private const string Secret = "synthetic-connection-secret";

    [DataTestMethod]
    [DataRow("io")]
    [DataRow("cancel")]
    public async Task TransportFailure_DoesNotRetainSensitiveException(string failure)
    {
        using var handler = new Handler(_ => throw (failure == "io"
            ? new IOException($"{Url} {Secret}")
            : new TaskCanceledException($"{Url} {Secret}", new Exception(Secret))));
        using var client = new HandyApiClient(Secret, handler);
        Exception error;
        if (failure == "cancel")
            error = await Assert.ThrowsExceptionAsync<TaskCanceledException>(
                () => client.HsspSetupAsync(Url, CancellationToken.None));
        else
            error = await Assert.ThrowsExceptionAsync<HandyApiException>(
                () => client.HsspSetupAsync(Url, CancellationToken.None));
        AssertSafe(error.ToString());
        Assert.IsNull(error.InnerException);
        StringAssert.Contains(error.Message, "hssp/setup");
    }

    [DataTestMethod]
    [DataRow("upload")]
    [DataRow("setup")]
    [DataRow("sync")]
    public async Task TransportExceptions_AreReplacedWithoutUnsafeInnerDetails(string operation)
    {
        using var device = new Handler(_ => throw new HttpRequestException($"{Url} {Secret}"));
        using var hosting = new Handler(_ => throw new HttpRequestException($"{Url} {Secret}"));
        using var client = new HandyApiClient(Secret, device, hostingHandler: hosting);
        var error = await Assert.ThrowsExceptionAsync<HandyApiException>(() => operation switch
        {
            "upload" => client.UploadScriptCsvAsync("0,0", CancellationToken.None),
            "setup" => client.HsspSetupAsync(Url, CancellationToken.None),
            _ => client.HsspSyncTimeAsync(0, 0)
        });
        AssertSafe(error.ToString());
        Assert.IsNull(error.InnerException);
        StringAssert.Contains(error.Message, "transport failed");
    }

    [DataTestMethod]
    [DataRow("null")]
    [DataRow("[]")]
    [DataRow("{")]
    [DataRow("""{"url":42}""")]
    [DataRow("""{"url":""}""")]
    public async Task InvalidUploadResponses_AreExplicitAndSafe(string body)
    {
        using var device = new Handler(_ => Response("{}"));
        using var hosting = new Handler(_ => Response(body));
        using var client = new HandyApiClient(Secret, device, hostingHandler: hosting);
        var error = await Assert.ThrowsExceptionAsync<HandyApiException>(
            () => client.UploadScriptCsvAsync("0,0", CancellationToken.None));
        AssertSafe(error.ToString());
        Assert.IsNull(error.InnerException);
    }

    [TestMethod]
    public async Task UntrustedDeviceNameAndHttpReason_AreNotDiagnostics()
    {
        using var device = new Handler(request => request.RequestUri!.AbsolutePath.EndsWith("hssp/setup")
            ? Response(JsonSerializer.Serialize(new { error = new { code = 1002, name = Url, message = Secret } }))
            : new HttpResponseMessage(HttpStatusCode.BadGateway) { ReasonPhrase = $"{Url} {Secret}" });
        using var client = new HandyApiClient(Secret, device);
        var setup = await Assert.ThrowsExceptionAsync<HandyApiException>(
            () => client.HsspSetupAsync(Url, CancellationToken.None));
        Assert.AreEqual(1002, setup.Code);
        Assert.IsNull(setup.Name);
        AssertSafe(setup.ToString());
        var sync = await Assert.ThrowsExceptionAsync<HandyApiException>(() => client.HsspSyncTimeAsync(0, 0));
        StringAssert.Contains(sync.Message, "502");
        AssertSafe(sync.ToString());
    }

    [DataTestMethod]
    [DataRow("{")]
    [DataRow("[]")]
    public async Task InvalidSetupResponse_IsNotReportedAsSuccess(string body)
    {
        using var device = new Handler(_ => Response(body));
        using var client = new HandyApiClient(Secret, device);
        var error = await Assert.ThrowsExceptionAsync<HandyApiException>(
            () => client.HsspSetupAsync(Url, CancellationToken.None));
        AssertSafe(error.ToString());
    }

    [STATestMethod]
    [DataRow("success")]
    [DataRow("upload-http")]
    [DataRow("upload-malformed")]
    [DataRow("setup-device")]
    [DataRow("setup-transport")]
    public void ActualSetupAndReplacement_KeepVisibleAndPersistentLogsSecretFree(string scenario)
    {
        var oldApp = Path.GetFileName(Path.GetDirectoryName(AppLog.FilePath)!);
        AppLog.Init("ScriptSyncPilot-diagnostics-test-" + Guid.NewGuid().ToString("N"));
        var logPath = AppLog.FilePath;
        try
        {
            var replacing = false;
            using var device = new Handler(request =>
            {
                if (replacing && request.RequestUri!.AbsolutePath.EndsWith("hssp/setup"))
                {
                    if (scenario == "setup-transport")
                        throw new HttpRequestException($"{Url} {Secret}");
                    if (scenario == "setup-device")
                        return Response(JsonSerializer.Serialize(new { error = new { code = 1002, name = Url, message = Secret } }));
                }
                return Response("{}");
            });
            using var hosting = new Handler(_ =>
            {
                if (replacing && scenario == "upload-http")
                    return new HttpResponseMessage(HttpStatusCode.BadGateway)
                    {
                        ReasonPhrase = Url,
                        Content = new StringContent($"{Url} {Secret}")
                    };
                if (replacing && scenario == "upload-malformed")
                    return Response($"{{\"{Url} {Secret}");
                return Response(JsonSerializer.Serialize(new { url = Url }));
            });
            using var client = new HandyApiClient(Secret, device, hostingHandler: hosting);
            using var form = new ScriptSyncPilot.HandySync.MainForm(new AppSettings(), _ => client);
            _ = form.Handle;
            SetField(form, "_handy", client);
            var script = new Funscript();
            SetField(form, "_script", script);
            InvokeInstall(form, client);
            Assert.IsTrue(ReferenceEquals(client, GetField<object>(form, "_installedScriptClient")));

            SetField(form, "_script", new Funscript());
            SetField(form, "_scriptVersion", 1L);
            replacing = true;
            var replacement = (Task)form.GetType().GetMethod("ReSetupScriptAsync",
                BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(form, null)!;
            Complete(replacement);
            var visible = GetField<TextBox>(form, "_log").Text;
            var persistent = File.ReadAllText(logPath);
            AssertSafe(visible);
            AssertSafe(persistent);
            StringAssert.Contains(visible, "Script upload complete; configuring HSSP.");
            StringAssert.Contains(persistent, "HSSP setup complete.");
            if (scenario != "success")
            {
                StringAssert.Contains(visible, "Could not push new script to device:");
                StringAssert.Contains(persistent, "Could not push new script to device:");
            }
            SetField(form, "_handy", null);
        }
        finally
        {
            AppLog.Init(oldApp);
            if (File.Exists(logPath)) File.Delete(logPath);
            var directory = Path.GetDirectoryName(logPath)!;
            if (Directory.Exists(directory)) Directory.Delete(directory);
        }
    }

    private static void InvokeInstall(object form, HandyApiClient client)
    {
        var task = (Task)form.GetType().GetMethod("EnsureCurrentScriptInstalledAsync",
            BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(form, [client, CancellationToken.None])!;
        Complete(task);
    }

    private static void Complete(Task task)
    {
        var timer = Stopwatch.StartNew();
        while (!task.IsCompleted && timer.Elapsed < TimeSpan.FromSeconds(5))
        {
            Application.DoEvents();
            Thread.Sleep(1);
        }
        Assert.IsTrue(task.IsCompleted);
        task.GetAwaiter().GetResult();
        Application.DoEvents();
    }

    private static void AssertSafe(string text)
    {
        Assert.IsFalse(text.Contains(Url, StringComparison.Ordinal));
        Assert.IsFalse(text.Contains("synthetic-bearer", StringComparison.Ordinal));
        Assert.IsFalse(text.Contains(Secret, StringComparison.Ordinal));
    }

    private static T GetField<T>(object instance, string name) =>
        (T)instance.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(instance)!;

    private static void SetField(object instance, string name, object? value) =>
        instance.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(instance, value);

    private static HttpResponseMessage Response(string body) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(body)
    };

    private sealed class Handler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(respond(request));
    }
}
