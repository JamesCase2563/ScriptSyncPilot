using System.Security.Cryptography;
using System.Text;

namespace ScriptSyncPilot.HandySync;

internal sealed class DpapiConnectionKeyProtector : IConnectionKeyProtector
{
    private static readonly Encoding KeyEncoding = new UTF8Encoding(false, true);

    public string Protect(string connectionKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionKey);
        var plaintext = KeyEncoding.GetBytes(connectionKey);
        try
        {
            return Convert.ToBase64String(
                ProtectedData.Protect(plaintext, null, DataProtectionScope.CurrentUser));
        }
        finally
        {
            CryptographicOperations.ZeroMemory(plaintext);
        }
    }

    public string Unprotect(string protectedValue)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(protectedValue);
        var ciphertext = Convert.FromBase64String(protectedValue);
        var plaintext = ProtectedData.Unprotect(ciphertext, null, DataProtectionScope.CurrentUser);
        try
        {
            var connectionKey = KeyEncoding.GetString(plaintext);
            if (string.IsNullOrWhiteSpace(connectionKey))
                throw new CryptographicException("The protected connection key is empty.");
            return connectionKey;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(plaintext);
        }
    }
}
