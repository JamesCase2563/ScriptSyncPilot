using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ScriptSyncPilot.HandySync;

namespace ScriptSyncPilot.FunscriptEditor.Tests;

[TestClass]
public sealed class ProtectedConnectionKeyTests
{
    [TestMethod]
    public void Entry_SerializesOnlyItsIdentifierLabelVersionAndProtectedValue()
    {
        const string key = "synthetic-representation-key";
        var protector = new DpapiConnectionKeyProtector();
        var entry = new ProtectedConnectionKey(
            Guid.NewGuid(), "Default", ProtectedConnectionKey.CurrentVersion, protector.Protect(key));

        var json = JsonSerializer.Serialize(entry);
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        Assert.AreEqual(4, root.EnumerateObject().Count());
        Assert.AreEqual(entry.Id, root.GetProperty("Id").GetGuid());
        Assert.AreEqual("Default", root.GetProperty("Label").GetString());
        Assert.AreEqual(1, root.GetProperty("Version").GetInt32());
        Assert.AreEqual(entry.ProtectedValue, root.GetProperty("ProtectedValue").GetString());
        Assert.IsFalse(json.Contains(key, StringComparison.Ordinal));

        var restored = JsonSerializer.Deserialize<ProtectedConnectionKey>(json)!;
        Assert.AreEqual(entry.Id, restored.Id);
        Assert.AreEqual(entry.Label, restored.Label);
        Assert.AreEqual(entry.Version, restored.Version);
        Assert.AreEqual(entry.ProtectedValue, restored.ProtectedValue);
        Assert.AreEqual(key, protector.Unprotect(restored.ProtectedValue));
    }

    [TestMethod]
    public void EntryArray_PreservesOrderAndStableIdentifiersAcrossRoundTrips()
    {
        var entries = new[]
        {
            new ProtectedConnectionKey(Guid.NewGuid(), "First", 1, "AQID"),
            new ProtectedConnectionKey(Guid.NewGuid(), "Second", 1, "BAUG")
        };

        var restored = JsonSerializer.Deserialize<ProtectedConnectionKey[]>(
            JsonSerializer.Serialize(entries))!;
        var restoredAgain = JsonSerializer.Deserialize<ProtectedConnectionKey[]>(
            JsonSerializer.Serialize(restored))!;

        Assert.AreEqual(2, restoredAgain.Length);
        for (var index = 0; index < entries.Length; index++)
        {
            Assert.AreEqual(entries[index].Id, restoredAgain[index].Id);
            Assert.AreEqual(entries[index].Label, restoredAgain[index].Label);
            Assert.AreEqual(entries[index].ProtectedValue, restoredAgain[index].ProtectedValue);
        }
        Assert.AreEqual("[]", JsonSerializer.Serialize(Array.Empty<ProtectedConnectionKey>()));
    }

    [DataTestMethod]
    [DataRow("Id")]
    [DataRow("Label")]
    [DataRow("Version")]
    [DataRow("ProtectedValue")]
    public void MissingField_IsRejected(string field)
    {
        var json = ValidEntryJson();
        Assert.IsTrue(json.Remove(field));

        Assert.ThrowsException<JsonException>(
            () => JsonSerializer.Deserialize<ProtectedConnectionKey>(json.ToJsonString()));
    }

    [DataTestMethod]
    [DataRow("Id", "\"00000000-0000-0000-0000-000000000000\"")]
    [DataRow("Label", "null")]
    [DataRow("Label", "\"   \"")]
    [DataRow("Version", "0")]
    [DataRow("Version", "2")]
    [DataRow("ProtectedValue", "null")]
    [DataRow("ProtectedValue", "\"   \"")]
    [DataRow("ProtectedValue", "\"synthetic-invalid-payload!\"")]
    public void InvalidField_IsRejectedWithoutEchoingPayload(string field, string value)
    {
        var json = ValidEntryJson();
        json[field] = JsonNode.Parse(value);

        var error = Assert.ThrowsException<JsonException>(
            () => JsonSerializer.Deserialize<ProtectedConnectionKey>(json.ToJsonString()));

        Assert.IsFalse(error.ToString().Contains("synthetic-invalid-payload!", StringComparison.Ordinal));
        Assert.IsFalse(error.ToString().Contains("AQID", StringComparison.Ordinal));
    }

    private static JsonObject ValidEntryJson() => new()
    {
        ["Id"] = Guid.NewGuid().ToString(),
        ["Label"] = "Default",
        ["Version"] = 1,
        ["ProtectedValue"] = "AQID"
    };
}
