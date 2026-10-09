namespace ScriptSyncPilot.Common;

/// <summary>A splice delta: at <see cref="Index"/>, <see cref="Removed"/> was replaced by <see cref="Inserted"/>.</summary>
public sealed class ActionEdit
{
    public int Index { get; }
    public IReadOnlyList<FunscriptAction> Removed { get; }
    public IReadOnlyList<FunscriptAction> Inserted { get; }

    private ActionEdit(int index, IReadOnlyList<FunscriptAction> removed, IReadOnlyList<FunscriptAction> inserted)
    {
        Index = index;
        Removed = removed;
        Inserted = inserted;
    }

    public ActionEdit Inverse() => new(Index, Inserted, Removed);

    /// <summary>Returns the smallest contiguous section that differs between two action lists, or null if they are equal.</summary>
    public static ActionEdit? Between(IReadOnlyList<FunscriptAction> before, IReadOnlyList<FunscriptAction> after)
    {
        ArgumentNullException.ThrowIfNull(before);
        ArgumentNullException.ThrowIfNull(after);
        var shorter = Math.Min(before.Count, after.Count);
        var prefix = 0;
        while (prefix < shorter && before[prefix] == after[prefix]) prefix++;
        if (prefix == before.Count && prefix == after.Count) return null;

        var suffix = 0;
        while (suffix < shorter - prefix && before[before.Count - 1 - suffix] == after[after.Count - 1 - suffix]) suffix++;
        return new ActionEdit(
            prefix,
            Slice(before, prefix, before.Count - prefix - suffix),
            Slice(after, prefix, after.Count - prefix - suffix));
    }

    private static IReadOnlyList<FunscriptAction> Slice(IReadOnlyList<FunscriptAction> source, int start, int count)
    {
        var slice = new FunscriptAction[count];
        for (var index = 0; index < count; index++) slice[index] = source[start + index];
        return Array.AsReadOnly(slice);
    }
}

/// <summary>
/// Multilevel undo/redo of action edits, stored as deltas. A saved-state checkpoint tracks dirty state;
/// entry ids are never reused, so branching away from a saved state never looks clean again.
/// </summary>
public sealed class ActionHistory
{
    private readonly Stack<Entry> _undo = new();
    private readonly Stack<Entry> _redo = new();
    private long _lastId;
    private long _savedId;

    public bool CanUndo => _undo.Count > 0;
    public bool CanRedo => _redo.Count > 0;
    public bool IsDirty => CurrentId != _savedId;

    private long CurrentId => _undo.TryPeek(out var top) ? top.Id : 0;

    /// <summary>Records an already applied edit and clears redo. A null (no-op) edit leaves history untouched.</summary>
    public bool Record(ActionEdit? edit)
    {
        if (edit is null) return false;
        _undo.Push(new Entry(edit, ++_lastId));
        _redo.Clear();
        return true;
    }

    /// <summary>Reverts the latest edit in <paramref name="script"/> and returns the applied (inverse) edit, or null if there is nothing to undo.</summary>
    public ActionEdit? Undo(Funscript script)
    {
        ArgumentNullException.ThrowIfNull(script);
        if (!_undo.TryPeek(out var entry)) return null;
        var inverse = entry.Edit.Inverse();
        script.ApplyEdit(inverse);
        _redo.Push(_undo.Pop());
        return inverse;
    }

    /// <summary>Reapplies the latest undone edit in <paramref name="script"/> and returns it, or null if there is nothing to redo.</summary>
    public ActionEdit? Redo(Funscript script)
    {
        ArgumentNullException.ThrowIfNull(script);
        if (!_redo.TryPeek(out var entry)) return null;
        script.ApplyEdit(entry.Edit);
        _undo.Push(_redo.Pop());
        return entry.Edit;
    }

    public void MarkSaved() => _savedId = CurrentId;

    /// <summary>Starts a fresh, clean history for a newly created or opened document.</summary>
    public void Reset()
    {
        _undo.Clear();
        _redo.Clear();
        _savedId = 0;
    }

    private sealed record Entry(ActionEdit Edit, long Id);
}
