namespace ScriptSyncPilot.Common;

public enum PatternProfile
{
    Triangle,
    FastUpSlowDown,
    SlowedDirectionChanges,
}

public enum PatternStartMode
{
    PatternStart,
    ContinueExisting,
}

public sealed record PatternGenerationOptions(decimal StartSeconds, decimal DurationSeconds, int CycleCount)
{
    public PatternProfile Profile { get; init; } = PatternProfile.Triangle;
    public PatternStartMode StartMode { get; init; } = PatternStartMode.PatternStart;
    public int MinimumPosition { get; init; }
    public int MaximumPosition { get; init; } = 100;
    public decimal RiseFraction { get; init; } = 0.25m;
    public decimal MaximumDurationSeconds { get; init; } = 3 * 60 * 60;
}

public sealed record GeneratedPattern(
    IReadOnlyList<FunscriptAction> Actions,
    int StartMs,
    int EndMs,
    decimal EffectiveCyclesPerMinute,
    decimal StartPhase,
    bool UsedPatternStartFallback);

public static class PatternGenerator
{
    public const int MaximumActionCount = 100_000;
    private const double ErrorTolerance = 1 + 1e-9;

    public static decimal ObservedCyclesPerMinute(decimal observedCycles, decimal observedSeconds)
    {
        ValidateObservation(observedCycles, observedSeconds);
        return checked(60m * observedCycles / observedSeconds);
    }

    public static int SuggestCycleCount(decimal observedCycles, decimal observedSeconds, decimal outputSeconds)
    {
        ValidateObservation(observedCycles, observedSeconds);
        if (outputSeconds <= 0) throw new ArgumentOutOfRangeException(nameof(outputSeconds));
        var count = Math.Max(1m, Round(checked(observedCycles * outputSeconds / observedSeconds)));
        if (count > (MaximumActionCount - 1) / 2)
            throw new ArgumentException("Suggested cycle count exceeds the generated action limit.", nameof(outputSeconds));
        return (int)count;
    }

    public static decimal DurationForCycles(decimal observedCycles, decimal observedSeconds, int cycleCount)
    {
        ValidateObservation(observedCycles, observedSeconds);
        if (cycleCount <= 0) throw new ArgumentOutOfRangeException(nameof(cycleCount));
        return checked(observedSeconds * cycleCount / observedCycles);
    }

    public static GeneratedPattern Generate(
        PatternGenerationOptions options,
        IReadOnlyList<FunscriptAction>? existingActions = null)
    {
        ArgumentNullException.ThrowIfNull(options);
        ValidateOptions(options);
        var startMs = QuantizeMilliseconds(options.StartSeconds);
        var durationMs = QuantizeMilliseconds(options.DurationSeconds);
        if (durationMs == 0) throw new ArgumentException("Duration must round to at least one millisecond.", nameof(options));
        if ((long)startMs + durationMs > int.MaxValue)
            throw new ArgumentException("Pattern endpoint exceeds the timestamp limit.", nameof(options));

        var phase = 0m;
        var fallback = false;
        if (options.StartMode == PatternStartMode.ContinueExisting)
        {
            var snapshot = existingActions?.ToArray() ?? [];
            ValidateSnapshot(snapshot);
            fallback = !TryResolvePhase(options, snapshot, startMs, out phase);
        }

        var rise = options.Profile == PatternProfile.FastUpSlowDown ? options.RiseFraction : 0.5m;
        var offsets = new SortedSet<decimal> { 0, 1, NormalizePhase(-phase), NormalizePhase(rise - phase) };
        var requiredCount = (long)(offsets.Count - 1) * options.CycleCount + 1;
        if (requiredCount > MaximumActionCount)
            throw new ArgumentException("Pattern exceeds the generated action limit.", nameof(options));

        var evaluator = new ProfileEvaluator(options, durationMs, phase, rise);
        var mandatory = new List<FunscriptAction>((int)requiredCount);
        for (var cycle = 0; cycle < options.CycleCount; cycle++)
        {
            foreach (var offset in offsets)
            {
                if (offset == 1) continue;
                var elapsed = cycle + offset;
                var at = startMs + (int)Round(durationMs * elapsed / options.CycleCount);
                var pos = QuantizePosition(evaluator.AtPhase(NormalizePhase(phase + offset)));
                AppendMandatory(mandatory, new FunscriptAction(at, pos));
            }
        }
        AppendMandatory(mandatory, new FunscriptAction(startMs + durationMs, QuantizePosition(evaluator.AtPhase(phase))));

        var result = new List<FunscriptAction>(mandatory.Count) { mandatory[0] };
        var totalCount = mandatory.Count;
        for (var index = 1; index < mandatory.Count; index++)
        {
            var pending = new Stack<(FunscriptAction Left, FunscriptAction Right)>();
            pending.Push((mandatory[index - 1], mandatory[index]));
            while (pending.TryPop(out var segment))
            {
                var leftTime = segment.Left.At - startMs;
                var rightTime = segment.Right.At - startMs;
                if (Math.Abs(evaluator.AtTime(leftTime) - segment.Left.Pos) > ErrorTolerance
                    || Math.Abs(evaluator.AtTime(rightTime) - segment.Right.Pos) > ErrorTolerance)
                    throw new ArgumentException("Mandatory point cannot meet the quantized precision requirement.", nameof(options));

                if (evaluator.MaximumError(leftTime, segment.Left.Pos, rightTime, segment.Right.Pos) <= ErrorTolerance)
                {
                    result.Add(segment.Right);
                    continue;
                }

                if (rightTime - leftTime <= 1)
                    throw new ArgumentException("Millisecond resolution cannot represent this pattern accurately.", nameof(options));
                if (++totalCount > MaximumActionCount)
                    throw new ArgumentException("Adaptive sampling exceeds the generated action limit.", nameof(options));

                var middleTime = (int)Round(((decimal)leftTime + rightTime) / 2);
                var middle = new FunscriptAction(startMs + middleTime, QuantizePosition(evaluator.AtTime(middleTime)));
                pending.Push((middle, segment.Right));
                pending.Push((segment.Left, middle));
            }
        }

        return new GeneratedPattern(result.AsReadOnly(), startMs, startMs + durationMs,
            60_000m * options.CycleCount / durationMs, phase, fallback);
    }

