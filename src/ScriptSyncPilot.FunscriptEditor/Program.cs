using ScriptSyncPilot.Common;

namespace ScriptSyncPilot.FunscriptEditor;

public static class Program
{
    [STAThread]
    private static void Main()
    {
        AppLog.Init("ScriptSyncPilot.FunscriptEditor");
        ApplicationConfiguration.Initialize();
        Application.Run(new MainForm());
    }
}
