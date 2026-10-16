using System.Text.Json;
using ScriptSyncPilot.Common;

namespace ScriptSyncPilot.HandySync;

/// <summary>Small persisted settings blob, kept separate from any runtime session state.</summary>
public sealed class AppSettings
{
    public string ConnectionKey { get; set; } = "";
    public string MpvIpcPipe { get; set; } = @"\\.\pipe\vrheadtrack";
    public int DeviceOffsetMs { get; set; }
    public int StrokeMinPercent { get; set; }
    public int StrokeMaxPercent { get; set; } = 100;
    public string LastScriptPath { get; set; } = "";

    private static string FilePath =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "ScriptSyncPilot.HandySync", "settings.json");

    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(FilePath))
            {
                var json = File.ReadAllText(FilePath);
                return Deserialize(json);
            }
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            // fall through to defaults
        }
        return new AppSettings();
    }

    internal static AppSettings Deserialize(string json, Action<string>? log = null)
    {
        LogDiscardedLegacyOffset(json, log ?? AppLog.Write);
        return JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
    }

    internal string Serialize() => JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });

    private static void LogDiscardedLegacyOffset(string json, Action<string> log)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.TryGetProperty("OffsetMs", out var legacyOffset)
                && legacyOffset.TryGetInt32(out var value) && value != 0)
                log("Discarded legacy local OffsetMs setting; the device-side HSTP offset is independent.");
        }
        catch (JsonException)
        {
        }
    }

    public void Save()
    {
         try
         {
             var dir = Path.GetDirectoryName(FilePath)!;
             Directory.CreateDirectory(dir);
             File.WriteAllText(FilePath, Serialize());
         }
         catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
         {
             // ignore failures (best-effort persistence)
         }
    }
}
