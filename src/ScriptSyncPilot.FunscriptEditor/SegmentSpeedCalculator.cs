using ScriptSyncPilot.Common;

namespace ScriptSyncPilot.FunscriptEditor;

/// <summary>Script-space measurements for one adjacent action pair.</summary>
public readonly record struct SegmentSpeed(
    long ElapsedMilliseconds,
    long DistancePercentagePoints,
    double? AveragePercentagePointsPerSecond)
{
    public bool HasValidTiming => ElapsedMilliseconds > 0;
}

/// <summary>Calculates script-space average speed without depending on UI or playback state.</summary>
public static class SegmentSpeedCalculator
{
    public static SegmentSpeed Calculate(FunscriptAction start, FunscriptAction end)
    {
        var elapsedMilliseconds = (long)end.At - start.At;
        var distancePercentagePoints = Math.Abs((long)end.Pos - start.Pos);
        double? averageSpeed = elapsedMilliseconds > 0
            ? distancePercentagePoints * 1000d / elapsedMilliseconds
            : null;

        return new SegmentSpeed(elapsedMilliseconds, distancePercentagePoints, averageSpeed);
    }
}
