using Microsoft.VisualStudio.TestTools.UnitTesting;
using ScriptSyncPilot.Common;
using ScriptSyncPilot.FunscriptEditor;
using ScriptSyncPilot.HandySync;

namespace ScriptSyncPilot.FunscriptEditor.Tests;

[TestClass]
public sealed class HandySyncWaveformColorTests
{
    [TestMethod]
    public void ColumnAggregation_UsesMaximumValidSpeedForOverlappingSegments()
    {
        var actions = new List<FunscriptAction>
        {
            new(0, 0), new(1000, 100), new(2000, 0), new(3000, 50)
        };

        var columns = ScriptWaveformControl.ComputeMaximumSegmentSpeedPerColumn(actions, 3, 3);

        Assert.AreEqual(100d, columns[0]);
        Assert.AreEqual(100d, columns[1]);
        Assert.AreEqual(50d, columns[2]);
    }

    [TestMethod]
    public void ColumnAggregation_SkipsInvalidSegmentsAndLeavesEmptyColumnsNull()
    {
        var actions = new List<FunscriptAction>
        {
            new(0, 10), new(0, 90), new(0, 20)
        };

        var columns = ScriptWaveformControl.ComputeMaximumSegmentSpeedPerColumn(actions, 4, 5);

        Assert.IsTrue(columns.All(value => value is null)); // duplicate timestamps are invalid
        Assert.IsTrue(ScriptWaveformControl.ComputeMaximumSegmentSpeedPerColumn([], 4, 5)
            .All(value => value is null)); // no segments leaves every column empty
    }

    [TestMethod]
    public void ColumnAggregation_ReturnsEmptyResultsForEmptyWidthOrInvalidDuration()
    {
        Assert.AreEqual(0, ScriptWaveformControl.ComputeMaximumSegmentSpeedPerColumn([], 2, 0).Length);
        var columns = ScriptWaveformControl.ComputeMaximumSegmentSpeedPerColumn(
            [new FunscriptAction(0, 0), new FunscriptAction(1000, 100)], double.NaN, 3);
        Assert.IsTrue(columns.All(value => value is null));
    }

    [STATestMethod]
    public void EditorAndHandySync_UseSameCommonMappingForSameRangeAndSpeed()
    {
        using var editor = new TimelineControl();
        using var waveform = new ScriptWaveformControl();
        const double minimum = 0;
        const double maximum = 700;
        const double speed = 150;

        editor.ConfigureSpeedColoring(true, SegmentSpeedScaleMode.Manual, minimum, maximum);
        waveform.ConfigureSpeedColoring(true, SegmentSpeedScaleMode.Manual, minimum, maximum);
        var segment = SegmentSpeedCalculator.Calculate(new FunscriptAction(0, 0), new FunscriptAction(1000, 150));

        var editorMapping = editor.GetSegmentSpeedColor(segment);
        var handyMapping = waveform.GetSpeedColor(speed);

        Assert.IsNotNull(editorMapping);
        Assert.IsNotNull(handyMapping);
        Assert.AreEqual(editorMapping.Value.Position, handyMapping.Value.Position);
        Assert.AreEqual(editorMapping.Value.Color, handyMapping.Value.Color);
    }
}
