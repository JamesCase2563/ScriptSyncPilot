using ScriptSyncPilot.Common;

namespace ScriptSyncPilot.FunscriptEditor;

public enum SegmentSpeedScaleMode
{
    FitToScript,
    Manual,
}

public enum SegmentSpeedClamp
{
    None,
    Low,
    High,
}

public readonly record struct SegmentSpeedColorPosition(double NormalizedPosition, SegmentSpeedClamp Clamp);

/// <summary>A script-space speed interval used only to map segments to colors.</summary>
public readonly record struct SegmentSpeedColorRange
{
    public double Minimum { get; }
    public double Maximum { get; }

    public SegmentSpeedColorRange(double minimum, double maximum)
    {
        if (!double.IsFinite(minimum) || !double.IsFinite(maximum) || minimum < 0 || minimum >= maximum)
            throw new ArgumentOutOfRangeException(nameof(maximum), "The speed range must be finite, non-negative, and have minimum below maximum.");

        Minimum = minimum;
        Maximum = maximum;
    }

    public SegmentSpeedColorPosition Map(double speed)
    {
        if (!double.IsFinite(speed) || speed < 0)
            throw new ArgumentOutOfRangeException(nameof(speed), "Speed must be a finite, non-negative value.");

        if (speed < Minimum) return new SegmentSpeedColorPosition(0, SegmentSpeedClamp.Low);
        if (speed > Maximum) return new SegmentSpeedColorPosition(1, SegmentSpeedClamp.High);
        var linearPosition = (speed - Minimum) / (Maximum - Minimum);
        return new SegmentSpeedColorPosition(Math.Sqrt(linearPosition), SegmentSpeedClamp.None);
    }
}

/// <summary>Creates a stable color range from all valid segments in a script.</summary>
public static class SegmentSpeedScaleCalculator
{
    public static SegmentSpeedColorRange FitToScript(IReadOnlyList<FunscriptAction> actions)
    {
        ArgumentNullException.ThrowIfNull(actions);
        var speeds = new List<double>(Math.Max(0, actions.Count - 1));
        for (var i = 0; i < actions.Count - 1; i++)
        {
            var segment = SegmentSpeedCalculator.Calculate(actions[i], actions[i + 1]);
            if (segment.HasValidTiming && segment.AveragePercentagePointsPerSecond is { } speed && double.IsFinite(speed))
                speeds.Add(speed);
        }

        if (speeds.Count == 0) return new SegmentSpeedColorRange(0, 100);

        speeds.Sort();
        var nearestRankIndex = (int)Math.Ceiling(0.99 * speeds.Count) - 1;
        var percentile = speeds[Math.Clamp(nearestRankIndex, 0, speeds.Count - 1)];
        var maximum = Math.Max(100, Math.Ceiling(percentile / 100) * 100);
        return new SegmentSpeedColorRange(0, maximum);
    }
}
