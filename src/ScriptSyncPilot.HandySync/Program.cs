using ScriptSyncPilot.Common;

namespace ScriptSyncPilot.HandySync;

public static class Program
{
    [STAThread]
    private static void Main()
    {
        AppLog.Init("ScriptSyncPilot.HandySync");
        AppLog.WriteStartupInformation();
        ApplicationConfiguration.Initialize();
        AppSettings settings;
        string? settingsLoadError = null;
        try
        {
            settings = AppSettings.Load();
        }
        catch (SettingsException ex)
        {
            settings = new AppSettings();
            settingsLoadError = ex.Message;
        }

        Application.Run(new MainForm(settings, key => new HandyApiClient(key), settingsLoadError: settingsLoadError));
    }
}
