using ScriptSyncPilot.Common;

namespace ScriptSyncPilot.FunscriptEditor;

/// <summary>Script-space measurements for one adjacent action pair.</summary>
public readonly record struct SegmentSpeed(
    long ElapsedMilliseconds,
    long DistancePercentagePoints,
    double? AveragePercentagePointsPerSecond,
    SegmentTimingStatus TimingStatus)
{
    public bool HasValidTiming => TimingStatus == SegmentTimingStatus.Valid;
}

public enum SegmentTimingStatus
{
    Valid,
    DuplicateTimestamp,
    DecreasingTimestamp,
}

/// <summary>Calculates script-space average speed without depending on UI or playback state.</summary>
public static class SegmentSpeedCalculator
{
    public static SegmentSpeed Calculate(FunscriptAction start, FunscriptAction end)
    {
        var elapsedMilliseconds = (long)end.At - start.At;
        var distancePercentagePoints = Math.Abs((long)end.Pos - start.Pos);
        var timingStatus = elapsedMilliseconds switch
        {
            > 0 => SegmentTimingStatus.Valid,
            0 => SegmentTimingStatus.DuplicateTimestamp,
            _ => SegmentTimingStatus.DecreasingTimestamp,
        };
        double? averageSpeed = elapsedMilliseconds > 0
            ? distancePercentagePoints * 1000d / elapsedMilliseconds
            : null;

        return new SegmentSpeed(elapsedMilliseconds, distancePercentagePoints, averageSpeed, timingStatus);
    }
}
