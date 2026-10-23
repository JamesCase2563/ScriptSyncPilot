namespace ScriptSyncPilot.HandySync;

internal interface IConnectionKeyProtector
{
    string Protect(string connectionKey);
    string Unprotect(string protectedValue);
}
