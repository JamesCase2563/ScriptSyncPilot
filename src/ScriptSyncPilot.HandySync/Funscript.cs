using System.Text.Json;
using System.Text.Json.Serialization;

namespace ScriptSyncPilot.HandySync;

/// <summary>A parsed funscript: a sorted list of (time, position) actions.</summary>
public sealed class Funscript
{
    public IReadOnlyList<FunscriptAction> Actions { get; }

    private Funscript(List<FunscriptAction> actions) => Actions = actions;

    public static Funscript Load(string path)
    {
        var json = File.ReadAllText(path);
        var doc = JsonSerializer.Deserialize<FunscriptFile>(json, JsonOpts)
                  ?? throw new InvalidDataException("Empty or invalid funscript.");
        if (doc.Actions is null || doc.Actions.Count == 0)
            throw new InvalidDataException("Funscript has no actions.");

        var actions = doc.Actions
            .OrderBy(a => a.At)
            .ToList();
        return new Funscript(actions);
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
        foreach (var a in Actions)
            sb.Append(a.At).Append(',').Append(a.Pos).Append('\n');
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
        public List<FunscriptAction>? Actions { get; set; }
    }
}

public readonly record struct FunscriptAction(
    [property: JsonPropertyName("at")] int At,
    [property: JsonPropertyName("pos")] int Pos);
