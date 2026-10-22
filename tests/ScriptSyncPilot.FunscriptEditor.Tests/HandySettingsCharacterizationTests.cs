using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ScriptSyncPilot.HandySync;

namespace ScriptSyncPilot.FunscriptEditor.Tests;

[TestClass]
public sealed class HandySettingsCharacterizationTests
{
    [TestMethod]
    public void EmptyObject_UsesExistingDefaults()
    {
        var settings = AppSettings.Deserialize("{}", _ => { });

        Assert.AreEqual("", settings.ConnectionKey);
        Assert.AreEqual(@"\\.\pipe\vrheadtrack", settings.MpvIpcPipe);
        Assert.AreEqual(0, settings.DeviceOffsetMs);
        Assert.AreEqual(0, settings.StrokeMinPercent);
        Assert.AreEqual(100, settings.StrokeMaxPercent);
        Assert.AreEqual("", settings.LastScriptPath);
    }

    [TestMethod]
    public void NonSecretSettings_RoundTripWithoutChanges()
    {
        var original = new AppSettings
        {
            MpvIpcPipe = @"\\.\pipe\test-player",
            DeviceOffsetMs = -375,
            StrokeMinPercent = 15,
            StrokeMaxPercent = 85,
            LastScriptPath = @"C:\test scripts\sample.funscript"
        };

        var restored = AppSettings.Deserialize(original.Serialize(), _ => { });

        Assert.AreEqual(original.MpvIpcPipe, restored.MpvIpcPipe);
        Assert.AreEqual(original.DeviceOffsetMs, restored.DeviceOffsetMs);
        Assert.AreEqual(original.StrokeMinPercent, restored.StrokeMinPercent);
        Assert.AreEqual(original.StrokeMaxPercent, restored.StrokeMaxPercent);
        Assert.AreEqual(original.LastScriptPath, restored.LastScriptPath);
    }

    [TestMethod]
    public void LegacyPlaintextKey_CurrentlyLoadsAndSerializesWithOtherSettings()
    {
        var settings = AppSettings.Deserialize("""
            {
              "ConnectionKey": "synthetic-legacy-key",
              "DeviceOffsetMs": 125,
              "StrokeMinPercent": 20,
              "StrokeMaxPercent": 90,
              "LastScriptPath": "sample.funscript",
              "FutureSetting": true
            }
            """, _ => { });

        Assert.AreEqual("synthetic-legacy-key", settings.ConnectionKey);
        Assert.AreEqual(125, settings.DeviceOffsetMs);
        Assert.AreEqual(20, settings.StrokeMinPercent);
        Assert.AreEqual(90, settings.StrokeMaxPercent);
        Assert.AreEqual("sample.funscript", settings.LastScriptPath);
        using var document = JsonDocument.Parse(settings.Serialize());
        Assert.AreEqual("synthetic-legacy-key",
            document.RootElement.GetProperty("ConnectionKey").GetString());
    }

    [DataTestMethod]
    [DataRow("{")]
    [DataRow("""{"DeviceOffsetMs":"invalid"}""")]
    public void InvalidSettings_DeserializeThrowsJsonException(string json)
    {
        Assert.ThrowsException<JsonException>(() => AppSettings.Deserialize(json, _ => { }));
    }
}
