using System.Text.Json;

namespace ScriptSyncPilot.FunscriptEditor;

/// <summary>Small persisted settings blob for the editor, separate from any runtime session state.</summary>
public sealed class EditorSettings
{
    public string LastVideoPath { get; set; } = "";
    public string LastScriptPath { get; set; } = "";
    public string MpvPath { get; set; } = "mpv";
    public string MpvIpcPipe { get; set; } = @"\\.\pipe\vrheadtrack";
    public bool SpeedColoringEnabled { get; set; } = true;
    public SegmentSpeedScaleMode SpeedScaleMode { get; set; } = SegmentSpeedScaleMode.FitToScript;
    public double ManualSpeedMinimumPpS { get; set; } = 0;
    public double ManualSpeedMaximumPpS { get; set; } = 700;
    public decimal? PatternOutputStartSeconds { get; set; }
    public decimal? PatternOutputEndSeconds { get; set; }
    public int? PatternCycleCount { get; set; }
    public int? PatternProfile { get; set; }
    public int? PatternStartMode { get; set; }
    public int? PatternMinimumPosition { get; set; }
    public int? PatternMaximumPosition { get; set; }
    public decimal? PatternRiseFraction { get; set; }
    public int? PatternMaximumHours { get; set; }
    public int? LastPatternConflictChoice { get; set; }
    public int? LastPatternMergeChoice { get; set; }

    private static string FilePath =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "ScriptSyncPilot.FunscriptEditor", "settings.json");

    public static EditorSettings Load()
    {
        try
        {
            if (File.Exists(FilePath))
                return JsonSerializer.Deserialize<EditorSettings>(File.ReadAllText(FilePath)) ?? new EditorSettings();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            // fall through to defaults
        }
        return new EditorSettings();
    }

    public void Save()
    {
        try
        {
            var dir = Path.GetDirectoryName(FilePath)!;
            Directory.CreateDirectory(dir);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            // ignore failures (best-effort persistence)
        }
    }
}