    private static void ValidateObservation(decimal cycles, decimal seconds)
    {
        if (cycles <= 0) throw new ArgumentOutOfRangeException(nameof(cycles));
        if (seconds <= 0) throw new ArgumentOutOfRangeException(nameof(seconds));
    }

    private static void ValidateOptions(PatternGenerationOptions options)
    {
        if (!Enum.IsDefined(options.Profile) || !Enum.IsDefined(options.StartMode))
            throw new ArgumentException("Unknown profile or start mode.", nameof(options));
        if (options.StartSeconds < 0 || options.DurationSeconds <= 0 || options.CycleCount <= 0)
            throw new ArgumentException("Start must be nonnegative; duration and cycle count must be positive.", nameof(options));
        if (options.MaximumDurationSeconds <= 0 || options.DurationSeconds > options.MaximumDurationSeconds)
            throw new ArgumentException("Duration exceeds the configured section limit.", nameof(options));
        if (options.MinimumPosition < 0 || options.MaximumPosition > 100 || options.MinimumPosition >= options.MaximumPosition)
            throw new ArgumentException("Position range must satisfy 0 <= minimum < maximum <= 100.", nameof(options));
        if (options.RiseFraction <= 0 || options.RiseFraction >= 1)
            throw new ArgumentException("Rise fraction must lie strictly between zero and one.", nameof(options));
    }

    private static int QuantizeMilliseconds(decimal seconds)
    {
        if (seconds > int.MaxValue / 1000m)
            throw new ArgumentException("Time exceeds the timestamp limit.", nameof(seconds));
        return (int)Round(seconds * 1000m);
    }

    private static decimal Round(decimal value) => decimal.Round(value, 0, MidpointRounding.AwayFromZero);
    private static int QuantizePosition(double value) => (int)Math.Round(value, MidpointRounding.AwayFromZero);
    private static decimal NormalizePhase(decimal phase) => phase - decimal.Floor(phase);

    private static void AppendMandatory(List<FunscriptAction> actions, FunscriptAction action)
    {
        if (actions.Count > 0 && action.At <= actions[^1].At)
            throw new ArgumentException("Distinct mandatory points collapse to the same millisecond.");
        actions.Add(action);
    }

    private static void ValidateSnapshot(FunscriptAction[] actions)
    {
        for (var index = 0; index < actions.Length; index++)
        {
            if (actions[index].At < 0 || actions[index].Pos is < 0 or > 100
                || (index > 0 && actions[index - 1].At >= actions[index].At))
                throw new ArgumentException("Continuation actions must be valid, sorted, and have unique timestamps.", nameof(actions));
        }
    }

