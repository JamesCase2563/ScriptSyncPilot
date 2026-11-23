using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ScriptSyncPilot.Common;
using ScriptSyncPilot.HandySync;
using EditorForm = ScriptSyncPilot.FunscriptEditor.MainForm;
using HandyForm = ScriptSyncPilot.HandySync.MainForm;

namespace ScriptSyncPilot.FunscriptEditor.Tests;

[TestClass]
public sealed class MpvSessionReplacementTests
{
    private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;

    [TestMethod]
    public void Editor_UsesMillisecondPrecisionWhenConfirmingSeekPosition()
    {
        Assert.IsFalse(EditorForm.IsPositionNearSeekTarget(5, 4), "A stale position before the target must not confirm it.");
        Assert.IsTrue(EditorForm.IsPositionNearSeekTarget(5, 5.0004), "A sub-millisecond difference is within seek precision.");
        Assert.IsFalse(EditorForm.IsPositionNearSeekTarget(5, 5.001), "A neighboring millisecond timestamp must remain distinguishable.");
        Assert.IsTrue(EditorForm.IsPositionNearSeekTarget(5, 4.9996), "A sub-millisecond difference is within seek precision.");
        Assert.IsFalse(EditorForm.IsPositionNearSeekTarget(5, 6), "A stale position after the target must not confirm it.");

        Assert.IsTrue(EditorForm.IsPositionNearSeekTarget(5, 5.016, 30), "A reported position within half a 30 fps frame confirms the seek.");
        Assert.IsFalse(EditorForm.IsPositionNearSeekTarget(5, 5.017, 30), "A position beyond half a frame does not confirm the seek.");
    }

    [TestMethod]
    public void Editor_UsesGenerationToRejectSupersededPausedSeekResults()
    {
        using var form = new EditorForm(new EditorSettings());
        using var session = new MpvSession();
        var timeline = (TimelineControl)GetField(form, "_timeline");
        SetField(form, "_mpv", session);
        var pendingType = typeof(EditorForm).GetNestedType("PendingMpvSeek", BindingFlags.NonPublic)!;
        var superseded = Activator.CreateInstance(pendingType, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            binder: null, args: [session, 2.0, 1L], culture: null)!;
        var current = Activator.CreateInstance(pendingType, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            binder: null, args: [session, 3.0, 2L], culture: null)!;
        SetField(form, "_mpvSeekGeneration", 2L);
        SetField(form, "_pendingMpvSeek", current);
        timeline.SetPlayhead(3.0);

        Assert.IsFalse((bool)InvokeResult(form, "IsCurrentPendingSeek", superseded)!,
            "An older seek generation must no longer own paused navigation state.");
        Assert.IsTrue((bool)InvokeResult(form, "IsCurrentPendingSeek", current)!);
        Assert.IsFalse((bool)InvokeResult(form, "ShouldApplyMpvPositionUpdate", session)!,
            "An observation matching an older seek must not replace the newest paused navigation target.");
        Assert.AreEqual(3.0, timeline.PlayheadSeconds);
    }

    [STATestMethod]
    public void Editor_KeepsPausedNavigationCursorWhenMpvHasNotConfirmedSeek()
    {
        using var form = new EditorForm(new EditorSettings());
        using var session = new MpvSession();
        var timeline = (TimelineControl)GetField(form, "_timeline");
        timeline.SetPlayhead(3.0);
        SetField(form, "_mpv", session);

        var pendingType = typeof(EditorForm).GetNestedType("PendingMpvSeek", BindingFlags.NonPublic)!;
        var pending = Activator.CreateInstance(pendingType, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            binder: null, args: [session, 3.0, 1L], culture: null)!;
        SetField(form, "_mpvSeekGeneration", 1L);
        SetField(form, "_pendingMpvSeek", pending);

        Invoke(form, "KeepRequestedPausedSeek", pending, 1.0);

        Assert.AreSame(pending, GetField(form, "_pendingMpvSeek"));
        Assert.AreEqual(3.0, timeline.PlayheadSeconds,
            "An unconfirmed mpv position must not roll the paused navigation cursor back.");
        Assert.IsFalse((bool)InvokeResult(form, "ShouldApplyMpvPositionUpdate", session)!);
    }

