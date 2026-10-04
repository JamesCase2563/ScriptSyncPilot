using ScriptSyncPilot.Common;

namespace ScriptSyncPilot.HandySync;

public static class Program
{
    [STAThread]
    private static void Main()
    {
        AppLog.Init("ScriptSyncPilot.HandySync");
        ApplicationConfiguration.Initialize();
        Application.Run(new MainForm());
    }
}
