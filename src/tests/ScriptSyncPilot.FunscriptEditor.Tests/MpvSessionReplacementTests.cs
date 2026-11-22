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
    public void Editor_RecognizesWhenMpvPositionHasReachedPendingSeekTarget()
    {
        Assert.IsFalse(EditorForm.HasReachedSeekTarget(4, 5, 4), "A stale position before a forward seek target must not replace it.");
        Assert.IsTrue(EditorForm.HasReachedSeekTarget(4, 5, 4.996), "A position within tolerance of the forward target is an acknowledgment.");
        Assert.IsTrue(EditorForm.HasReachedSeekTarget(4, 5, 5.2), "Playback moving beyond the target has reached it.");

        Assert.IsFalse(EditorForm.HasReachedSeekTarget(6, 5, 6), "A stale position after a backward seek target must not replace it.");
        Assert.IsTrue(EditorForm.HasReachedSeekTarget(6, 5, 5.004), "A position within tolerance of the backward target is an acknowledgment.");
        Assert.IsTrue(EditorForm.HasReachedSeekTarget(6, 5, 4.8), "Playback moving beyond the target has reached it.");
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

    private static Task InvokeTask(object target, string method, params object[] arguments) =>
        (Task)target.GetType().GetMethod(method, PrivateInstance)!.Invoke(target, arguments)!;

    private static void PumpUiQueue()
    {
        Application.DoEvents();
        Application.DoEvents();
    }
}
