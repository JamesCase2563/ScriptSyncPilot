using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ScriptSyncPilot.HandySync;

namespace ScriptSyncPilot.FunscriptEditor.Tests;

[TestClass]
public sealed class ProtectedSettingsTests
{
    [TestMethod]
    public void SaveAndLoad_ProtectsKeyAndPreservesOtherSettings()
    {
        WithTemporaryPath(path =>
        {
            var settings = new AppSettings
            {
                ConnectionKey = "synthetic-persisted-key",
                MpvIpcPipe = @"\\.\pipe\test-player",
                LastScriptPath = @"C:\test scripts\example.funscript",
                DeviceOffsetMs = -150,
                StrokeMinPercent = 20,
                StrokeMaxPercent = 80
            };
            settings.Save(path);
            var json = File.ReadAllText(path);
            Assert.IsFalse(json.Contains(settings.ConnectionKey, StringComparison.Ordinal));
            using var document = JsonDocument.Parse(json);
            Assert.IsFalse(document.RootElement.TryGetProperty("ConnectionKey", out _));
            var entry = document.RootElement.GetProperty("ConnectionKeys")[0];
            Assert.AreEqual(1, entry.GetProperty("Version").GetInt32());
            Assert.AreEqual("Default", entry.GetProperty("Label").GetString());

            var restored = AppSettings.Load(path);
            Assert.AreEqual(settings.ConnectionKey, restored.ConnectionKey);
            Assert.AreEqual(settings.MpvIpcPipe, restored.MpvIpcPipe);
            Assert.AreEqual(settings.LastScriptPath, restored.LastScriptPath);
            Assert.AreEqual(settings.DeviceOffsetMs, restored.DeviceOffsetMs);
            Assert.AreEqual(settings.StrokeMinPercent, restored.StrokeMinPercent);
            Assert.AreEqual(settings.StrokeMaxPercent, restored.StrokeMaxPercent);
            restored.ConnectionKey = "synthetic-updated-key";
            restored.Save(path);
            using var updated = JsonDocument.Parse(File.ReadAllText(path));
            var entries = updated.RootElement.GetProperty("ConnectionKeys");
            Assert.AreEqual(1, entries.GetArrayLength());
            Assert.AreEqual(entry.GetProperty("Id").GetGuid(), entries[0].GetProperty("Id").GetGuid());
            Assert.AreEqual(restored.ConnectionKey, AppSettings.Load(path).ConnectionKey);
        });
    }

    [TestMethod]
    public void LegacyFile_KeyReentryPreservesAllNonSecretSettingsThroughSaveAndReload()
    {
        WithTemporaryPath(path =>
        {
            const string legacyJson = """
                {
                  "ConnectionKey": "synthetic-legacy-key",
                  "MpvIpcPipe": "\\\\.\\pipe\\legacy-test-player",
                  "DeviceOffsetMs": -375,
                  "StrokeMinPercent": 15,
                  "StrokeMaxPercent": 85,
                  "LastScriptPath": "C:\\test scripts\\legacy movie.funscript"
                }
                """;
            File.WriteAllText(path, legacyJson);
            var settings = AppSettings.Load(path);
            Assert.AreEqual("", settings.ConnectionKey);
            Assert.IsTrue(settings.RequiresKeyReentry);
            Assert.AreEqual(legacyJson, File.ReadAllText(path));

            settings.ConnectionKey = "synthetic-reentered-key";
            settings.Save(path);
            var savedJson = File.ReadAllText(path);
            using var original = JsonDocument.Parse(legacyJson);
            using var saved = JsonDocument.Parse(savedJson);
            foreach (var field in original.RootElement.EnumerateObject())
            {
                if (field.Name == "ConnectionKey") continue;
                Assert.AreEqual(field.Value.GetRawText(),
                    saved.RootElement.GetProperty(field.Name).GetRawText(), field.Name);
            }
            Assert.IsFalse(saved.RootElement.TryGetProperty("ConnectionKey", out _));
            Assert.IsFalse(savedJson.Contains("synthetic-legacy-key", StringComparison.Ordinal));
            Assert.IsFalse(savedJson.Contains("synthetic-reentered-key", StringComparison.Ordinal));
            Assert.AreEqual(1, saved.RootElement.GetProperty("ConnectionKeys").GetArrayLength());

            var restored = AppSettings.Load(path);
            Assert.AreEqual("synthetic-reentered-key", restored.GetFirstConnectionKey());
            Assert.IsFalse(restored.RequiresKeyReentry);
            Assert.AreEqual(@"\\.\pipe\legacy-test-player", restored.MpvIpcPipe);
            Assert.AreEqual(-375, restored.DeviceOffsetMs);
            Assert.AreEqual(15, restored.StrokeMinPercent);
            Assert.AreEqual(85, restored.StrokeMaxPercent);
            Assert.AreEqual(@"C:\test scripts\legacy movie.funscript", restored.LastScriptPath);
        });
    }

