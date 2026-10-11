using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ScriptSyncPilot.Common;
using ScriptSyncPilot.FunscriptEditor;

namespace ScriptSyncPilot.FunscriptEditor.Tests;

[TestClass]
public sealed class PlaybackSpeedCommandTests
{
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