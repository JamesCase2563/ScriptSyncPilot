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

    [STATestMethod]
    [DataRow("""{"fw_status":0,"fw_version":"4.3.0","hw_model_name":"H01","hw_model_no":0,"hw_model_variant":0,"session_id":"synthetic-private-session"}""",
        """model name "H01", model number 0, model variant 0.""", true)]
    [DataRow("""{"fw_status":2,"fw_version":"3.2.0","hw_model_name":"H01","hw_model_no":1,"hw_model_variant":2}""",
        """model name "H01", model number 1, model variant 2.""", false)]
    [DataRow("""{"fw_status":0,"fw_version":"4.3.0"}""",
        "model name not reported, model number not reported, model variant not reported.", true)]
    [DataRow("""{"fw_status":0,"fw_version":"4.3.0","hw_model_name":null,"hw_model_no":null,"hw_model_variant":null}""",
        "model name not reported, model number not reported, model variant not reported.", true)]
    [DataRow("""{"fw_status":0,"fw_version":"4.3.0","hw_model_name":"H01\r\nforged log entry"}""",
        """model name "H01\r\nforged log entry", model number not reported, model variant not reported.""", true)]
    [DataRow("invalid", "Could not verify device firmware; device controls are disabled:", false)]
    public void ConnectionInfo_LogsHardwareToVisibleAndPersistentLogs(string result, string expected, bool controlsSupported)
    {
        var oldApp = Path.GetFileName(Path.GetDirectoryName(AppLog.FilePath)!);
        AppLog.Init("ScriptSyncPilot-device-info-test-" + Guid.NewGuid().ToString("N"));
        var logPath = AppLog.FilePath;
        try
        {
            var requestCount = 0;
            using var handler = new Handler(request =>
            {
                requestCount++;
                Assert.AreEqual(HttpMethod.Get, request.Method);
                Assert.AreEqual("/api/info", request.RequestUri!.AbsolutePath);
                return Response(result == "invalid" ? "{" : "{\"result\":" + result + "}");
            });
            using var client = new HandyApiClient(Secret, handler, new Uri("https://example.test/api/"));
            using var form = new ScriptSyncPilot.HandySync.MainForm(new AppSettings(), _ => client);
            _ = form.Handle;
            SetField(form, "_handy", client);

            var check = (Task)form.GetType().GetMethod("CheckDeviceFirmwareAsync",
                BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(form, [client])!;
            Complete(check);

            Assert.AreEqual(1, requestCount, "Hardware logging should reuse the firmware info request.");
            Assert.AreEqual(controlsSupported, GetField<bool>(form, "_deviceControlsSupported"));
            var visible = GetField<TextBox>(form, "_log").Text;
            var persistent = File.ReadAllText(logPath);
            foreach (var log in new[] { visible, persistent })
            {
                StringAssert.Contains(log, expected);
                AssertSafe(log);
                Assert.IsFalse(log.Contains("synthetic-private-session", StringComparison.Ordinal));
                Assert.IsFalse(log.Contains("\r\nforged log entry", StringComparison.Ordinal));
                if (result != "invalid")
                {
                    StringAssert.Contains(log, "Device hardware:");
                    StringAssert.Contains(log, controlsSupported ? "Device firmware supported:" : "Device controls unavailable:");
                }
                else
                {
                    Assert.IsFalse(log.Contains("Device hardware:", StringComparison.Ordinal));
                }
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

    [STATestMethod]
    public void ConnectionInfoBoundsAndEscapesRemoteHardwareAndFirmwareText()
    {
        var untrusted = new string('F', 120) + "\r\nforged log entry";
        var response = JsonSerializer.Serialize(new
        {
            result = new
            {
                fw_status = 2,
                fw_version = untrusted,
                hw_model_name = untrusted
            }
        });
        using var handler = new Handler(_ => Response(response));
        using var client = new HandyApiClient(Secret, handler, new Uri("https://example.test/api/"));
        using var form = new ScriptSyncPilot.HandySync.MainForm(new AppSettings(), _ => client);
        _ = form.Handle;
        SetField(form, "_handy", client);

        var check = (Task)form.GetType().GetMethod("CheckDeviceFirmwareAsync",
            BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(form, [client])!;
        Complete(check);

        var log = GetField<TextBox>(form, "_log").Text;
        var bounded = JsonSerializer.Serialize(new string('F', 80) + "…");
        StringAssert.Contains(log, $"model name {bounded}");
        StringAssert.Contains(log, $"firmware {bounded}");
        Assert.IsFalse(log.Contains("\r\nforged log entry", StringComparison.Ordinal));
        Assert.IsFalse(log.Contains(new string('F', 81), StringComparison.Ordinal));
        AssertSafe(log);
        SetField(form, "_handy", null);
    }

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
