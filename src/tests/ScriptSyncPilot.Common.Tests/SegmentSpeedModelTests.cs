using Microsoft.VisualStudio.TestTools.UnitTesting;
using ScriptSyncPilot.Common;

namespace ScriptSyncPilot.Common.Tests;

[TestClass]
public sealed class SegmentSpeedModelTests
{
    [TestMethod]
    public void SegmentCalculator_ReturnsDistanceAverageAndInvalidTimingStatus()
    {
        var rising = SegmentSpeedCalculator.Calculate(new FunscriptAction(0, 20), new FunscriptAction(1000, 70));
        var falling = SegmentSpeedCalculator.Calculate(new FunscriptAction(1000, 90), new FunscriptAction(2500, 15));
        var still = SegmentSpeedCalculator.Calculate(new FunscriptAction(0, 42), new FunscriptAction(2000, 42));
        var duplicate = SegmentSpeedCalculator.Calculate(new FunscriptAction(500, 0), new FunscriptAction(500, 100));
        var decreasing = SegmentSpeedCalculator.Calculate(new FunscriptAction(900, 0), new FunscriptAction(400, 50));

        Assert.AreEqual(50L, rising.DistancePercentagePoints);
        Assert.AreEqual(50d, rising.AveragePercentagePointsPerSecond);
        Assert.AreEqual(75L, falling.DistancePercentagePoints);
        Assert.AreEqual(50d, falling.AveragePercentagePointsPerSecond);
        Assert.AreEqual(0d, still.AveragePercentagePointsPerSecond);
        Assert.AreEqual(SegmentTimingStatus.DuplicateTimestamp, duplicate.TimingStatus);
        Assert.IsNull(duplicate.AveragePercentagePointsPerSecond);
        Assert.AreEqual(SegmentTimingStatus.DecreasingTimestamp, decreasing.TimingStatus);
        Assert.IsNull(decreasing.AveragePercentagePointsPerSecond);
    }

    [TestMethod]
    public void FitToScript_UsesNearestRankP99AndIgnoresInvalidSegments()
    {
        var actions = new List<FunscriptAction> { new(0, 0) };
        var at = 0;
        var position = 0;
        for (var i = 0; i < 100; i++)
        {
            at += 1000;
            position = 100 - position;
            actions.Add(new FunscriptAction(at, position));
        }

        at += 100;
        position = 100 - position;
        actions.Add(new FunscriptAction(at, position)); // one 1000 pp/s outlier (under one percent)
        actions.Add(new FunscriptAction(at, 100 - position)); // duplicate timestamp: excluded from fit

        var range = SegmentSpeedScaleCalculator.FitToScript(actions);

        Assert.AreEqual(0d, range.Minimum);
        Assert.AreEqual(100d, range.Maximum);
    }

    [TestMethod]
    public void MapColor_AppliesSqrtTransferClampsAndReturnsJetPaletteRgb()
    {
        var range = new SegmentSpeedColorRange(0, 700);

        var mapped = range.MapColor(150);
        Assert.AreEqual(Math.Sqrt(150d / 700), mapped.Position.NormalizedPosition, 0.000001);
        Assert.AreEqual(SegmentSpeedClamp.None, mapped.Position.Clamp);
        Assert.AreEqual(new RgbColor(0, 0, 128), SegmentSpeedPalette.GetColor(0));
        Assert.AreEqual(new RgbColor(128, 0, 0), SegmentSpeedPalette.GetColor(1));

        var high = range.MapColor(900);
        Assert.AreEqual(SegmentSpeedClamp.High, high.Position.Clamp);
        Assert.AreEqual(1d, high.Position.NormalizedPosition);

        var low = new SegmentSpeedColorRange(200, 700).MapColor(100);
        Assert.AreEqual(SegmentSpeedClamp.Low, low.Position.Clamp);
        Assert.AreEqual(0d, low.Position.NormalizedPosition);
    }
}
