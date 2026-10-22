using ScriptSyncPilot.Common;

namespace ScriptSyncPilot.HandySync;

public static class Program
{
    [STAThread]
    private static void Main()
    {
        AppLog.Init("ScriptSyncPilot.HandySync");
        ApplicationConfiguration.Initialize();
        try
        {
            Application.Run(new MainForm());
        }
        catch (SettingsException ex)
        {
            AppLog.Write(ex.Message);
            MessageBox.Show(ex.Message, "HandySync settings error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}
