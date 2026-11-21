using System.Runtime.ExceptionServices;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ScriptSyncPilot.Common;
using ScriptSyncPilot.FunscriptEditor;

namespace ScriptSyncPilot.FunscriptEditor.Tests;

[TestClass]
public sealed class NumericPositionEntryTests
{
    private static long KeyData(int scanCode, bool repeated = false, bool extended = false) =>
        ((long)scanCode << 16) | (repeated ? 1L << 30 : 0) | (extended ? 1L << 24 : 0) | 1;

    [TestMethod]
    public void Decoder_MapsAllElevenPhysicalPositionsAndConsumesRepeats()
    {
        var codes = new[] { 0x29, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08, 0x09, 0x0a, 0x0b };
        for (var index = 0; index < codes.Length; index++)
        {
            Assert.IsTrue(TimelineControl.TryDecodePositionKey(0x0100, KeyData(codes[index]), true, Keys.None, out var position));
            Assert.AreEqual((int?)(index * 10), position);
            Assert.IsTrue(TimelineControl.TryDecodePositionKey(0x0100, KeyData(codes[index], repeated: true), true, Keys.None, out position));
            Assert.IsNull(position);
        }
    }

    [TestMethod]
    public void Decoder_LeavesModifiersFocusAndOtherMessagesUntouched()
    {
        for (var mask = 1; mask < 8; mask++)
        {
            var modifiers = (mask & 1) != 0 ? Keys.Shift : Keys.None;
            if ((mask & 2) != 0) modifiers |= Keys.Control;
            if ((mask & 4) != 0) modifiers |= Keys.Alt;
            Assert.IsFalse(TimelineControl.TryDecodePositionKey(0x0100, KeyData(0x02), true, modifiers, out var position));
            Assert.IsNull(position);
        }
        Assert.IsFalse(TimelineControl.TryDecodePositionKey(0x0100, KeyData(0x02), false, Keys.None, out _));
        foreach (var message in new[] { 0x0101, 0x0102, 0x0104, 0x0105 })
            Assert.IsFalse(TimelineControl.TryDecodePositionKey(message, KeyData(0x02), true, Keys.None, out _));
        Assert.IsFalse(TimelineControl.TryDecodePositionKey(0x0100, KeyData(0x02, extended: true), true, Keys.None, out _));
    }

    [TestMethod]
    public void Decoder_DoesNotConsumeNavigationRepeatsOrNumpad()
    {
        foreach (var scanCode in new[] { 0x4b, 0x4d, 0x48, 0x50, 0x52, 0x37, 0x10, 0x28, 0x0c })
        {
            foreach (var extended in new[] { false, true })
                Assert.IsFalse(TimelineControl.TryDecodePositionKey(0x0100, KeyData(scanCode, repeated: true, extended: extended), true, Keys.None, out _));
        }
    }

    [TestMethod]
    public void Routing_FocusedTimelineConsumesPhysicalKeyThroughWinFormsPreprocessing()
    {
        RunSta(() =>
        {
            using var form = new Form();
            using var timeline = new TimelineControl { Dock = DockStyle.Fill };
            form.Controls.Add(timeline);
            var script = new Funscript();
            timeline.LoadScript(script);
            form.Show();
            form.Activate();
            Assert.IsTrue(timeline.Focus());
            Assert.IsTrue(timeline.Focused);
            var message = Message.Create(timeline.Handle, 0x0100, (IntPtr)Keys.D1, (IntPtr)KeyData(0x02));
            Assert.AreEqual(PreProcessControlState.MessageProcessed, timeline.PreProcessControlMessage(ref message));
            Assert.AreEqual(1, script.Actions.Count);
            Assert.AreEqual(new FunscriptAction(0, 10), script.Actions[0]);
            var playbackRequests = 0;
            timeline.PlayPauseRequested += (_, _) => playbackRequests++;
            message = Message.Create(timeline.Handle, 0x0100, (IntPtr)Keys.Space, (IntPtr)KeyData(0x39));
            Assert.AreEqual(PreProcessControlState.MessageProcessed, timeline.PreProcessControlMessage(ref message));
            Assert.AreEqual(1, playbackRequests);
            message = Message.Create(timeline.Handle, 0x0100, (IntPtr)Keys.Space, (IntPtr)KeyData(0x39, repeated: true));
            Assert.AreEqual(PreProcessControlState.MessageProcessed, timeline.PreProcessControlMessage(ref message));
            Assert.AreEqual(1, playbackRequests);
            Assert.AreEqual(1, script.Actions.Count);
            form.Close();
        });
    }

