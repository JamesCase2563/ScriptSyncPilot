using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ScriptSyncPilot.HandySync;

namespace ScriptSyncPilot.FunscriptEditor.Tests;

[TestClass]
public sealed class ConnectionKeyCollectionTests
{
    [TestMethod]
    public void AddedKeys_RoundTripInOrderWithStableIdsLabelsAndIndependentProtection()
    {
        var settings = new AppSettings();
        var first = settings.AddConnectionKey("First device", "synthetic-first");
        var second = settings.AddConnectionKey("Second device", "synthetic-second");
        Assert.AreNotEqual(first, second);
        Assert.AreEqual("synthetic-first", settings.GetFirstConnectionKey());
        Assert.AreEqual("synthetic-first", settings.ConnectionKey);

        var json = settings.Serialize();
        Assert.IsFalse(json.Contains("synthetic-first", StringComparison.Ordinal));
        Assert.IsFalse(json.Contains("synthetic-second", StringComparison.Ordinal));
        var restored = AppSettings.Deserialize(json, _ => { });
        CollectionAssert.AreEqual(new[] { first, second }, restored.ConnectionKeys.Select(entry => entry.Id).ToArray());
        CollectionAssert.AreEqual(new[] { "First device", "Second device" },
            restored.ConnectionKeys.Select(entry => entry.Label).ToArray());
        var protector = new DpapiConnectionKeyProtector();
        CollectionAssert.AreEqual(new[] { "synthetic-first", "synthetic-second" },
            restored.ConnectionKeys.Select(entry => protector.Unprotect(entry.ProtectedValue)).ToArray());
        Assert.AreEqual("synthetic-first", restored.GetFirstConnectionKey());
        Assert.AreEqual(2, AppSettings.Deserialize(restored.Serialize(), _ => { }).ConnectionKeys.Count);
    }

    [TestMethod]
    public void DeleteMiddle_PreservesOtherEntriesAndRuntimeKey()
    {
        var settings = new AppSettings();
        var first = settings.AddConnectionKey("First", "synthetic-first");
        var middle = settings.AddConnectionKey("Middle", "synthetic-middle");
        var last = settings.AddConnectionKey("Last", "synthetic-last");
        var lastEntry = settings.ConnectionKeys[2];

        settings.DeleteConnectionKey(middle);

        CollectionAssert.AreEqual(new[] { first, last }, settings.ConnectionKeys.Select(entry => entry.Id).ToArray());
        Assert.AreSame(lastEntry, settings.ConnectionKeys[1]);
        Assert.AreEqual("synthetic-first", settings.ConnectionKey);
        var restored = AppSettings.Deserialize(settings.Serialize(), _ => { });
        CollectionAssert.AreEqual(new[] { first, last }, restored.ConnectionKeys.Select(entry => entry.Id).ToArray());
    }

    [TestMethod]
    public void DeleteFirst_PromotesNextWithoutOverwritingItOnSave()
    {
        var settings = new AppSettings();
        var first = settings.AddConnectionKey("First", "synthetic-first");
        var next = settings.AddConnectionKey("Next", "synthetic-next");

        settings.DeleteConnectionKey(first);

        Assert.AreEqual("synthetic-next", settings.GetFirstConnectionKey());
        Assert.AreEqual("synthetic-next", settings.ConnectionKey);
        var restored = AppSettings.Deserialize(settings.Serialize(), _ => { });
        Assert.AreEqual(1, restored.ConnectionKeys.Count);
        Assert.AreEqual(next, restored.ConnectionKeys[0].Id);
        Assert.AreEqual("Next", restored.ConnectionKeys[0].Label);
        Assert.AreEqual("synthetic-next", restored.ConnectionKey);
    }

    [TestMethod]
    public void DeleteLast_LeavesNoActiveKeyAndSaveDoesNotResurrectIt()
    {
        var settings = new AppSettings();
        Assert.IsNull(settings.GetFirstConnectionKey());
        var id = settings.AddConnectionKey("Only", "synthetic-only");

        settings.DeleteConnectionKey(id);

        Assert.IsNull(settings.GetFirstConnectionKey());
        Assert.AreEqual("", settings.ConnectionKey);
        var restored = AppSettings.Deserialize(settings.Serialize(), _ => { });
        Assert.AreEqual(0, restored.ConnectionKeys.Count);
        Assert.IsNull(restored.GetFirstConnectionKey());
        Assert.AreEqual("", restored.ConnectionKey);
        var replacement = settings.AddConnectionKey("Replacement", "synthetic-replacement");
        Assert.AreNotEqual(id, replacement);
        Assert.AreEqual("synthetic-replacement", settings.ConnectionKey);
    }

    [TestMethod]
    public void AddingToLoadedSettings_PreservesFirstAndAllowsDuplicateLabels()
    {
        var settings = new AppSettings();
        var first = settings.AddConnectionKey("Device", "synthetic-first");
        var restored = AppSettings.Deserialize(settings.Serialize(), _ => { });

        var second = restored.AddConnectionKey("Device", "synthetic-second");

        Assert.AreEqual("synthetic-first", restored.GetFirstConnectionKey());
        Assert.AreEqual("synthetic-first", restored.ConnectionKey);
        Assert.AreEqual(first, restored.ConnectionKeys[0].Id);
        Assert.AreEqual(second, restored.ConnectionKeys[1].Id);
    }

