using System.Reflection;
using System.Net;
using System.IO.Pipes;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ScriptSyncPilot.Common;
using ScriptSyncPilot.HandySync;
using System.Windows.Forms;

namespace ScriptSyncPilot.FunscriptEditor.Tests;

[TestClass]
public sealed class HandySyncErrorReportingTests
{
    private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;

    [STATestMethod]
    [DataRow("", "Connection key required", "Cannot connect to Handy: enter a connection key first.")]
    [DataRow("synthetic-key", "Funscript required", "Cannot connect to Handy: load a funscript first.")]
    public void ConnectPrerequisiteFailureIsReportedInline(string key, string expectedStatus, string expectedLog)
    {
        var createClientCalls = 0;
        using var form = new ScriptSyncPilot.HandySync.MainForm(new AppSettings { ConnectionKey = key }, _ =>
        {
            createClientCalls++;
            throw new AssertFailedException("A client must not be created for invalid prerequisites.");
        });
        _ = form.Handle;

        InvokeAndPump(form, "ConnectHandyAsync");

        Assert.AreEqual(0, createClientCalls);
        Assert.AreEqual(expectedStatus, GetField<Label>(form, "_status").Text);
        StringAssert.Contains(GetField<TextBox>(form, "_log").Text, expectedLog);
    }

    [STATestMethod]
    public void FailedConnectRetainsErrorStatusAfterDisconnectCleanup()
    {
        using var handler = new ServiceUnavailableHandler();
        using var client = new HandyApiClient("synthetic-key", handler, new Uri("https://example.test/api/"));
        using var form = new ScriptSyncPilot.HandySync.MainForm(new AppSettings { ConnectionKey = "synthetic-key" }, _ => client);
        SetField(form, "_script", new Funscript());
        _ = form.Handle;

        InvokeAndPump(form, "ConnectHandyAsync");

        Assert.AreEqual("Handy error; see log", GetField<Label>(form, "_status").Text);
        StringAssert.Contains(GetField<TextBox>(form, "_log").Text, "Handy error: connected failed: 503.");
        Assert.IsNull(GetField<object?>(form, "_handy"));
    }