    [TestMethod]
    public void LegacyKey_IsNotPassedToProtectorAndReentrySavesProtectedKey()
    {
        var protector = new FailingProtector();
        var settings = AppSettings.Deserialize(
            """{"ConnectionKey":"synthetic-ignored-key","DeviceOffsetMs":275}""",
            _ => { }, protector);
        Assert.IsTrue(settings.RequiresKeyReentry);
        Assert.AreEqual("", settings.ConnectionKey);
        Assert.AreEqual(0, protector.UnprotectCalls);
        Assert.AreEqual(0, protector.ProtectCalls);

        var realSettings = AppSettings.Deserialize(
            """{"ConnectionKey":"synthetic-ignored-key","DeviceOffsetMs":275}""", _ => { });
        realSettings.ConnectionKey = "synthetic-reentered-key";
        var restored = AppSettings.Deserialize(realSettings.Serialize(), _ => { });
        Assert.AreEqual("synthetic-reentered-key", restored.ConnectionKey);
        Assert.AreEqual(275, restored.DeviceOffsetMs);
        Assert.IsFalse(restored.RequiresKeyReentry);
    }

    [TestMethod]
    public void ProtectionFailure_LeavesExistingFileUnchangedAndDoesNotEchoExceptionDetails()
    {
        WithTemporaryPath(path =>
        {
            File.WriteAllText(path, "original settings");
            var protector = new FailingProtector();
            var settings = new AppSettings(protector) { ConnectionKey = "synthetic-key" };
            var error = Assert.ThrowsException<SettingsException>(() => settings.Save(path));
            Assert.AreEqual("original settings", File.ReadAllText(path));
            Assert.AreEqual(1, protector.ProtectCalls);
            Assert.IsFalse(error.ToString().Contains("synthetic", StringComparison.Ordinal));
        });
    }

    [TestMethod]
    public void DecryptionFailure_LeavesFileUnchangedWithoutDefaultSettings()
    {
        WithTemporaryPath(path =>
        {
            var json = new AppSettings { ConnectionKey = "synthetic-key" }.Serialize();
            File.WriteAllText(path, json);
            var protector = new FailingProtector();
            var error = Assert.ThrowsException<SettingsException>(() => AppSettings.Load(path, protector));
            Assert.AreEqual(json, File.ReadAllText(path));
            Assert.AreEqual(1, protector.UnprotectCalls);
            Assert.IsFalse(error.ToString().Contains("synthetic", StringComparison.Ordinal));
        });
    }

    [DataTestMethod]
    [DataRow("null")]
    [DataRow("[null]")]
    [DataRow("[{}]")]
    public void InvalidProtectedCollection_IsRejected(string collection)
    {
        Assert.ThrowsException<SettingsException>(() => AppSettings.Deserialize(
            "{\"ConnectionKeys\":" + collection + "}", _ => { }));
    }

    [TestMethod]
    public void AllEntriesAreDecrypted_AndOtherEntriesRemainInSavedOrder()
    {
        var first = new AppSettings { ConnectionKey = "synthetic-first" }.Serialize();
        var second = new AppSettings { ConnectionKey = "synthetic-second" }.Serialize();
        var json = JsonNode.Parse(first)!.AsObject();
        var secondEntry = JsonNode.Parse(second)!["ConnectionKeys"]![0]!.DeepClone();
        json["ConnectionKeys"]!.AsArray().Add(secondEntry);

        var loaded = AppSettings.Deserialize(json.ToJsonString(), _ => { });
        Assert.AreEqual("synthetic-first", loaded.ConnectionKey);
        var saved = JsonNode.Parse(loaded.Serialize())!;
        Assert.IsTrue(JsonNode.DeepEquals(secondEntry, saved["ConnectionKeys"]![1]));

        json["ConnectionKeys"]![1]!["ProtectedValue"] = "AQID";
        Assert.ThrowsException<SettingsException>(
            () => AppSettings.Deserialize(json.ToJsonString(), _ => { }));
    }