    [TestMethod]
    public void ZoomAndScroll_StayWithinDurationAndViewportBounds()
    {
        RunSta(() =>
        {
            using var form = new Form { ClientSize = new Size(1040, 200) };
            using var timeline = new TimelineControl { Dock = DockStyle.Fill };
            form.Controls.Add(timeline);
            timeline.LoadScript(new Funscript());
            timeline.UpdateDuration(120);
            form.Show();
            var scrollbar = timeline.Controls.OfType<HScrollBar>().Single();

            timeline.PixelsPerSecond = 0.0001;
            Assert.AreEqual(timeline.MinPixelsPerSecond, timeline.PixelsPerSecond, 0.000001);
            Assert.AreEqual(0, scrollbar.Value);
            Assert.IsTrue(scrollbar.Value <= scrollbar.Maximum - scrollbar.LargeChange);

            timeline.PixelsPerSecond = double.MaxValue;
            Assert.AreEqual(timeline.MaxPixelsPerSecond, timeline.PixelsPerSecond, 0.000001);
            timeline.ZoomByWheel(12000, timeline.ClientSize.Width / 2);
            Assert.AreEqual(timeline.MaxPixelsPerSecond, timeline.PixelsPerSecond, 0.000001);
            Assert.IsTrue(scrollbar.Value >= 0);
            Assert.IsTrue(scrollbar.Value <= scrollbar.Maximum - scrollbar.LargeChange);

            timeline.ZoomByWheel(-12000, timeline.ClientSize.Width / 2);
            Assert.AreEqual(timeline.MinPixelsPerSecond, timeline.PixelsPerSecond, 0.000001);
            Assert.IsTrue(scrollbar.Value >= 0);
            Assert.IsTrue(scrollbar.Value <= scrollbar.Maximum - scrollbar.LargeChange);

            timeline.PixelsPerSecond = 50;
            timeline.UpdateDuration(10);
            Assert.AreEqual(timeline.MinPixelsPerSecond, timeline.PixelsPerSecond, 0.000001);
            Assert.IsTrue(scrollbar.Value <= scrollbar.Maximum - scrollbar.LargeChange);
            form.Close();
        });
    }

