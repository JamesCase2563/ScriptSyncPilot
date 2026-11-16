using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ScriptSyncPilot.Common;

/// <summary>A funscript: a time-ordered list of (time, position) actions.</summary>
public sealed class Funscript
{
    private List<FunscriptAction> _actions;
    private readonly Dictionary<string, JsonElement> _extensionData;

    /// <summary>Actions ordered by <see cref="FunscriptAction.At"/>. Read-only view; mutate via Add/Remove/Move/Update.</summary>
    public IReadOnlyList<FunscriptAction> Actions => _actions;

    public Funscript() : this(new List<FunscriptAction>(), new Dictionary<string, JsonElement>())
    {
    }

    private Funscript(List<FunscriptAction> actions, Dictionary<string, JsonElement>? extensionData = null)
    {
        _actions = actions;
        _extensionData = extensionData ?? new Dictionary<string, JsonElement>();
    }

    public static Funscript Load(string path)
    {
        var json = File.ReadAllText(path);
        FunscriptFile doc;
        try
        {
            doc = JsonSerializer.Deserialize<FunscriptFile>(json, JsonOpts)
                  ?? throw new InvalidDataException("Empty or invalid funscript.");
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException("Invalid funscript JSON.", ex);
        }

        var actions = (doc.Actions ?? new List<FunscriptAction>())
            .OrderBy(a => a.At)
            .ToList();
        return new Funscript(actions, doc.ExtensionData);
    }

    public void Save(string path)
        => Save(path, null);

    internal void Save(string path, Action<Stream, string>? writeStagedContent)
    {
        var doc = new FunscriptFile { Actions = _actions, ExtensionData = _extensionData };
        var json = JsonSerializer.Serialize(doc, new JsonSerializerOptions { WriteIndented = true });
        var destination = Path.GetFullPath(path);
        var directory = Path.GetDirectoryName(destination)!;
        Directory.CreateDirectory(directory);

        var temporaryPath = Path.Combine(directory, $".{Path.GetFileName(destination)}-{Guid.NewGuid():N}.tmp");
        var ownsTemporaryFile = false;
        try
        {
            using (var stream = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                ownsTemporaryFile = true;
                if (writeStagedContent is not null)
                {
                    writeStagedContent(stream, json);
                }
                else
                {
                    using var writer = new StreamWriter(stream, new UTF8Encoding(false), leaveOpen: true);
                    writer.Write(json);
                    writer.Flush();
                }

                stream.Flush(flushToDisk: true);
            }

            if (File.Exists(destination))
                File.Replace(temporaryPath, destination, null);
            else
                File.Move(temporaryPath, destination);
            ownsTemporaryFile = false;
        }
        finally
        {
            if (ownsTemporaryFile)
            {
                try { File.Delete(temporaryPath); }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
            }
        }
    }

    /// <summary>Inserts an action keeping the list sorted by time, replacing any action at the same time.</summary>
    public void AddOrReplace(FunscriptAction action)
    {
        var index = _actions.FindIndex(a => a.At == action.At);
        if (index >= 0) _actions[index] = action;
        else _actions.Insert(FindInsertIndex(action.At), action);
    }

    /// <summary>Replaces the action at <paramref name="index"/>, re-sorting if its time moved past a neighbor. Returns the action's new index.</summary>
    public int UpdateAt(int index, FunscriptAction action)
    {
        _actions.RemoveAt(index);
        var newIndex = FindInsertIndex(action.At);
        _actions.Insert(newIndex, action);
        return newIndex;
    }

    public void RemoveAt(int index) => _actions.RemoveAt(index);

    /// <summary>Validates and atomically applies a splice. Throws if the removed section does not match or the resulting actions are invalid.</summary>
    public void ApplyEdit(ActionEdit edit)
    {
        ArgumentNullException.ThrowIfNull(edit);
        if (edit.Index < 0 || edit.Index + edit.Removed.Count > _actions.Count
            || !_actions.GetRange(edit.Index, edit.Removed.Count).SequenceEqual(edit.Removed))
            throw new InvalidOperationException("The edit does not match the current actions.");
        var candidate = new List<FunscriptAction>(_actions);
        candidate.RemoveRange(edit.Index, edit.Removed.Count);
        candidate.InsertRange(edit.Index, edit.Inserted);
        ReplaceActionsAtomically(candidate);
    }

    /// <summary>Atomically replaces the action list after validating a complete sorted candidate. Preserves root extension data and returns false for a no-op.</summary>
    public bool ReplaceActionsAtomically(IEnumerable<FunscriptAction> actions)
    {
        ArgumentNullException.ThrowIfNull(actions);
        var replacement = actions.ToList();
        for (var index = 0; index < replacement.Count; index++)
        {
            var action = replacement[index];
            if (action.At < 0 || action.Pos is < 0 or > 100
                || (index > 0 && replacement[index - 1].At >= action.At))
                throw new ArgumentException("Actions must have nonnegative timestamps, valid positions, and strictly increasing times.", nameof(actions));
        }

        if (_actions.SequenceEqual(replacement)) return false;
        _actions = replacement;
        return true;
    }

    private int FindInsertIndex(int at)
    {
        var index = _actions.FindIndex(a => a.At > at);
        return index < 0 ? _actions.Count : index;
    }

    /// <summary>
    /// Handy's HSSP protocol currently only accepts a hosted CSV/funscript URL
    /// (the inline csv/funscript setup bodies are defined in the API schema but
    /// commented out server-side), so scripts always go through the hosting
    /// upload endpoint as CSV.
    /// </summary>
    public string ToCsv()
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine(); // reserved metadata line, left blank
        foreach (var a in _actions)
            sb.Append(a.At).Append(',').Append(a.Pos).AppendLine();
        return sb.ToString();
    }

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    private sealed class FunscriptFile
    {
        [JsonPropertyName("actions")]
        public List<FunscriptAction>? Actions { get; set; }

        [JsonExtensionData]
        public Dictionary<string, JsonElement>? ExtensionData { get; set; }
    }
}

public readonly record struct FunscriptAction(
    [property: JsonPropertyName("at")] int At,
    [property: JsonPropertyName("pos")] int Pos);
