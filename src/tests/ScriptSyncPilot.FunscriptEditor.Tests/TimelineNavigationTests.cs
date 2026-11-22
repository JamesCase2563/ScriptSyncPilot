using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ScriptSyncPilot.Common;
using ScriptSyncPilot.FunscriptEditor;

namespace ScriptSyncPilot.FunscriptEditor.Tests;

[TestClass]
public sealed class TimelineNavigationTests
{
    [STATestMethod]
    public void PreviousAndNext_NavigateStrictlyRelativeToPlayheadAndRequestSeek()
    {
        using var timeline = CreateTimeline();
        var script = CreateScript(1000, 2000, 3000);
        timeline.LoadScript(script);
        timeline.UpdateDuration(10);
        timeline.PixelsPerSecond = 100;
        var originalActions = script.Actions.ToArray();
        var originalZoom = timeline.PixelsPerSecond;
        var seeks = new List<double>();
        timeline.SeekRequested += seeks.Add;

        timeline.SetPlayhead(2);
        Assert.IsTrue(timeline.NavigateToPreviousAction());
        Assert.AreEqual(1.0, timeline.PlayheadSeconds);
        CollectionAssert.AreEqual(new[] { 1.0 }, seeks);

        timeline.SetPlayhead(2);
        Assert.IsTrue(timeline.NavigateToNextAction());
        Assert.AreEqual(3.0, timeline.PlayheadSeconds);
        CollectionAssert.AreEqual(new[] { 1.0, 3.0 }, seeks);

        timeline.SetPlayhead(1.0005);
        Assert.IsTrue(timeline.NavigateToPreviousAction());
        Assert.AreEqual(1.0, timeline.PlayheadSeconds);
        timeline.SetPlayhead(1.0005);
        Assert.IsTrue(timeline.NavigateToNextAction());
        Assert.AreEqual(2.0, timeline.PlayheadSeconds);
        Assert.AreEqual(originalZoom, timeline.PixelsPerSecond);
        CollectionAssert.AreEqual(originalActions, script.Actions.ToArray());
        Assert.IsFalse(timeline.History.CanUndo);
        Assert.AreEqual(4, seeks.Count);
    }

    [STATestMethod]
    public void PreviousAndNext_DoNotWrapAtBoundaries()
    {
        using var timeline = CreateTimeline();
        timeline.LoadScript(CreateScript(1000, 2000));
        timeline.UpdateDuration(10);
        var seeks = new List<double>();
        timeline.SeekRequested += seeks.Add;

        timeline.SetPlayhead(0.5);
        Assert.IsFalse(timeline.NavigateToPreviousAction());
        Assert.AreEqual(0.5, timeline.PlayheadSeconds);
        Assert.AreEqual(0, seeks.Count);
        Assert.IsTrue(timeline.NavigateToNextAction());
        Assert.AreEqual(1.0, timeline.PlayheadSeconds);

        timeline.SetPlayhead(2.0);
        Assert.IsFalse(timeline.NavigateToNextAction());
        Assert.AreEqual(2.0, timeline.PlayheadSeconds);
        Assert.AreEqual(1, seeks.Count);
        Assert.IsTrue(timeline.NavigateToPreviousAction());
        Assert.AreEqual(1.0, timeline.PlayheadSeconds);
        Assert.AreEqual(2, seeks.Count);
    }

    [STATestMethod]
    public void SelectedNavigation_SeeksSelectedActionAndNoSelectionIsNoOp()
    {
        using var timeline = CreateTimeline();
        var script = CreateScript(1000, 2000, 3000);
        timeline.LoadScript(script);
        timeline.UpdateDuration(10);
        timeline.PixelsPerSecond = 100;
        var seeks = new List<double>();
        timeline.SeekRequested += seeks.Add;

        Assert.IsFalse(timeline.NavigateToSelectedAction());
        Assert.AreEqual(0, seeks.Count);

        SelectAction(timeline, 2000);
        var actions = script.Actions.ToArray();
        var zoom = timeline.PixelsPerSecond;
        timeline.SetPlayhead(5);
        Assert.IsTrue(timeline.NavigateToSelectedAction());

        Assert.AreEqual(2.0, timeline.PlayheadSeconds);
        CollectionAssert.AreEqual(new[] { 2.0 }, seeks);
        timeline.SetPlayhead(5);
        Assert.IsTrue(timeline.NavigateToSelectedAction(), "Navigation must leave the current selection intact.");
        Assert.AreEqual(2.0, timeline.PlayheadSeconds);
        CollectionAssert.AreEqual(new[] { 2.0, 2.0 }, seeks);
        CollectionAssert.AreEqual(actions, script.Actions.ToArray());
        Assert.AreEqual(zoom, timeline.PixelsPerSecond);
        Assert.IsFalse(timeline.History.CanUndo);
    }

    [STATestMethod]
    public void EmptyScriptNavigation_IsNoOp()
    {
        using var timeline = CreateTimeline();
        timeline.LoadScript(new Funscript());
        timeline.SetPlayhead(4.5);
        var seeks = new List<double>();
        timeline.SeekRequested += seeks.Add;

        Assert.IsFalse(timeline.NavigateToPreviousAction());
        Assert.IsFalse(timeline.NavigateToNextAction());
        Assert.IsFalse(timeline.NavigateToSelectedAction());

        Assert.AreEqual(4.5, timeline.PlayheadSeconds);
        Assert.AreEqual(0, seeks.Count);
        Assert.IsFalse(timeline.History.CanUndo);
    }

