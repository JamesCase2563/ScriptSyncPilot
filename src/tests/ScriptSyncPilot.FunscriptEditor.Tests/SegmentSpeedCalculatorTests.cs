using Microsoft.VisualStudio.TestTools.UnitTesting;
using ScriptSyncPilot.Common;
using ScriptSyncPilot.FunscriptEditor;

namespace ScriptSyncPilot.FunscriptEditor.Tests;

[TestClass]
public sealed class SegmentSpeedCalculatorTests
{
    [TestMethod]
    public void Calculate_ReportsElapsedDistanceAndAverageSpeed()
    {
        var result = SegmentSpeedCalculator.Calculate(
            new FunscriptAction(100, 20),
            new FunscriptAction(1100, 70));

        Assert.AreEqual(1000L, result.ElapsedMilliseconds);
        Assert.AreEqual(50L, result.DistancePercentagePoints);
        Assert.AreEqual(50d, result.AveragePercentagePointsPerSecond);
        Assert.AreEqual(SegmentTimingStatus.Valid, result.TimingStatus);
        Assert.IsTrue(result.HasValidTiming);
    }

    [TestMethod]
    public void Calculate_FallingPositionUsesAbsoluteDistance()
    {
        var result = SegmentSpeedCalculator.Calculate(
            new FunscriptAction(1000, 90),
            new FunscriptAction(2500, 15));

        Assert.AreEqual(1500L, result.ElapsedMilliseconds);
        Assert.AreEqual(75L, result.DistancePercentagePoints);
        Assert.AreEqual(50d, result.AveragePercentagePointsPerSecond);
    }

    [TestMethod]
    public void Calculate_ZeroDistanceWithPositiveDurationHasZeroSpeed()
    {
        var result = SegmentSpeedCalculator.Calculate(
            new FunscriptAction(100, 42),
            new FunscriptAction(2100, 42));

        Assert.AreEqual(2000L, result.ElapsedMilliseconds);
        Assert.AreEqual(0L, result.DistancePercentagePoints);
        Assert.AreEqual(0d, result.AveragePercentagePointsPerSecond);
        Assert.AreEqual(SegmentTimingStatus.Valid, result.TimingStatus);
    }

    [TestMethod]
    public void Calculate_DuplicateTimestampIsInvalidAndHasNoSpeed()
    {
        var result = SegmentSpeedCalculator.Calculate(
            new FunscriptAction(500, 10),
            new FunscriptAction(500, 90));

        Assert.AreEqual(0L, result.ElapsedMilliseconds);
        Assert.AreEqual(80L, result.DistancePercentagePoints);
        Assert.IsNull(result.AveragePercentagePointsPerSecond);
        Assert.AreEqual(SegmentTimingStatus.DuplicateTimestamp, result.TimingStatus);
        Assert.IsFalse(result.HasValidTiming);
    }

    [TestMethod]
    public void Calculate_DecreasingTimestampIsInvalidAndHasNoSpeed()
    {
        // Synthetic pair: loaded scripts are sorted, so decreasing input is exercised at the calculator boundary.
        var result = SegmentSpeedCalculator.Calculate(
            new FunscriptAction(900, 10),
            new FunscriptAction(400, 60));

        Assert.AreEqual(-500L, result.ElapsedMilliseconds);
        Assert.AreEqual(50L, result.DistancePercentagePoints);
        Assert.IsNull(result.AveragePercentagePointsPerSecond);
        Assert.AreEqual(SegmentTimingStatus.DecreasingTimestamp, result.TimingStatus);
        Assert.IsFalse(result.HasValidTiming);
    }
}