    [STATestMethod]
    public void InvalidScriptFailureKeepsCurrentScriptAndDoesNotLogLocalPath()
    {
        var path = Path.Combine(Path.GetTempPath(), $"scriptsync-invalid-{Guid.NewGuid():N}.funscript");
        File.WriteAllText(path, "{invalid json");
        try
        {
            using var form = new ScriptSyncPilot.HandySync.MainForm(new AppSettings(), _ => throw new AssertFailedException("No client expected."));
            var currentScript = new Funscript();
            SetField(form, "_script", currentScript);
            _ = form.Handle;

            typeof(ScriptSyncPilot.HandySync.MainForm).GetMethod("LoadScript", PrivateInstance)!.Invoke(form, [path]);
            Application.DoEvents();

            Assert.AreSame(currentScript, GetField<Funscript>(form, "_script"));
            var log = GetField<TextBox>(form, "_log").Text;
            StringAssert.Contains(log, "Could not load script. Check that the file is accessible and contains valid funscript data.");
            Assert.IsFalse(log.Contains(path, StringComparison.Ordinal));
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [STATestMethod]
    public void FailedMpvAttachLeavesActionableStatusAfterCleanup()
    {
        using var form = new ScriptSyncPilot.HandySync.MainForm(new AppSettings { MpvIpcPipe = "" }, _ => throw new AssertFailedException("No Handy client expected."));
        SetField(form, "_script", new Funscript());
        _ = form.Handle;

        InvokeAndPump(form, "AttachMpvAsync");
        Application.DoEvents();

        Assert.AreEqual("mpv attach failed; see log", GetField<Label>(form, "_status").Text);
        StringAssert.Contains(GetField<TextBox>(form, "_log").Text,
            "Could not attach to mpv; check that it is running and the configured IPC pipe is available.");
        Assert.IsNull(GetField<object?>(form, "_mpv"));
    }

    [STATestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void FailedMpvSeekLogsFixedMessageWithoutPipePath(bool pipeAlreadyDisconnected)
    {
        var pipeName = $"scriptsync-seek-private-{Guid.NewGuid():N}";
        var pipePath = @"\\.\pipe\" + pipeName;
        using var server = pipeAlreadyDisconnected ? null : new NamedPipeServerStream(pipeName, PipeDirection.InOut, 1,
            PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
        using var pipe = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
        if (server is not null)
        {
            var connection = server.WaitForConnectionAsync();
            pipe.Connect(1000);
            connection.GetAwaiter().GetResult();
        }
        using var session = new MpvSession();
        SetField(session, "_pipe", pipe);
        using var form = new ScriptSyncPilot.HandySync.MainForm(new AppSettings { MpvIpcPipe = pipePath },
            _ => throw new AssertFailedException("No Handy client expected."));
        SetField(form, "_mpv", session);
        _ = form.Handle;
        if (server is not null) server.Dispose();
        Assert.AreEqual(!pipeAlreadyDisconnected, pipe.IsConnected,
            "The test must exercise either a connected pipe whose write fails or a pipe already disconnected before sending.");

        InvokeAndPump(form, "SeekMpvAsync", 12.5);

        const string expectedMessage = "Could not seek mpv video; check that the player is still attached.";
        var log = GetField<TextBox>(form, "_log").Text;
        StringAssert.Matches(log, new Regex(@"\A\[\d{2}:\d{2}:\d{2}\] " + Regex.Escape(expectedMessage) + @"\r?\n\z"));
        Assert.IsFalse(log.Contains(pipePath, StringComparison.Ordinal));
        Assert.IsFalse(log.Contains(pipeName, StringComparison.Ordinal));
    }

    [STATestMethod]
    [DataRow(false, "play", false)]
    [DataRow(true, "pause", false)]
    [DataRow(false, "play", true)]
    [DataRow(true, "pause", true)]
    public void FailedMpvPlayPauseLogsFixedMessageAndUnknownPlayerStatus(bool paused, string action, bool pipeAlreadyDisconnected)
    {
        var pipeName = $"scriptsync-transport-private-{Guid.NewGuid():N}";
        var pipePath = @"\\.\pipe\" + pipeName;
        using var server = pipeAlreadyDisconnected ? null : new NamedPipeServerStream(pipeName, PipeDirection.InOut, 1,
            PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
        using var pipe = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
        if (server is not null)
        {
            var connection = server.WaitForConnectionAsync();
            pipe.Connect(1000);
            connection.GetAwaiter().GetResult();
        }
        using var session = new MpvSession();
        SetField(session, "_pipe", pipe);
        typeof(MpvSession).GetProperty(nameof(MpvSession.IsPaused))!.SetValue(session, !paused);
        using var form = new ScriptSyncPilot.HandySync.MainForm(new AppSettings { MpvIpcPipe = pipePath },
            _ => throw new AssertFailedException("No Handy client expected."));
        SetField(form, "_mpv", session);
        SetField(form, "_mpvAttached", true);
        _ = form.Handle;
        if (server is not null) server.Dispose();
        Assert.AreEqual(!pipeAlreadyDisconnected, pipe.IsConnected,
            "The test must exercise either a connected pipe whose write fails or a pipe already disconnected before sending.");

        InvokeAndPump(form, "SetVideoPausedAsync", paused);

        var expectedMessage = $"Could not {action} mpv video; the player may have disconnected.";
        var log = GetField<TextBox>(form, "_log").Text;
        StringAssert.Matches(log, new Regex(@"\A\[\d{2}:\d{2}:\d{2}\] " + Regex.Escape(expectedMessage) + @"\r?\n\z"));
        Assert.IsFalse(log.Contains(pipePath, StringComparison.Ordinal));
        Assert.IsFalse(log.Contains(pipeName, StringComparison.Ordinal));
        Assert.AreEqual($"mpv {action} failed; player state may be unknown", GetField<Label>(form, "_status").Text);
        Assert.AreEqual(!paused, session.IsPaused, "A failed command must not be treated as a confirmed state change.");
        Assert.IsFalse(GetField<bool>(form, "_videoTransportPending"));
        Assert.AreEqual(!paused, GetField<Button>(form, "_playVideoButton").Enabled);
        Assert.AreEqual(paused, GetField<Button>(form, "_pauseVideoButton").Enabled);
    }

    [STATestMethod]
    public void StartupSettingsErrorIsVisibleAndDefaultsAreNotSavedOnClose()
    {
        const string safeError = "Settings could not be read. The existing file has not been changed.";
        var saveCalls = 0;
        using var form = new ScriptSyncPilot.HandySync.MainForm(new AppSettings(), _ => throw new AssertFailedException("No client expected."),
            _ => saveCalls++, settingsLoadError: safeError);
        _ = form.Handle;

        typeof(Form).GetMethod("OnShown", PrivateInstance)!.Invoke(form, [EventArgs.Empty]);
        Application.DoEvents();
        Assert.AreEqual("Settings not loaded; see log", GetField<Label>(form, "_status").Text);
        StringAssert.Contains(GetField<TextBox>(form, "_log").Text, safeError);

        var closing = new FormClosingEventArgs(CloseReason.UserClosing, false);
        typeof(Form).GetMethod("OnFormClosing", PrivateInstance)!.Invoke(form, [closing]);
        Application.DoEvents();

        Assert.AreEqual(0, saveCalls, "Fallback defaults must not overwrite unreadable settings.");
    }

    [STATestMethod]
    public void LegacyPlaintextKeyReentryIsShownInlineWithoutLoggingTheKey()
    {
        const string plaintextKey = "synthetic-legacy-plaintext-key";
        var settings = AppSettings.Deserialize("{\"ConnectionKey\":\"" + plaintextKey + "\"}");
        using var form = new ScriptSyncPilot.HandySync.MainForm(settings, _ => throw new AssertFailedException("No client expected."));
        _ = form.Handle;

        typeof(Form).GetMethod("OnShown", PrivateInstance)!.Invoke(form, [EventArgs.Empty]);
        Application.DoEvents();

        Assert.AreEqual("Connection key required", GetField<Label>(form, "_status").Text);
        StringAssert.Contains(GetField<TextBox>(form, "_log").Text, "Please re-enter your Handy connection key.");
        Assert.IsFalse(GetField<TextBox>(form, "_log").Text.Contains(plaintextKey, StringComparison.Ordinal));
        Assert.AreEqual(string.Empty, GetField<TextBox>(form, "_connectionKeyBox").Text);
    }

    private static void InvokeAndPump(Form form, string method, params object?[] arguments)
    {
        var task = (Task)typeof(ScriptSyncPilot.HandySync.MainForm).GetMethod(method, PrivateInstance)!.Invoke(form, arguments)!;
        var timer = System.Diagnostics.Stopwatch.StartNew();
        while (!task.IsCompleted && timer.Elapsed < TimeSpan.FromSeconds(5))
        {
            Application.DoEvents();
            Thread.Sleep(1);
        }
        Assert.IsTrue(task.IsCompleted, $"{method} did not complete within the test bound.");
        task.GetAwaiter().GetResult();
        Application.DoEvents();
    }

    private static T GetField<T>(object target, string name) =>
        (T)target.GetType().GetField(name, PrivateInstance)!.GetValue(target)!;

    private static void SetField(object target, string name, object? value) =>
        target.GetType().GetField(name, PrivateInstance)!.SetValue(target, value);

    private sealed class ServiceUnavailableHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
    }
}
