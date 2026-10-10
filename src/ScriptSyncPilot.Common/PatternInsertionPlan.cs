namespace ScriptSyncPilot.Common;

public enum PatternConflictChoice
{
    Stop,
    Replace,
    Merge,
}

public enum PatternMergeChoice
{
    KeepExisting,
    UseNew,
}

public readonly record struct PatternValueCollision(FunscriptAction Existing, FunscriptAction Generated);

public sealed record PatternInsertionCandidate(
    PatternConflictChoice Choice,
    IReadOnlyList<FunscriptAction> Actions,
    bool HasChanges);

public sealed class PatternInsertionPlan
{
    private readonly IReadOnlyList<FunscriptAction> _insertableActions;

    public int StartMs { get; }
    public int EndMs { get; }
    public IReadOnlyList<FunscriptAction> OriginalActions { get; }
    public IReadOnlyList<FunscriptAction> GeneratedActions { get; }
    public IReadOnlyList<FunscriptAction> ConflictingActions { get; }
    public IReadOnlyList<PatternValueCollision> Collisions { get; }
    public bool HasConflicts => ConflictingActions.Count > 0;
    public bool RequiresMergeChoice => Collisions.Count > 0;
    public bool PreservesStartBoundary { get; }
    public bool PreservesEndBoundary { get; }

    private PatternInsertionPlan(FunscriptAction[] original, FunscriptAction[] generated, int startMs, int endMs)
    {
        StartMs = startMs;
        EndMs = endMs;
        OriginalActions = Array.AsReadOnly(original);
        GeneratedActions = Array.AsReadOnly(generated);
        PreservesStartBoundary = original.Any(action => action.At == startMs);
        PreservesEndBoundary = original.Any(action => action.At == endMs);
        ConflictingActions = Array.AsReadOnly(original.Where(IsInterior).ToArray());
        _insertableActions = Array.AsReadOnly(generated.Where(action =>
            !(PreservesStartBoundary && action.At == startMs)
            && !(PreservesEndBoundary && action.At == endMs)).ToArray());

        var collisions = new List<PatternValueCollision>();
        var originalIndex = 0;
        foreach (var action in _insertableActions)
        {
            while (originalIndex < original.Length && original[originalIndex].At < action.At) originalIndex++;
            if (originalIndex < original.Length && original[originalIndex].At == action.At
                && original[originalIndex].Pos != action.Pos)
                collisions.Add(new PatternValueCollision(original[originalIndex], action));
        }
        Collisions = collisions.AsReadOnly();
    }

    public static PatternInsertionPlan Create(IReadOnlyList<FunscriptAction> existingActions, GeneratedPattern pattern)
    {
        ArgumentNullException.ThrowIfNull(existingActions);
        ArgumentNullException.ThrowIfNull(pattern);
        ArgumentNullException.ThrowIfNull(pattern.Actions);
        var original = existingActions.ToArray();
        var generated = pattern.Actions.ToArray();
        ValidateActions(original, nameof(existingActions));
        ValidateActions(generated, nameof(pattern));
        if (pattern.StartMs < 0 || pattern.EndMs <= pattern.StartMs
            || generated.Length < 2 || generated.Length > PatternGenerator.MaximumActionCount
            || generated[0].At != pattern.StartMs || generated[^1].At != pattern.EndMs)
            throw new ArgumentException("Generated actions must span a valid section and fit the generated action limit.", nameof(pattern));

        return new PatternInsertionPlan(original, generated, pattern.StartMs, pattern.EndMs);
    }

    public PatternInsertionCandidate Resolve(PatternConflictChoice choice, PatternMergeChoice mergeChoice = PatternMergeChoice.KeepExisting)
    {
        if (!Enum.IsDefined(choice)) throw new ArgumentOutOfRangeException(nameof(choice));
        if (!Enum.IsDefined(mergeChoice)) throw new ArgumentOutOfRangeException(nameof(mergeChoice));
        if (choice == PatternConflictChoice.Stop)
        {
            var stopAt = ConflictingActions.Count > 0 ? ConflictingActions[0].At : EndMs + 1L;
            var prefix = _insertableActions.Where(action => action.At < stopAt).ToArray();
            var stopped = OriginalActions.Concat(prefix).OrderBy(action => action.At).ToArray();
            return new PatternInsertionCandidate(choice, Array.AsReadOnly(stopped), !OriginalActions.SequenceEqual(stopped));
        }

        var retained = choice == PatternConflictChoice.Replace
            ? OriginalActions.Where(action => !IsInterior(action)).ToArray()
            : OriginalActions.ToArray();
        var result = new List<FunscriptAction>();
        var existingIndex = 0;
        var generatedIndex = 0;
        while (existingIndex < retained.Length || generatedIndex < _insertableActions.Count)
        {
            if (generatedIndex == _insertableActions.Count
                || (existingIndex < retained.Length && retained[existingIndex].At < _insertableActions[generatedIndex].At))
            {
                result.Add(retained[existingIndex++]);
            }
            else if (existingIndex == retained.Length || _insertableActions[generatedIndex].At < retained[existingIndex].At)
            {
                result.Add(_insertableActions[generatedIndex++]);
            }
            else
            {
                result.Add(mergeChoice == PatternMergeChoice.UseNew
                    ? _insertableActions[generatedIndex] : retained[existingIndex]);
                existingIndex++;
                generatedIndex++;
            }
        }

        return new PatternInsertionCandidate(choice, result.AsReadOnly(), !OriginalActions.SequenceEqual(result));
    }

    private bool IsInterior(FunscriptAction action) => action.At > StartMs && action.At < EndMs;

    private static void ValidateActions(FunscriptAction[] actions, string parameterName)
    {
        for (var index = 0; index < actions.Length; index++)
        {
            if (actions[index].At < 0 || actions[index].Pos is < 0 or > 100
                || (index > 0 && actions[index - 1].At >= actions[index].At))
                throw new ArgumentException("Actions must be valid, sorted, and have unique timestamps.", parameterName);
        }
    }
}