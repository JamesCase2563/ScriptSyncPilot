using System.Security.Cryptography;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ScriptSyncPilot.HandySync;

namespace ScriptSyncPilot.FunscriptEditor.Tests;

[TestClass]
public sealed class ConnectionKeyProtectorTests
{
    private const string SyntheticKey = "synthetic-test-key";

    [TestMethod]
    public void ProtectedValue_RoundTripsAcrossProtectorInstances()
    {
        IConnectionKeyProtector protector = new DpapiConnectionKeyProtector();
        var protectedValue = protector.Protect(SyntheticKey);

        Assert.AreNotEqual(SyntheticKey, protectedValue);
        Assert.IsFalse(protectedValue.Contains(SyntheticKey, StringComparison.Ordinal));
        Assert.AreEqual(SyntheticKey, new DpapiConnectionKeyProtector().Unprotect(protectedValue));
    }

    [TestMethod]
    public void ProtectedValue_UsesFrameworkCurrentUserDpapiWithoutAdditionalEntropy()
    {
        var protector = new DpapiConnectionKeyProtector();
        var plaintext = ProtectedData.Unprotect(
            Convert.FromBase64String(protector.Protect(SyntheticKey)), null, DataProtectionScope.CurrentUser);
        try
        {
            Assert.AreEqual(SyntheticKey, Encoding.UTF8.GetString(plaintext));
        }
        finally
        {
            CryptographicOperations.ZeroMemory(plaintext);
        }
    }

    [TestMethod]
    public void CorruptedDpapiValue_ThrowsWithoutReturningAFallbackOrDisclosingInput()
    {
        var protector = new DpapiConnectionKeyProtector();
        var ciphertext = Convert.FromBase64String(protector.Protect(SyntheticKey));
        ciphertext[^1] ^= 0xff;
        var corrupted = Convert.ToBase64String(ciphertext);

        var error = Assert.ThrowsException<CryptographicException>(() => protector.Unprotect(corrupted));

        Assert.IsFalse(error.ToString().Contains(SyntheticKey, StringComparison.Ordinal));
        Assert.IsFalse(error.ToString().Contains(corrupted, StringComparison.Ordinal));
    }

    [TestMethod]
    public void InvalidBase64_ThrowsWithoutDisclosingInput()
    {
        const string invalid = "synthetic-invalid-ciphertext!";
        var error = Assert.ThrowsException<FormatException>(
            () => new DpapiConnectionKeyProtector().Unprotect(invalid));

        Assert.IsFalse(error.ToString().Contains(invalid, StringComparison.Ordinal));
    }

    [DataTestMethod]
    [DataRow("")]
    [DataRow("   ")]
    public void EmptyKey_IsRejected(string key)
    {
        Assert.ThrowsException<ArgumentException>(() => new DpapiConnectionKeyProtector().Protect(key));
    }

    [DataTestMethod]
    [DataRow("")]
    [DataRow("   ")]
    public void EmptyProtectedValue_IsRejected(string value)
    {
        Assert.ThrowsException<ArgumentException>(() => new DpapiConnectionKeyProtector().Unprotect(value));
    }

    [TestMethod]
    public void ValidDpapiBlobContainingEmptyKey_IsRejected()
    {
        var protectedValue = Convert.ToBase64String(
            ProtectedData.Protect([], null, DataProtectionScope.CurrentUser));

        Assert.ThrowsException<CryptographicException>(
            () => new DpapiConnectionKeyProtector().Unprotect(protectedValue));
    }
}
