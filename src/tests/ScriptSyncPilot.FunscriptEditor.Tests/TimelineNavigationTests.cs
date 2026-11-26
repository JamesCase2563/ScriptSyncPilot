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
    public void PointNavigation_UsesTheExistingFollowAnchorWithoutChangingZoom()
    {
        using var timeline = CreateTimeline();
        var script = CreateScript(1000, 2000, 3000);
        timeline.LoadScript(script);
        timeline.UpdateDuration(10);
        timeline.PixelsPerSecond = 100;
        timeline.SetFollowAnchorX(timeline.Padding.Left + 125);
        var originalActions = script.Actions.ToArray();
        var seeks = new List<double>();
        timeline.SeekRequested += seeks.Add;

        Assert.IsTrue(timeline.NavigateToNextAction());

        var viewportSeconds = (timeline.ClientSize.Width - timeline.Padding.Horizontal - 40.0)
            / timeline.PixelsPerSecond;
        var expectedViewStart = 1 - (timeline.FollowAnchorX - timeline.Padding.Left) / timeline.PixelsPerSecond;
        var scrollbar = timeline.Controls.OfType<HScrollBar>().Single();
        Assert.AreEqual((int)Math.Round((expectedViewStart + viewportSeconds) * 1000), scrollbar.Value);
        Assert.AreEqual(1.0, timeline.PlayheadSeconds);
        Assert.AreEqual(100.0, timeline.PixelsPerSecond);
        CollectionAssert.AreEqual(new[] { 1.0 }, seeks);
        CollectionAssert.AreEqual(originalActions, script.Actions.ToArray());
        Assert.IsFalse(timeline.History.CanUndo);
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
    public void RepeatedPreviousAndNext_NavigateOnePointPerCommand()
    {
        using var timeline = CreateTimeline();
        timeline.LoadScript(CreateScript(1000, 2000, 3000, 4000, 5000));
        timeline.SetPlayhead(3);
        var seeks = new List<double>();
        timeline.SeekRequested += seeks.Add;

        Assert.IsTrue(timeline.NavigateToNextAction());
        Assert.AreEqual(4.0, timeline.PlayheadSeconds);
        Assert.IsTrue(timeline.NavigateToNextAction());
        Assert.AreEqual(5.0, timeline.PlayheadSeconds);
        Assert.IsFalse(timeline.NavigateToNextAction());

        Assert.IsTrue(timeline.NavigateToPreviousAction());
        Assert.AreEqual(4.0, timeline.PlayheadSeconds);
        Assert.IsTrue(timeline.NavigateToPreviousAction());
        Assert.AreEqual(3.0, timeline.PlayheadSeconds);
        Assert.IsTrue(timeline.NavigateToPreviousAction());
        Assert.AreEqual(2.0, timeline.PlayheadSeconds);
        CollectionAssert.AreEqual(new[] { 4.0, 5.0, 4.0, 3.0, 2.0 }, seeks);
    }

    [STATestMethod]
    public void FrameRateTolerance_SnapsNavigationToTheClosestPointWithinHalfAFrame()
    {
        using var timeline = CreateTimeline();
        timeline.LoadScript(CreateScript(1000, 2000, 3000));
        timeline.VideoFrameRate = 30;
        timeline.SetPlayhead(1.01);
        var seeks = new List<double>();
        timeline.SeekRequested += seeks.Add;

        Assert.IsTrue(timeline.NavigateToNextAction());
        Assert.AreEqual(2.0, timeline.PlayheadSeconds, "Next should advance beyond the point matched within half a frame.");

        timeline.SetPlayhead(2.01);
        Assert.IsTrue(timeline.NavigateToPreviousAction());
        Assert.AreEqual(1.0, timeline.PlayheadSeconds, "Previous should advance before the point matched within half a frame.");
        CollectionAssert.AreEqual(new[] { 2.0, 1.0 }, seeks);
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

        timeline.CenterPlayhead();
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
        Assert.IsTrue(IsInputKey(timeline, Keys.Control | Keys.Enter));
        Assert.IsFalse(IsInputKey(timeline, Keys.Enter));
        Assert.IsTrue(IsInputKey(timeline, Keys.Left), "Existing playback stepping remains an input key.");
        Assert.IsFalse(IsInputKey(timeline, Keys.Control | Keys.Shift | Keys.Up));
        Assert.IsFalse(IsInputKey(timeline, Keys.Shift | Keys.Enter));

        var originalZoom = timeline.PixelsPerSecond;
        var originalActions = script.Actions.ToArray();
        var seeks = new List<double>();
        timeline.SeekRequested += seeks.Add;

        var plainEnter = SendKeyDown(timeline, Keys.Enter);
        Assert.IsFalse(plainEnter.Handled, "Unmodified Enter retains its normal WinForms behavior.");

        var noSelection = SendKeyDown(timeline, Keys.Control | Keys.Enter);
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
        var selected = SendKeyDown(timeline, Keys.Control | Keys.Enter);
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

        foreach (var shortcut in new[] { Keys.Control | Keys.Up, Keys.Control | Keys.Down, Keys.Control | Keys.Enter })
        {
            var key = SendKeyDown(timeline, shortcut);
            Assert.IsFalse(key.Handled, $"{shortcut} should not be consumed while the text input has focus.");
        }

        Assert.AreEqual(1.0, timeline.PlayheadSeconds);
        Assert.AreEqual(0, seeks.Count);
        Assert.AreEqual(2, script.Actions.Count);
        form.Close();
    }

    [STATestMethod]
    public void PointNavigationButtons_TrackShortcutAvailabilityAndSitBelowSeekRow()
    {
        using var form = new MainForm(new EditorSettings());
        var timeline = GetField<TimelineControl>(form, "_timeline");
        var previous = GetField<Button>(form, "_previousPointButton");
        var selected = GetField<Button>(form, "_selectedPointButton");
        var next = GetField<Button>(form, "_nextPointButton");
        form.Show();
        form.PerformLayout();
        var script = CreateScript(1000, 2000, 3000);
        timeline.LoadScript(script);
        timeline.UpdateDuration(10);
        timeline.PixelsPerSecond = 100;

        Assert.IsFalse(previous.Enabled);
        Assert.IsFalse(selected.Enabled);
        Assert.IsTrue(next.Enabled);

        timeline.SetPlayhead(2);
        Assert.IsTrue(previous.Enabled);
        Assert.IsTrue(next.Enabled);
        SelectAction(timeline, 2000);
        Assert.IsTrue(selected.Enabled);

        timeline.SetPlayhead(3);
        Assert.IsTrue(previous.Enabled);
        Assert.IsFalse(next.Enabled);
        Assert.IsTrue(selected.Enabled);

        var stepBar = GetField<Button>(form, "_backFrameButton").Parent!;
        Assert.AreSame(stepBar, GetField<Button>(form, "_forwardFrameButton").Parent);
        Assert.IsTrue(selected.Parent!.Top >= stepBar.Bottom, "Point navigation row should be below the seek row.");

        timeline.LoadScript(new Funscript());
        Assert.IsFalse(previous.Enabled);
        Assert.IsFalse(selected.Enabled);
        Assert.IsFalse(next.Enabled);
    }

    [STATestMethod]
    public void PointNavigationButtons_MatchShortcutDestinationsAndShowHint()
    {
        using var form = new MainForm(new EditorSettings());
        var timeline = GetField<TimelineControl>(form, "_timeline");
        var previous = GetField<Button>(form, "_previousPointButton");
        var selected = GetField<Button>(form, "_selectedPointButton");
        var next = GetField<Button>(form, "_nextPointButton");
        var script = CreateScript(1000, 2000, 3000);
        timeline.LoadScript(script);
        form.Show();
        form.Activate();
        timeline.UpdateDuration(10);
        timeline.PixelsPerSecond = 100;
        Assert.IsTrue(timeline.Focus());

        var seeks = new List<double>();
        timeline.SeekRequested += seeks.Add;

        timeline.SetPlayhead(2);
        previous.PerformClick();
        Assert.AreEqual(1.0, timeline.PlayheadSeconds);
        Assert.AreEqual(1.0, seeks[^1]);
        timeline.SetPlayhead(2);
        Assert.IsTrue(timeline.Focus());
        SendKeyDown(timeline, Keys.Control | Keys.Up);
        Assert.AreEqual(1.0, timeline.PlayheadSeconds);
        Assert.AreEqual(1.0, seeks[^1]);

        timeline.SetPlayhead(2);
        next.PerformClick();
        Assert.AreEqual(3.0, timeline.PlayheadSeconds);
        Assert.AreEqual(3.0, seeks[^1]);
        timeline.SetPlayhead(2);
        Assert.IsTrue(timeline.Focus());
        SendKeyDown(timeline, Keys.Control | Keys.Down);
        Assert.AreEqual(3.0, timeline.PlayheadSeconds);
        Assert.AreEqual(3.0, seeks[^1]);

        timeline.CenterPlayhead();
        SelectAction(timeline, 2000);
        Assert.IsTrue(timeline.CanNavigateToSelectedAction, "The action under the pointer should be selected.");
        Assert.IsTrue(selected.Enabled, "The selected-point button should follow selection availability.");
        timeline.SetPlayhead(5);
        selected.PerformClick();
        Assert.AreEqual(2.0, timeline.PlayheadSeconds);
        Assert.AreEqual(2.0, seeks[^1]);
        timeline.SetPlayhead(5);
        Assert.IsTrue(timeline.Focus());
        SendKeyDown(timeline, Keys.Control | Keys.Enter);
        Assert.AreEqual(2.0, timeline.PlayheadSeconds);
        Assert.AreEqual(2.0, seeks[^1]);

        var hint = selected.Parent!.Controls.OfType<Label>().Single(label => label.Text.Contains("Shortcuts:"));
        Assert.IsTrue(hint.Visible);
        StringAssert.Contains(hint.Text, "Ctrl+Up = previous");
        StringAssert.Contains(hint.Text, "Ctrl+Down = next");
        StringAssert.Contains(hint.Text, "Ctrl+Enter = selected");
        Assert.AreEqual(6, seeks.Count);
        form.Close();
    }

    [STATestMethod]
    public void CenterPlayheadButton_ResetsAnchorAndCentersCurrentPositionWithoutSeeking()
    {
        using var form = new MainForm(new EditorSettings());
        var timeline = GetField<TimelineControl>(form, "_timeline");
        var centerButton = GetField<Button>(form, "_centerPlayheadButton");
        form.Show();
        timeline.UpdateDuration(60);
        timeline.PixelsPerSecond = 100;
        timeline.SetFollowAnchorX(timeline.Padding.Left + 100);
        timeline.SetPlayheadAtFollowAnchor(20);
        var seeks = 0;
        timeline.SeekRequested += _ => seeks++;

        centerButton.PerformClick();

        var centerX = timeline.Padding.Left
            + (timeline.ClientSize.Width - timeline.Padding.Horizontal - 40.0) / 2;
        var viewportSeconds = (timeline.ClientSize.Width - timeline.Padding.Horizontal - 40.0)
            / timeline.PixelsPerSecond;
        var viewStartSeconds = timeline.Controls.OfType<HScrollBar>().Single().Value / 1000.0 - viewportSeconds;
        var expectedViewStart = timeline.PlayheadSeconds - (centerX - timeline.Padding.Left) / timeline.PixelsPerSecond;
        Assert.AreEqual(centerX, timeline.FollowAnchorX, 0.001);
        Assert.AreEqual(expectedViewStart, viewStartSeconds, 0.001);
        Assert.AreEqual(20.0, timeline.PlayheadSeconds);
        Assert.AreEqual(100.0, timeline.PixelsPerSecond);
        Assert.AreEqual(0, seeks);
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
        var viewportSeconds = (timeline.ClientSize.Width - timeline.Padding.Horizontal - 40.0)
            / timeline.PixelsPerSecond;
        var viewStartSeconds = timeline.Controls.OfType<HScrollBar>().Single().Value / 1000.0 - viewportSeconds;
        var x = (int)Math.Round(timeline.Padding.Left
            + (timestamp / 1000.0 - viewStartSeconds) * timeline.PixelsPerSecond);
        var trackTop = timeline.Padding.Top + 10;
        var trackBottom = timeline.Height - timeline.Padding.Bottom - 24 - 10;
        var y = (int)Math.Round(trackBottom - (trackBottom - trackTop) / 2.0);
        var hit = typeof(TimelineControl).GetMethod("HitTest", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(timeline, [new Point(x, y)]);
        timeline.HandleMouseDown(new MouseEventArgs(MouseButtons.Left, 1, x, y, 0), Keys.None);
        typeof(TimelineControl).GetMethod("OnMouseUp", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(timeline, [new MouseEventArgs(MouseButtons.Left, 1, x, y, 0)]);
        if (hit is null)
            Assert.Fail($"Expected hit at action {timestamp} ms; x={x}, y={y}, width={timeline.ClientSize.Width}, height={timeline.Height}, scroll={timeline.Controls.OfType<HScrollBar>().Single().Value}, viewport={viewportSeconds}, anchor={timeline.FollowAnchorX}, pps={timeline.PixelsPerSecond}.");
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

    private static T GetField<T>(object target, string name) =>
        (T)target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(target)!;
}
