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
    public void PlaybackFollow_RecentersOutsideEitherEdgeWithoutSeekingOrChangingZoom()
    {
        RunSta(() =>
        {
            using var timeline = new TimelineControl { Size = new Size(1040, 200) };
            var script = new Funscript();
            timeline.LoadScript(script);
            timeline.PixelsPerSecond = 100;
            var scrollbar = timeline.Controls.OfType<HScrollBar>().Single();
            var seeks = 0;
            timeline.SeekRequested += _ => seeks++;
            timeline.SetPlayhead(9.9, followPlayback: true);
            Assert.AreEqual(0, scrollbar.Value);
            timeline.SetPlayhead(10, followPlayback: true);
            Assert.AreEqual(5000, scrollbar.Value);
            timeline.SetPlayhead(14.9, followPlayback: true);
            Assert.AreEqual(5000, scrollbar.Value);
            timeline.SetPlayhead(15, followPlayback: true);
            Assert.AreEqual(10000, scrollbar.Value);
            timeline.SetPlayhead(40);
            Assert.AreEqual(10000, scrollbar.Value);
            timeline.SetPlayhead(59, followPlayback: true);
            Assert.AreEqual(50000, scrollbar.Value);
            timeline.SetPlayhead(1);
            Assert.AreEqual(50000, scrollbar.Value);
            timeline.SetPlayhead(50, followPlayback: true);
            Assert.AreEqual(50000, scrollbar.Value);
            timeline.SetPlayhead(49, followPlayback: true);
            Assert.AreEqual(44000, scrollbar.Value);
            timeline.SetPlayhead(0, followPlayback: true);
            Assert.AreEqual(0, scrollbar.Value);
            Assert.AreEqual(100.0, timeline.PixelsPerSecond);
            Assert.AreEqual(0.0, timeline.PlayheadSeconds);
            Assert.AreEqual(0, seeks);
            Assert.IsFalse(timeline.History.CanUndo);
            Assert.AreEqual(0, script.Actions.Count);
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