    private static bool TryResolvePhase(PatternGenerationOptions options, FunscriptAction[] actions, int startMs, out decimal phase)
    {
        phase = 0;
        if (actions.Length == 0 || startMs < actions[0].At || startMs > actions[^1].At) return false;
        var next = Array.FindIndex(actions, action => action.At >= startMs);
        decimal position;
        int direction;
        if (actions[next].At == startMs)
        {
            position = actions[next].Pos;
            direction = next > 0 ? Math.Sign(actions[next].Pos - actions[next - 1].Pos) : 0;
            if (direction == 0 && next + 1 < actions.Length)
                direction = Math.Sign(actions[next + 1].Pos - actions[next].Pos);
        }
        else
        {
            var previous = actions[next - 1];
            var following = actions[next];
            position = previous.Pos + (decimal)(startMs - previous.At) * (following.Pos - previous.Pos) / (following.At - previous.At);
            direction = Math.Sign(following.Pos - previous.Pos);
        }

        if (position < options.MinimumPosition || position > options.MaximumPosition) return false;
        var normalized = (position - options.MinimumPosition) / (options.MaximumPosition - options.MinimumPosition);
        if (normalized == 0) return true;
        var rise = options.Profile == PatternProfile.FastUpSlowDown ? options.RiseFraction : 0.5m;
        if (normalized == 1) { phase = rise; return true; }
        if (options.Profile == PatternProfile.SlowedDirectionChanges)
        {
            var ascending = (decimal)(Math.Acos(1 - 2 * (double)normalized) / (2 * Math.PI));
            phase = direction < 0 ? 1 - ascending : ascending;
        }
        else
        {
            phase = direction < 0 ? 1 - normalized * (1 - rise) : normalized * rise;
        }
        return true;
    }

    private sealed class ProfileEvaluator(PatternGenerationOptions options, int durationMs, decimal phase, decimal rise)
    {
        private readonly double _rate = (double)options.CycleCount / durationMs;
        private readonly double _phase = (double)phase;
        private readonly int _range = options.MaximumPosition - options.MinimumPosition;

        public double AtPhase(decimal normalizedPhase)
        {
            if (options.Profile == PatternProfile.SlowedDirectionChanges)
                return AtNormalizedPhase((double)normalizedPhase);
            var value = normalizedPhase <= rise ? normalizedPhase / rise : (1 - normalizedPhase) / (1 - rise);
            return (double)(options.MinimumPosition + _range * value);
        }

        public double AtTime(double time) => AtNormalizedPhase(_phase + time * _rate);

        private double AtNormalizedPhase(double normalizedPhase)
        {
            var fraction = normalizedPhase - Math.Floor(normalizedPhase);
            var value = options.Profile == PatternProfile.SlowedDirectionChanges
                ? (1 - Math.Cos(2 * Math.PI * fraction)) / 2
                : fraction <= (double)rise ? fraction / (double)rise : (1 - fraction) / (1 - (double)rise);
            return options.MinimumPosition + _range * value;
        }

        public double MaximumError(int leftTime, int leftPosition, int rightTime, int rightPosition)
        {
            var slope = (double)(rightPosition - leftPosition) / (rightTime - leftTime);
            var maximum = Math.Max(ErrorAt(leftTime), ErrorAt(rightTime));
            var firstCycle = (int)Math.Floor(_phase + leftTime * _rate) - 1;
            var lastCycle = (int)Math.Ceiling(_phase + rightTime * _rate) + 1;
            for (var cycle = firstCycle; cycle <= lastCycle; cycle++)
            {
                CheckPhase(cycle);
                CheckPhase(cycle + (double)rise);
                if (options.Profile != PatternProfile.SlowedDirectionChanges) continue;
                var ratio = slope / (_range * Math.PI * _rate);
                if (Math.Abs(ratio) > 1) continue;
                var rootPhase = Math.Asin(ratio) / (2 * Math.PI);
                CheckPhase(cycle + rootPhase);
                CheckPhase(cycle + 0.5 - rootPhase);
            }
            return maximum;

            double ErrorAt(double time) => Math.Abs(AtTime(time) - (leftPosition + slope * (time - leftTime)));

            void CheckPhase(double absolutePhase)
            {
                var time = (absolutePhase - _phase) / _rate;
                if (time > leftTime && time < rightTime)
                    maximum = Math.Max(maximum, ErrorAt(time));
            }
        }
    }
}