    [DataTestMethod]
    [DataRow("", "synthetic-key")]
    [DataRow("   ", "synthetic-key")]
    [DataRow("Label", "")]
    [DataRow("Label", "   ")]
    public void InvalidAdd_DoesNotMutateSettings(string label, string key)
    {
        var settings = new AppSettings();
        Assert.ThrowsException<ArgumentException>(() => settings.AddConnectionKey(label, key));
        Assert.AreEqual(0, settings.ConnectionKeys.Count);
        Assert.AreEqual("", settings.ConnectionKey);
    }

    [TestMethod]
    public void UnknownOrEmptyId_IsReportedWithoutMutatingEntries()
    {
        var settings = new AppSettings();
        var id = settings.AddConnectionKey("Device", "synthetic-key");

        Assert.ThrowsException<KeyNotFoundException>(() => settings.DeleteConnectionKey(Guid.NewGuid()));
        Assert.ThrowsException<ArgumentException>(() => settings.DeleteConnectionKey(Guid.Empty));
        Assert.AreEqual(1, settings.ConnectionKeys.Count);
        Assert.AreEqual(id, settings.ConnectionKeys[0].Id);
        Assert.AreEqual("synthetic-key", settings.ConnectionKey);
    }

    [TestMethod]
    public void ProtectionFailure_DoesNotAddOrChangeActiveKey()
    {
        var protector = new SwitchableProtector();
        var settings = new AppSettings(protector);
        var id = settings.AddConnectionKey("First", "synthetic-first");
        protector.FailProtect = true;

        var error = Assert.ThrowsException<SettingsException>(
            () => settings.AddConnectionKey("Second", "synthetic-second"));

        Assert.AreEqual(1, settings.ConnectionKeys.Count);
        Assert.AreEqual(id, settings.ConnectionKeys[0].Id);
        Assert.AreEqual("synthetic-first", settings.ConnectionKey);
        Assert.IsFalse(error.ToString().Contains("synthetic", StringComparison.Ordinal));
    }

    [TestMethod]
    public void DecryptionFailure_RejectsRetrievalAndPromotionWithoutMutatingEntries()
    {
        var protector = new SwitchableProtector();
        var settings = new AppSettings(protector);
        var first = settings.AddConnectionKey("First", "synthetic-first");
        var second = settings.AddConnectionKey("Second", "synthetic-second");
        protector.FailUnprotect = true;

        var retrievalError = Assert.ThrowsException<SettingsException>(() => settings.GetFirstConnectionKey());
        var deletionError = Assert.ThrowsException<SettingsException>(() => settings.DeleteConnectionKey(first));

        CollectionAssert.AreEqual(new[] { first, second }, settings.ConnectionKeys.Select(entry => entry.Id).ToArray());
        Assert.AreEqual("synthetic-first", settings.ConnectionKey);
        Assert.IsFalse(retrievalError.ToString().Contains("synthetic", StringComparison.Ordinal));
        Assert.IsFalse(deletionError.ToString().Contains("synthetic", StringComparison.Ordinal));
    }

    [TestMethod]
    public void CollectionOperations_PersistAcrossActualFileSaveAndLoad()
    {
        var path = Path.Combine(Path.GetTempPath(), "ScriptSyncPilot-collection-" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            var settings = new AppSettings();
            var first = settings.AddConnectionKey("First", "synthetic-first");
            var second = settings.AddConnectionKey("Second", "synthetic-second");
            settings.Save(path);
            var loaded = AppSettings.Load(path);
            loaded.DeleteConnectionKey(first);
            loaded.Save(path);
            var restored = AppSettings.Load(path);
            Assert.AreEqual(second, restored.ConnectionKeys.Single().Id);
            Assert.AreEqual("synthetic-second", restored.GetFirstConnectionKey());
            restored.DeleteConnectionKey(second);
            restored.Save(path);
            Assert.IsNull(AppSettings.Load(path).GetFirstConnectionKey());
            using var document = JsonDocument.Parse(File.ReadAllText(path));
            Assert.AreEqual(0, document.RootElement.GetProperty("ConnectionKeys").GetArrayLength());
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    private sealed class SwitchableProtector : IConnectionKeyProtector
    {
        private readonly DpapiConnectionKeyProtector _inner = new();
        public bool FailProtect { get; set; }
        public bool FailUnprotect { get; set; }

        public string Protect(string connectionKey) => FailProtect
            ? throw new CryptographicException("synthetic sensitive details")
            : _inner.Protect(connectionKey);

        public string Unprotect(string protectedValue) => FailUnprotect
            ? throw new CryptographicException("synthetic sensitive details")
            : _inner.Unprotect(protectedValue);
    }
}