    [STATestMethod]
    public void Editor_DropsQueuedNotificationsAfterSessionReplacement()
    {
        using var form = new EditorForm(new EditorSettings());
        using var staleSession = new MpvSession();
        using var currentSession = new MpvSession();
        _ = form.Handle;
        SetField(form, "_mpv", staleSession);
        SetField(form, "_activePipe", "old-pipe");

        var timeline = (TimelineControl)GetField(form, "_timeline");
        var playPause = (Button)GetField(form, "_playPauseButton");
        var time = (Label)GetField(form, "_timeLabel");
        var speed = (Label)GetField(form, "_speedLabel");
        var originalDuration = timeline.DurationSeconds;
        var originalTime = time.Text;
        var originalSpeed = speed.Text;

        Invoke(form, "OnMpvPositionChanged", staleSession, 42d);
        Invoke(form, "OnMpvPauseChanged", staleSession, false);
        Invoke(form, "OnMpvDurationChanged", staleSession, 120d);
        Invoke(form, "OnMpvSpeedChanged", staleSession, 1.5d);
        Invoke(form, "OnMpvDisconnected", staleSession);
        SetField(form, "_mpv", currentSession);
        PumpUiQueue();

        Assert.AreSame(currentSession, GetField(form, "_mpv"));
        Assert.AreEqual(0d, timeline.PlayheadSeconds);
        Assert.AreEqual(originalDuration, timeline.DurationSeconds);
        Assert.AreEqual("Play", playPause.Text);
        Assert.AreEqual(originalTime, time.Text);
        Assert.AreEqual(originalSpeed, speed.Text);
    }

    [STATestMethod]
    public void HandySync_DropsQueuedNotificationsAfterSessionReplacement()
    {
        using var form = new HandyForm();
        using var staleSession = new MpvSession();
        using var currentSession = new MpvSession();
        _ = form.Handle;
        SetField(form, "_mpv", staleSession);
        SetField(form, "_mpvAttached", true);

        var waveform = GetField<ScriptWaveformControl>(form, "_waveform");
        var status = (Label)GetField(form, "_status");
        var initialStatus = status.Text;

        InvokeTask(form, "OnMpvPositionChangedAsync", staleSession, 42d).GetAwaiter().GetResult();
        Invoke(form, "OnMpvDurationChanged", staleSession, 120d);
        Invoke(form, "OnMpvSpeedChanged", staleSession);
        Invoke(form, "OnMpvDisconnected", staleSession);
        SetField(form, "_mpv", currentSession);
        PumpUiQueue();

        Assert.AreSame(currentSession, GetField(form, "_mpv"));
        Assert.IsTrue(GetField<bool>(form, "_mpvAttached"));
        Assert.AreEqual(0d, GetField<double>(waveform, "_playheadSeconds"));
        Assert.AreEqual(0d, GetField<double>(waveform, "_mediaDurationSeconds"));
        Assert.AreEqual(initialStatus, status.Text);
    }

    private static object GetField(object target, string name) =>
        target.GetType().GetField(name, PrivateInstance)!.GetValue(target)!;

    private static T GetField<T>(object target, string name) =>
        (T)GetField(target, name);

    private static void SetField(object target, string name, object? value) =>
        target.GetType().GetField(name, PrivateInstance)!.SetValue(target, value);

    private static void Invoke(object target, string method, params object[] arguments) =>
        target.GetType().GetMethod(method, PrivateInstance)!.Invoke(target, arguments);

    private static object? InvokeResult(object target, string method, params object[] arguments) =>
        target.GetType().GetMethod(method, PrivateInstance)!.Invoke(target, arguments);

    private static Task InvokeTask(object target, string method, params object[] arguments) =>
        (Task)target.GetType().GetMethod(method, PrivateInstance)!.Invoke(target, arguments)!;

    private static void PumpUiQueue()
    {
        Application.DoEvents();
        Application.DoEvents();
    }
}