    [STATestMethod]
    public void FocusedTimeline_RoutesConfirmedShortcutsWithoutMpv()
    {
        using var form = new Form { ClientSize = new Size(1040, 200), KeyPreview = true };
        using var timeline = CreateTimeline();
        var script = CreateScript(1000, 2000, 3000);
        timeline.Dock = DockStyle.Fill;
        timeline.LoadScript(script);
        timeline.UpdateDuration(10);
        timeline.PixelsPerSecond = 100;
        form.Controls.Add(timeline);
        form.Show();
        form.Activate();
        Assert.IsTrue(timeline.Focus());
        Assert.IsTrue(timeline.Focused);

        Assert.IsTrue(IsInputKey(timeline, Keys.Control | Keys.Up));
        Assert.IsTrue(IsInputKey(timeline, Keys.Control | Keys.Down));
        Assert.IsTrue(IsInputKey(timeline, Keys.Enter));
        Assert.IsTrue(IsInputKey(timeline, Keys.Left), "Existing playback stepping remains an input key.");
        Assert.IsFalse(IsInputKey(timeline, Keys.Control | Keys.Shift | Keys.Up));
        Assert.IsFalse(IsInputKey(timeline, Keys.Shift | Keys.Enter));

        var originalZoom = timeline.PixelsPerSecond;
        var originalActions = script.Actions.ToArray();
        var seeks = new List<double>();
        timeline.SeekRequested += seeks.Add;

        var noSelection = SendKeyDown(timeline, Keys.Enter);
        Assert.IsTrue(noSelection.Handled);
        Assert.IsTrue(noSelection.SuppressKeyPress);
        Assert.AreEqual(0, seeks.Count);

        timeline.SetPlayhead(1);
        var next = SendKeyDown(timeline, Keys.Control | Keys.Down);
        Assert.IsTrue(next.Handled);
        Assert.AreEqual(2.0, timeline.PlayheadSeconds);

        var previous = SendKeyDown(timeline, Keys.Control | Keys.Up);
        Assert.IsTrue(previous.Handled);
        Assert.AreEqual(1.0, timeline.PlayheadSeconds);

        SelectAction(timeline, 3000);
        timeline.SetPlayhead(5);
        var selected = SendKeyDown(timeline, Keys.Enter);
        Assert.IsTrue(selected.Handled);
        Assert.AreEqual(3.0, timeline.PlayheadSeconds);
        CollectionAssert.AreEqual(new[] { 2.0, 1.0, 3.0 }, seeks);
        Assert.AreEqual(originalZoom, timeline.PixelsPerSecond);
        CollectionAssert.AreEqual(originalActions, script.Actions.ToArray());
        Assert.IsFalse(timeline.History.CanUndo);
        form.Close();
    }

    [STATestMethod]
    public void UnfocusedTimeline_DoesNotConsumePointShortcuts()
    {
        using var form = new Form { ClientSize = new Size(1040, 200) };
        using var timeline = CreateTimeline();
        var input = new TextBox();
        var script = CreateScript(1000, 2000);
        timeline.LoadScript(script);
        form.Controls.Add(timeline);
        form.Controls.Add(input);
        form.Show();
        Assert.IsTrue(input.Focus());
        Assert.IsFalse(timeline.Focused);
        var seeks = new List<double>();
        timeline.SeekRequested += seeks.Add;
        timeline.SetPlayhead(1);

        foreach (var shortcut in new[] { Keys.Control | Keys.Up, Keys.Control | Keys.Down, Keys.Enter })
        {
            var key = SendKeyDown(timeline, shortcut);
            Assert.IsFalse(key.Handled, $"{shortcut} should not be consumed while the text input has focus.");
        }

        Assert.AreEqual(1.0, timeline.PlayheadSeconds);
        Assert.AreEqual(0, seeks.Count);
        Assert.AreEqual(2, script.Actions.Count);
        form.Close();
    }

    private static TimelineControl CreateTimeline() => new() { Size = new Size(1040, 200) };

    private static Funscript CreateScript(params int[] timestamps)
    {
        var script = new Funscript();
        foreach (var timestamp in timestamps)
            script.AddOrReplace(new FunscriptAction(timestamp, 50));
        return script;
    }

    private static void SelectAction(TimelineControl timeline, int timestamp)
    {
        var x = timeline.Padding.Left + (int)Math.Round(timestamp / 1000.0 * timeline.PixelsPerSecond);
        var trackTop = timeline.Padding.Top + 10;
        var trackBottom = timeline.Height - timeline.Padding.Bottom - 24 - 10;
        var y = (int)Math.Round(trackBottom - (trackBottom - trackTop) / 2.0);
        timeline.HandleMouseDown(new MouseEventArgs(MouseButtons.Left, 1, x, y, 0), Keys.None);
        typeof(TimelineControl).GetMethod("OnMouseUp", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(timeline, [new MouseEventArgs(MouseButtons.Left, 1, x, y, 0)]);
    }

    private static bool IsInputKey(TimelineControl timeline, Keys keyData) =>
        (bool)typeof(TimelineControl).GetMethod("IsInputKey", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(timeline, [keyData])!;

    private static KeyEventArgs SendKeyDown(TimelineControl timeline, Keys keyData)
    {
        var args = new KeyEventArgs(keyData);
        typeof(TimelineControl).GetMethod("OnKeyDown", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(timeline, [args]);
        return args;
    }
}
