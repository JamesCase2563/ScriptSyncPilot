namespace ScriptSyncPilot.Common;

/// <summary>
/// Best-effort append-only file logger. Call <see cref="Init"/> once at
/// startup with the hosting app's name before the first <see cref="Write"/>.
/// </summary>
public static class AppLog
{
    private static readonly object Sync = new();

    public static string FilePath { get; private set; } = BuildPath("ScriptSyncPilot");

    public static void Init(string appName) => FilePath = BuildPath(appName);

    private static string BuildPath(string appName) => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        appName,
        $"{appName.ToLowerInvariant()}.log");

    public static void Write(string message)
    {
        try
        {
            lock (Sync)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
                File.AppendAllText(FilePath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {message}{Environment.NewLine}");
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
