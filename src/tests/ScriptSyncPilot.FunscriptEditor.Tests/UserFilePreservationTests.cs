using System.Reflection;
using System.Windows.Forms;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ScriptSyncPilot.Common;
using ScriptSyncPilot.FunscriptEditor;

namespace ScriptSyncPilot.FunscriptEditor.Tests;

[TestClass]
public sealed class UserFilePreservationTests
{
    private static readonly BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;

    [STATestMethod]
    public void CancelDiscardPrompt_LeavesExistingScriptFileAndDirtyEditUntouched()
    {
        const string originalJson = "{\"version\":\"1.0\",\"metadata\":{\"title\":\"keep\"},\"actions\":[{\"at\":0,\"pos\":10}]}";
        var path = Path.Combine(Path.GetTempPath(), $"ScriptSyncPilot-cancel-{Guid.NewGuid():N}.funscript");
        File.WriteAllText(path, originalJson);
        var originalBytes = File.ReadAllBytes(path);
        var script = Funscript.Load(path);
        var confirmationCount = 0;
        using var form = new MainForm(new EditorSettings(), () =>
        {
            confirmationCount++;
            return false;
        });

        try
        {
            SetField(form, "_script", script);
            SetField(form, "_scriptPath", path);
            var timeline = GetField<TimelineControl>(form, "_timeline");
            timeline.LoadScript(script);
            Assert.IsTrue(timeline.ReplaceActions(
                [new FunscriptAction(0, 10), new FunscriptAction(1000, 90)], 1000));
            var editedActions = script.Actions.ToArray();

            typeof(MainForm).GetMethod("NewScript", PrivateInstance)!.Invoke(form, null);
            Assert.AreSame(script, GetField<Funscript>(form, "_script"));
            Assert.AreEqual(path, GetField<string>(form, "_scriptPath"));
            Assert.IsTrue(timeline.History.IsDirty);
            CollectionAssert.AreEqual(originalBytes, File.ReadAllBytes(path));

            var closing = new FormClosingEventArgs(CloseReason.UserClosing, false);
            typeof(Form).GetMethod("OnFormClosing", PrivateInstance)!.Invoke(form, [closing]);

            Assert.IsTrue(closing.Cancel);
            Assert.AreEqual(2, confirmationCount);
            Assert.IsFalse(GetField<bool>(form, "_isClosing"));
            Assert.IsTrue(timeline.History.IsDirty);
            CollectionAssert.AreEqual(editedActions, script.Actions.ToArray());
            CollectionAssert.AreEqual(originalBytes, File.ReadAllBytes(path));
        }
        finally
        {
            var filter = GetField<IMessageFilter?>(form, "_wheelFilter");
            if (filter is not null) Application.RemoveMessageFilter(filter);
            if (File.Exists(path)) File.Delete(path);
        }
    }

    private static T GetField<T>(object target, string name) =>
        (T)target.GetType().GetField(name, PrivateInstance)!.GetValue(target)!;

    private static void SetField(object target, string name, object? value) =>
        target.GetType().GetField(name, PrivateInstance)!.SetValue(target, value);
}
