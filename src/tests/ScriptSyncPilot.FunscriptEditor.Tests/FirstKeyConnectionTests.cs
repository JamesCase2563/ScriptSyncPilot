using System.Diagnostics;
using System.Net;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ScriptSyncPilot.Common;
using ScriptSyncPilot.HandySync;

namespace ScriptSyncPilot.FunscriptEditor.Tests;

[TestClass]
public sealed class FirstKeyConnectionTests
{
    [STATestMethod]
    public void LoadedSettings_ConnectWithFirstSavedKey()
    {
        var settings = new AppSettings();
        settings.AddConnectionKey("First", "synthetic-first");
        settings.AddConnectionKey("Second", "synthetic-second");
        var loaded = AppSettings.Deserialize(settings.Serialize(), _ => { });

        AssertConnection(loaded, "synthetic-first");
    }

    [STATestMethod]
    public void DeletedFirst_ConnectsWithPromotedKeyAfterReload()
    {
        var settings = new AppSettings();
        var first = settings.AddConnectionKey("First", "synthetic-first");
        settings.AddConnectionKey("Second", "synthetic-second");
        settings.DeleteConnectionKey(first);

        AssertConnection(AppSettings.Deserialize(settings.Serialize(), _ => { }), "synthetic-second");
    }

    [STATestMethod]
    public void EditedInput_RemainsConnectionSourceAndUpdatesOnlyFirstOnSave()
    {
        var settings = new AppSettings();
        var first = settings.AddConnectionKey("First", "synthetic-first");
        var second = settings.AddConnectionKey("Second", "synthetic-second");

        AssertConnection(settings, "synthetic-first", "  synthetic-edited  ");
        settings.ConnectionKey = "synthetic-edited";
        var restored = AppSettings.Deserialize(settings.Serialize(), _ => { });
        Assert.AreEqual(first, restored.ConnectionKeys[0].Id);
        Assert.AreEqual(second, restored.ConnectionKeys[1].Id);
        Assert.AreEqual("synthetic-edited", restored.GetFirstConnectionKey());
        restored.DeleteConnectionKey(first);
        Assert.AreEqual("synthetic-second", restored.GetFirstConnectionKey());
    }

    [STATestMethod]
    public void NoSavedKey_AllowsManualEntry()
    {
        AssertConnection(new AppSettings(), "", "synthetic-manual");
    }

    [STATestMethod]
    public void DeletedLast_LeavesFormWithoutAnActiveKey()
    {
        var settings = new AppSettings();
        var id = settings.AddConnectionKey("Only", "synthetic-only");
        settings.DeleteConnectionKey(id);
        var loaded = AppSettings.Deserialize(settings.Serialize(), _ => { });
        using var form = new ScriptSyncPilot.HandySync.MainForm(loaded,
            _ => throw new AssertFailedException("No connection should be created."));
        Assert.AreEqual("", GetField<TextBox>(form, "_connectionKeyBox").Text);
    }

    private static void AssertConnection(AppSettings settings, string initialKey, string? editedInput = null)
    {
        using var handler = new ConnectionProbeHandler();
        string? suppliedKey = null;
        using var form = new ScriptSyncPilot.HandySync.MainForm(settings, key =>
        {
            suppliedKey = key;
            return new HandyApiClient(key, handler, new Uri("https://example.test/api/"));
        });
        _ = form.Handle;
        var input = GetField<TextBox>(form, "_connectionKeyBox");
        Assert.AreEqual(initialKey, input.Text);
        if (editedInput is not null) input.Text = editedInput;
        typeof(ScriptSyncPilot.HandySync.MainForm).GetField("_script", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(form, new Funscript());
        var connection = (Task)typeof(ScriptSyncPilot.HandySync.MainForm)
            .GetMethod("ConnectHandyAsync", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(form, null)!;
        var wait = Stopwatch.StartNew();
        while (!connection.IsCompleted && wait.Elapsed < TimeSpan.FromSeconds(5))
        {
            Application.DoEvents();
            Thread.Sleep(1);
        }
        Assert.IsTrue(connection.IsCompleted, "Connection probe timed out.");
        connection.GetAwaiter().GetResult();
        Application.DoEvents();

        var expectedKey = (editedInput ?? initialKey).Trim();
        Assert.AreEqual(expectedKey, suppliedKey);
        Assert.AreEqual(expectedKey, handler.ConnectionKey);
        Assert.AreEqual(1, handler.RequestCount);
        Assert.IsTrue(GetField<Button>(form, "_connectButton").Enabled);
    }

    private static T GetField<T>(object instance, string name) =>
        (T)instance.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(instance)!;

    private sealed class ConnectionProbeHandler : HttpMessageHandler
    {
        public string? ConnectionKey { get; private set; }
        public int RequestCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestCount++;
            Assert.AreEqual(HttpMethod.Get, request.Method);
            Assert.AreEqual("/api/connected", request.RequestUri!.AbsolutePath);
            ConnectionKey = request.Headers.GetValues("X-Connection-Key").Single();
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{"result":{"connected":false}}""")
            });
        }
    }
}