    [TestMethod]
    public void DeleteDuringActionDrag_EndsDragAndCannotMutateNextAction()
    {
        RunSta(() =>
        {
            using var form = new Form { ClientSize = new Size(1040, 200) };
            using var timeline = new TimelineControl { Dock = DockStyle.Fill };
            form.Controls.Add(timeline);
            var script = new Funscript();
            script.AddOrReplace(new FunscriptAction(5000, 50));
            script.AddOrReplace(new FunscriptAction(30000, 80));
            timeline.LoadScript(script);
            form.Show();
            timeline.PixelsPerSecond = 100;

            var x = timeline.Padding.Left + 500;
            var trackTop = timeline.Padding.Top + 10;
            var trackBottom = timeline.Height - timeline.Padding.Bottom - 24 - 10;
            var y = (int)Math.Round(trackBottom - 0.5 * (trackBottom - trackTop));
            void Mouse(string method, MouseEventArgs args) => typeof(TimelineControl)
                .GetMethod(method, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
                .Invoke(timeline, [args]);

            Mouse("OnMouseDown", new MouseEventArgs(MouseButtons.Left, 1, x, y, 0));
            Mouse("OnMouseMove", new MouseEventArgs(MouseButtons.Left, 1, x + 20, y - 10, 0));
            Assert.AreEqual(new FunscriptAction(5200, 57), script.Actions[0]);

            typeof(TimelineControl).GetMethod("OnKeyDown", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
                .Invoke(timeline, [new KeyEventArgs(Keys.Delete)]);
            Assert.AreEqual(1, script.Actions.Count);
            Assert.AreEqual(new FunscriptAction(30000, 80), script.Actions[0]);

            Mouse("OnMouseMove", new MouseEventArgs(MouseButtons.Left, 1, x + 300, y - 20, 0));
            Mouse("OnMouseUp", new MouseEventArgs(MouseButtons.Left, 1, x + 300, y - 20, 0));
            Assert.AreEqual(1, script.Actions.Count);
            Assert.AreEqual(new FunscriptAction(30000, 80), script.Actions[0]);
            Assert.IsTrue(timeline.History.CanUndo);
            form.Close();
        });
    }

    [TestMethod]
    public void Routing_UnfocusedTimelineDoesNotConsumePositionKey()
    {
        RunSta(() =>
        {
            using var form = new Form();
            using var timeline = new TimelineControl { Dock = DockStyle.Fill };
            var other = new TextBox();
            form.Controls.Add(timeline);
            form.Controls.Add(other);
            var script = new Funscript();
            timeline.LoadScript(script);
            form.Show();
            other.Focus();
            var message = Message.Create(timeline.Handle, 0x0100, (IntPtr)Keys.D1, (IntPtr)KeyData(0x02));

            timeline.PreProcessControlMessage(ref message);
            Assert.AreEqual(0, script.Actions.Count);
            Assert.IsFalse(timeline.Focused);
            form.Close();
        });
    }

    [TestMethod]
    public void Insertion_ReplacesWithoutDuplicatesAndPreservesNoOpRedo()
    {
        RunSta(() =>
        {
            var script = new Funscript();
            script.AddOrReplace(new FunscriptAction(0, 0));
            script.AddOrReplace(new FunscriptAction(2000, 100));
            using var timeline = new TimelineControl();
            timeline.LoadScript(script);
            timeline.SetPlayhead(1.2345);
            var changes = 0;
            timeline.ScriptChanged += (_, _) => changes++;
            timeline.InsertPositionAtPlayhead(30);
            CollectionAssert.AreEqual(new[] { new FunscriptAction(0, 0), new FunscriptAction(1234, 30), new FunscriptAction(2000, 100) }, script.Actions.ToArray());
            Assert.AreEqual(1, changes);
            Assert.AreEqual(1.2345, timeline.PlayheadSeconds);
            timeline.History.MarkSaved();
            timeline.InsertPositionAtPlayhead(70);
            Assert.AreEqual(3, script.Actions.Count);
            Assert.AreEqual(70, script.Actions[1].Pos);
            Assert.IsTrue(timeline.Undo());
            Assert.IsFalse(timeline.History.IsDirty);
            Assert.IsTrue(timeline.History.CanRedo);
            var beforeNoOp = changes;
            timeline.InsertPositionAtPlayhead(30);
            Assert.AreEqual(beforeNoOp, changes);
            Assert.IsTrue(timeline.History.CanRedo);
            Assert.IsFalse(timeline.History.IsDirty);
            Assert.IsTrue(timeline.Redo());
            Assert.AreEqual(70, script.Actions[1].Pos);
            Assert.IsTrue(timeline.Undo());
            timeline.InsertPositionAtPlayhead(90);
            Assert.IsFalse(timeline.History.CanRedo);
            Assert.IsTrue(timeline.Undo());
            Assert.IsTrue(timeline.Undo());
            Assert.AreEqual(2, script.Actions.Count);
        });
    }

    [TestMethod]
    public void Insertion_ValidatesRangeAndClampsPlayheadOnEmptyDocument()
    {
        RunSta(() =>
        {
            var script = new Funscript();
            using var timeline = new TimelineControl();
            timeline.LoadScript(script);
            Assert.ThrowsException<ArgumentOutOfRangeException>(() => timeline.InsertPositionAtPlayhead(-1));
            Assert.ThrowsException<ArgumentOutOfRangeException>(() => timeline.InsertPositionAtPlayhead(101));
            Assert.AreEqual(0, script.Actions.Count);
            Assert.IsFalse(timeline.History.CanUndo);
            timeline.SetPlayhead(-1);
            timeline.InsertPositionAtPlayhead(0);
            Assert.AreEqual(new FunscriptAction(0, 0), script.Actions[0]);
            timeline.SetPlayhead(100);
            timeline.InsertPositionAtPlayhead(100);
            Assert.AreEqual(new FunscriptAction(60000, 100), script.Actions[1]);
        });
    }

    [TestMethod]
    public void MiddleDrag_PansAndClampsWithoutSeekingOrEditing()
    {
        RunSta(() =>
        {
            using var form = new Form();
            using var timeline = new TimelineControl { Dock = DockStyle.Fill };
            form.Controls.Add(timeline);
            var script = new Funscript();
            script.AddOrReplace(new FunscriptAction(0, 0));
            script.AddOrReplace(new FunscriptAction(60000, 100));
            timeline.LoadScript(script);
            form.Show();
            timeline.PixelsPerSecond = 100;
            timeline.SetPlayhead(12);
            var seeks = 0;
            var changes = 0;
            timeline.SeekRequested += _ => seeks++;
            timeline.ScriptChanged += (_, _) => changes++;
            var scrollbar = timeline.Controls.OfType<HScrollBar>().Single();
            void Mouse(string method, MouseButtons button, int x) =>
                typeof(TimelineControl).GetMethod(method,
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
                    .Invoke(timeline, [new MouseEventArgs(button, 1, x, 30, 0)]);

            Mouse("OnMouseDown", MouseButtons.Middle, 150);
            Assert.IsTrue(timeline.Capture);
            Mouse("OnMouseMove", MouseButtons.Middle, 50);
            Assert.AreEqual(1000, scrollbar.Value);
            Mouse("OnMouseMove", MouseButtons.Middle, -100000);
            Assert.IsTrue(scrollbar.Value > 1000);
            var end = scrollbar.Value;
            Mouse("OnMouseMove", MouseButtons.Middle, -200000);
            Assert.AreEqual(end, scrollbar.Value);
            Mouse("OnMouseMove", MouseButtons.Middle, 100000);
            Assert.AreEqual(0, scrollbar.Value);
            Mouse("OnMouseUp", MouseButtons.Middle, 100000);
            Assert.IsFalse(timeline.Capture);
            Mouse("OnMouseMove", MouseButtons.None, 50);
            Assert.AreEqual(0, scrollbar.Value);

            Mouse("OnMouseDown", MouseButtons.Middle, 150);
            timeline.Capture = false;
            Mouse("OnMouseMove", MouseButtons.None, 50);
            Assert.AreEqual(0, scrollbar.Value);
            Assert.AreEqual(12.0, timeline.PlayheadSeconds);
            Assert.AreEqual(0, seeks);
            Assert.AreEqual(0, changes);
            Assert.IsFalse(timeline.History.CanUndo);
            Assert.AreEqual(2, script.Actions.Count);
            form.Close();
        });
    }

    [TestMethod]
    public void ShiftLeftDrag_PansAndClampsWithoutSeekingOrEditing()
    {
        RunSta(() =>
        {
            using var form = new Form { ClientSize = new Size(1040, 200) };
            using var timeline = new TimelineControl { Dock = DockStyle.Fill };
            form.Controls.Add(timeline);
            var script = new Funscript();
            script.AddOrReplace(new FunscriptAction(0, 0));
            script.AddOrReplace(new FunscriptAction(60000, 100));
            timeline.LoadScript(script);
            form.Show();
            timeline.PixelsPerSecond = 100;
            var scrollbar = timeline.Controls.OfType<HScrollBar>().Single();
            var seeks = 0;
            var changes = 0;
            timeline.SeekRequested += _ => seeks++;
            timeline.ScriptChanged += (_, _) => changes++;
            var start = new MouseEventArgs(MouseButtons.Left, 1, 500, 30, 0);
            var actionPoint = new MouseEventArgs(
                MouseButtons.Left, 1, timeline.Padding.Left,
                timeline.Height - timeline.Padding.Bottom - 24 - 10, 0);

            Assert.IsFalse(timeline.TryStartShiftPan(actionPoint, Keys.Shift));
            Assert.IsFalse(timeline.TryStartShiftPan(start, Keys.None));
            timeline.SetPlayhead(30, followPlayback: true);
            var initialOffset = scrollbar.Value;
            Assert.IsTrue(timeline.TryStartShiftPan(start, Keys.Shift));
            Assert.IsTrue(timeline.Capture);
            Assert.IsTrue(timeline.UpdatePan(400, Keys.Shift));
            Assert.AreEqual(initialOffset + 1000, scrollbar.Value);

            Assert.IsTrue(timeline.UpdatePan(-100000, Keys.Shift));
            Assert.AreEqual(scrollbar.Maximum - scrollbar.LargeChange, scrollbar.Value);
            Assert.IsTrue(timeline.UpdatePan(100000, Keys.Shift));
            Assert.AreEqual(0, scrollbar.Value);
            typeof(TimelineControl).GetMethod("OnMouseUp",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
                .Invoke(timeline, [new MouseEventArgs(MouseButtons.Left, 1, 100000, 30, 0)]);
            Assert.IsFalse(timeline.Capture);

            Assert.AreEqual(100.0, timeline.PixelsPerSecond);
            Assert.AreEqual(30.0, timeline.PlayheadSeconds);
            Assert.AreEqual(0, seeks);
            Assert.AreEqual(0, changes);
            Assert.IsFalse(timeline.History.CanUndo);
            Assert.AreEqual(2, script.Actions.Count);
            form.Close();
        });
    }

    [TestMethod]
    public void ShiftLeftOnActionPoint_StillEditsTheAction()
    {
        RunSta(() =>
        {
            using var form = new Form { ClientSize = new Size(1040, 200) };
            using var timeline = new TimelineControl { Dock = DockStyle.Fill };
            form.Controls.Add(timeline);
            var script = new Funscript();
            script.AddOrReplace(new FunscriptAction(1000, 50));
            script.AddOrReplace(new FunscriptAction(60000, 100));
            timeline.LoadScript(script);
            form.Show();
            timeline.PixelsPerSecond = 100;
            var trackBottom = timeline.Height - timeline.Padding.Bottom - 24 - 10;
            var trackTop = timeline.Padding.Top + 10;
            var x = timeline.Padding.Left + 100;
            var y = (int)Math.Round(trackBottom - (trackBottom - trackTop) / 2.0);
            var down = new MouseEventArgs(MouseButtons.Left, 1, x, y, 0);
            var seeks = 0;
            timeline.SeekRequested += _ => seeks++;

            timeline.HandleMouseDown(down, Keys.Shift);
            Assert.IsFalse(timeline.UpdatePan(x + 20, Keys.Shift));
            typeof(TimelineControl).GetMethod("OnMouseMove",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
                .Invoke(timeline, [new MouseEventArgs(MouseButtons.Left, 1, x + 20, y, 0)]);
            typeof(TimelineControl).GetMethod("OnMouseUp",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
                .Invoke(timeline, [new MouseEventArgs(MouseButtons.Left, 1, x + 20, y, 0)]);

            Assert.AreEqual(1200, script.Actions[0].At);
            Assert.IsTrue(timeline.History.CanUndo);
            Assert.AreEqual(0, seeks);
            form.Close();
        });
    }

    [TestMethod]
    public void ShiftPan_EndsWhenModifierIsReleasedOrCaptureIsLost()
    {
        RunSta(() =>
        {
            using var form = new Form { ClientSize = new Size(1040, 200) };
            using var timeline = new TimelineControl { Dock = DockStyle.Fill };
            form.Controls.Add(timeline);
            timeline.LoadScript(new Funscript());
            form.Show();
            timeline.PixelsPerSecond = 100;
            var scrollbar = timeline.Controls.OfType<HScrollBar>().Single();
            var start = new MouseEventArgs(MouseButtons.Left, 1, 500, 30, 0);

            Assert.IsTrue(timeline.TryStartShiftPan(start, Keys.Shift));
            Assert.IsTrue(timeline.UpdatePan(400, Keys.Shift));
            var offsetAfterMove = scrollbar.Value;
            Assert.IsTrue(timeline.UpdatePan(399, Keys.None));
            Assert.IsFalse(timeline.Capture);
            Assert.IsFalse(timeline.UpdatePan(300, Keys.Shift));
            Assert.AreEqual(offsetAfterMove, scrollbar.Value);

            Assert.IsTrue(timeline.TryStartShiftPan(start, Keys.Shift));
            Assert.IsTrue(timeline.UpdatePan(400, Keys.Shift));
            offsetAfterMove = scrollbar.Value;
            timeline.Capture = false;
            Assert.IsFalse(timeline.Capture);
            Assert.IsFalse(timeline.UpdatePan(300, Keys.Shift));
            Assert.AreEqual(offsetAfterMove, scrollbar.Value);
            form.Close();
        });
    }

    [TestMethod]
    public void PlaybackFollow_RecentersOutsideEitherEdgeWithoutSeekingOrChangingZoom()
    {
        RunSta(() =>
        {
            using var timeline = new TimelineControl { Size = new Size(1040, 200) };
            var script = new Funscript();
            timeline.LoadScript(script);
            timeline.PixelsPerSecond = 100;
            var scrollbar = timeline.Controls.OfType<HScrollBar>().Single();
            var viewportSeconds = (timeline.ClientSize.Width - timeline.Padding.Horizontal - 40.0)
                / timeline.PixelsPerSecond;
            var seeks = 0;
            timeline.SeekRequested += _ => seeks++;
            timeline.SetPlayhead(9.9, followPlayback: true);
            Assert.AreEqual(0, scrollbar.Value);
            timeline.SetPlayhead(10, followPlayback: true);
            Assert.AreEqual((int)Math.Round((10 - viewportSeconds / 2) * 1000), scrollbar.Value);
            timeline.SetPlayhead(14.9, followPlayback: true);
            Assert.AreEqual((int)Math.Round((10 - viewportSeconds / 2) * 1000), scrollbar.Value);
            timeline.SetPlayhead(15, followPlayback: true);
            Assert.AreEqual((int)Math.Round((15 - viewportSeconds / 2) * 1000), scrollbar.Value);
            timeline.SetPlayhead(40);
            Assert.AreEqual((int)Math.Round((15 - viewportSeconds / 2) * 1000), scrollbar.Value);
            timeline.SetPlayhead(59, followPlayback: true);
            Assert.AreEqual((int)Math.Round((60 - viewportSeconds) * 1000), scrollbar.Value);
            timeline.SetPlayhead(1);
            Assert.AreEqual((int)Math.Round((60 - viewportSeconds) * 1000), scrollbar.Value);
            timeline.SetPlayhead(55, followPlayback: true);
            Assert.AreEqual((int)Math.Round((60 - viewportSeconds) * 1000), scrollbar.Value);
            timeline.SetPlayhead(49, followPlayback: true);
            Assert.AreEqual((int)Math.Round((49 - viewportSeconds / 2) * 1000), scrollbar.Value);
            timeline.SetPlayhead(0, followPlayback: true);
            Assert.AreEqual(0, scrollbar.Value);
            Assert.AreEqual(100.0, timeline.PixelsPerSecond);
            Assert.AreEqual(0.0, timeline.PlayheadSeconds);
            Assert.AreEqual(0, seeks);
            Assert.IsFalse(timeline.History.CanUndo);
            Assert.AreEqual(0, script.Actions.Count);
        });
    }

    [TestMethod]
    public void PlaybackFollow_ManualPanSuppressesUntilPlayheadReentersViewport()
    {
        RunSta(() =>
        {
            using var form = new Form { ClientSize = new Size(1040, 200) };
            using var timeline = new TimelineControl { Dock = DockStyle.Fill };
            form.Controls.Add(timeline);
            timeline.LoadScript(new Funscript());
            form.Show();
            timeline.PixelsPerSecond = 100;
            var scrollbar = timeline.Controls.OfType<HScrollBar>().Single();

            timeline.SetPlayhead(30, followPlayback: true);
            Assert.IsTrue(timeline.TryStartShiftPan(
                new MouseEventArgs(MouseButtons.Left, 1, 500, 30, 0), Keys.Shift));
            Assert.IsTrue(timeline.UpdatePan(-500, Keys.Shift));
            typeof(TimelineControl).GetMethod("OnMouseUp",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
                .Invoke(timeline, [new MouseEventArgs(MouseButtons.Left, 1, -500, 30, 0)]);
            var manuallyPannedOffset = scrollbar.Value;
            Assert.IsTrue(manuallyPannedOffset > 30000);
            var viewportSeconds = (timeline.ClientSize.Width - timeline.Padding.Horizontal - 40.0)
                / timeline.PixelsPerSecond;

            timeline.SetPlayhead(31, followPlayback: true);
            Assert.AreEqual(manuallyPannedOffset, scrollbar.Value,
                "Playback follow should not undo the manual pan while the playhead is outside the viewport.");

            timeline.SetPlayhead(manuallyPannedOffset / 1000.0 + 0.1, followPlayback: true);
            Assert.AreEqual(manuallyPannedOffset, scrollbar.Value,
                "Re-entering the viewport should clear suppression without recentering immediately.");

            var playheadOutsideViewport = manuallyPannedOffset / 1000.0 + viewportSeconds + 0.1;
            timeline.SetPlayhead(playheadOutsideViewport, followPlayback: true);
            var expectedFollowOffset = (int)Math.Round((playheadOutsideViewport - viewportSeconds / 2) * 1000);
            Assert.AreEqual(expectedFollowOffset, scrollbar.Value,
                "After re-entry, playback follow should resume when the playhead leaves the viewport again.");
            Assert.AreEqual(100.0, timeline.PixelsPerSecond);
            form.Close();
        });
    }

    private static void RunSta(Action action)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try { action(); }
            catch (Exception exception) { failure = exception; }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (failure is not null) ExceptionDispatchInfo.Capture(failure).Throw();
    }
}