    [TestMethod]
    public void DuplicateIdentifiers_AreRejected()
    {
        var json = JsonNode.Parse(new AppSettings { ConnectionKey = "synthetic-key" }.Serialize())!;
        var entries = json["ConnectionKeys"]!.AsArray();
        entries.Add(entries[0]!.DeepClone());
        Assert.ThrowsException<SettingsException>(
            () => AppSettings.Deserialize(json.ToJsonString(), _ => { }));
    }

    [TestMethod]
    public void MissingSettingsFile_ReturnsDefaults()
    {
        WithTemporaryPath(path => Assert.AreEqual("", AppSettings.Load(path).ConnectionKey));
    }

    [TestMethod]
    public void PublicJsonSerialization_DoesNotExposeRuntimeKey()
    {
        var json = JsonSerializer.Serialize(new AppSettings { ConnectionKey = "synthetic-runtime-key" });
        Assert.IsFalse(json.Contains("synthetic-runtime-key", StringComparison.Ordinal));
        using var document = JsonDocument.Parse(json);
        Assert.IsFalse(document.RootElement.TryGetProperty("ConnectionKey", out _));
    }

    [TestMethod]
    public void MalformedSettingsFile_IsNotSilentlyReplacedWithDefaults()
    {
        WithTemporaryPath(path =>
        {
            File.WriteAllText(path, "{");
            Assert.ThrowsException<SettingsException>(() => AppSettings.Load(path));
            Assert.AreEqual("{", File.ReadAllText(path));
        });
    }

    [TestMethod]
    public void BlankRuntimeKey_DoesNotRemoveExistingProtectedEntry()
    {
        WithTemporaryPath(path =>
        {
            new AppSettings { ConnectionKey = "synthetic-key" }.Save(path);
            var original = File.ReadAllText(path);
            var settings = AppSettings.Load(path);
            settings.ConnectionKey = "";
            Assert.ThrowsException<SettingsException>(() => settings.Save(path));
            Assert.AreEqual(original, File.ReadAllText(path));
        });
    }

    [DataTestMethod]
    [DataRow("MpvIpcPipe")]
    [DataRow("LastScriptPath")]
    public void NullNonSecretTextField_IsRejectedWithoutChangingFile(string field)
    {
        WithTemporaryPath(path =>
        {
            var json = "{\"" + field + "\":null}";
            File.WriteAllText(path, json);
            Assert.ThrowsException<SettingsException>(() => AppSettings.Load(path));
            Assert.AreEqual(json, File.ReadAllText(path));
        });
    }

    [TestMethod]
    public void OmittedNonSecretTextFields_UseDefaults()
    {
        var defaults = new AppSettings();
        var settings = AppSettings.Deserialize("{}", _ => { });
        Assert.AreEqual(defaults.MpvIpcPipe, settings.MpvIpcPipe);
        Assert.AreEqual(defaults.LastScriptPath, settings.LastScriptPath);
    }

    [TestMethod]
    public void EmptyNonSecretTextFields_ArePreserved()
    {
        var settings = AppSettings.Deserialize(
            """{"MpvIpcPipe":"","LastScriptPath":""}""", _ => { });
        Assert.AreEqual("", settings.MpvIpcPipe);
        Assert.AreEqual("", settings.LastScriptPath);
    }

    [TestMethod]
    public void UnwritableDestination_ReportsFailure()
    {
        WithTemporaryPath(path =>
        {
            Directory.CreateDirectory(path);
            try
            {
                Assert.ThrowsException<SettingsException>(() => new AppSettings().Save(path));
            }
            finally
            {
                Directory.Delete(path);
            }
        });
    }

    private static void WithTemporaryPath(Action<string> test)
    {
        var directory = Path.Combine(Path.GetTempPath(), "ScriptSyncPilot-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "settings.json");
        try { test(path); }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
            Directory.Delete(directory);
        }
    }

    private sealed class FailingProtector : IConnectionKeyProtector
    {
        public int ProtectCalls { get; private set; }
        public int UnprotectCalls { get; private set; }

        public string Protect(string connectionKey)
        {
            ProtectCalls++;
            throw new CryptographicException("synthetic sensitive protection details");
        }

        public string Unprotect(string protectedValue)
        {
            UnprotectCalls++;
            throw new CryptographicException("synthetic sensitive decryption details");
        }
    }
}
