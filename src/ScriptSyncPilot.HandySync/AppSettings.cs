using System.Text.Json;

namespace ScriptSyncPilot.HandySync;

/// <summary>Small persisted settings blob, kept separate from any runtime session state.</summary>
public sealed class AppSettings
{
    public string ConnectionKey { get; set; } = "";
    public string MpvIpcPipe { get; set; } = @"\\.\pipe\vrheadtrack";
    public int OffsetMs { get; set; }
    public string LastScriptPath { get; set; } = "";

    private static string FilePath =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "ScriptSyncPilot.HandySync", "settings.json");

    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(FilePath))
                return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(FilePath)) ?? new AppSettings();
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            // fall through to defaults
        }
        return new AppSettings();
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
