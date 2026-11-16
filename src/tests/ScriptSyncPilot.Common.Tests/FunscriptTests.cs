using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ScriptSyncPilot.Common;

namespace ScriptSyncPilot.Common.Tests;

[TestClass]
public sealed class FunscriptTests
{
    private string _directory = null!;

    [TestInitialize]
    public void Initialize()
    {
        _directory = Path.Combine(Path.GetTempPath(), "ScriptSyncPilot.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_directory);
    }

    [TestCleanup]
    public void Cleanup() => Directory.Delete(_directory, recursive: true);

    [TestMethod]
    public void AddOrReplace_OrdersActionsAndReplacesMatchingTimestamp()
    {
        var script = new Funscript();
        script.AddOrReplace(new FunscriptAction(2000, 100));
        script.AddOrReplace(new FunscriptAction(0, 0));
        script.AddOrReplace(new FunscriptAction(1000, 50));
        script.AddOrReplace(new FunscriptAction(1000, 75));

        CollectionAssert.AreEqual(
            new[] { new FunscriptAction(0, 0), new FunscriptAction(1000, 75), new FunscriptAction(2000, 100) },
            script.Actions.ToArray());
    }

    [TestMethod]
    public void UpdateAt_ReordersActionAndReturnsNewIndex()
    {
        var script = new Funscript();
        script.AddOrReplace(new FunscriptAction(0, 0));
        script.AddOrReplace(new FunscriptAction(1000, 50));
        script.AddOrReplace(new FunscriptAction(2000, 100));

        var index = script.UpdateAt(0, new FunscriptAction(1500, 25));

        Assert.AreEqual(1, index);
        CollectionAssert.AreEqual(
            new[] { new FunscriptAction(1000, 50), new FunscriptAction(1500, 25), new FunscriptAction(2000, 100) },
            script.Actions.ToArray());
    }

    [TestMethod]
    public void RemoveAt_RemovesOnlyRequestedAction()
    {
        var script = new Funscript();
        script.AddOrReplace(new FunscriptAction(0, 0));
        script.AddOrReplace(new FunscriptAction(1000, 100));

        script.RemoveAt(0);

        CollectionAssert.AreEqual(new[] { new FunscriptAction(1000, 100) }, script.Actions.ToArray());
    }

    [TestMethod]
    public void ReplaceActionsAtomically_ReplacesWholeListAndDetectsNoOp()
    {
        var script = new Funscript();
        script.AddOrReplace(new FunscriptAction(0, 0));
        script.AddOrReplace(new FunscriptAction(1000, 100));
        var replacement = new[] { new FunscriptAction(1000, 25), new FunscriptAction(2000, 75) };

        Assert.IsTrue(script.ReplaceActionsAtomically(replacement));
        CollectionAssert.AreEqual(replacement, script.Actions.ToArray());
        Assert.IsFalse(script.ReplaceActionsAtomically(replacement));
    }

    [TestMethod]
    public void ReplaceActionsAtomically_InvalidCandidateLeavesExistingActionsUnchanged()
    {
        var script = new Funscript();
        script.AddOrReplace(new FunscriptAction(0, 10));
        script.AddOrReplace(new FunscriptAction(1000, 90));
        var original = script.Actions.ToArray();
        var invalidCandidates = new[]
        {
            new[] { new FunscriptAction(1000, 20), new FunscriptAction(0, 30) },
            new[] { new FunscriptAction(0, 20), new FunscriptAction(0, 30) },
            new[] { new FunscriptAction(-1, 20) },
            new[] { new FunscriptAction(0, -1) },
            new[] { new FunscriptAction(0, 101) },
        };

        foreach (var candidate in invalidCandidates)
        {
            Assert.ThrowsException<ArgumentException>(() => script.ReplaceActionsAtomically(candidate));
            CollectionAssert.AreEqual(original, script.Actions.ToArray());
        }
    }

    [TestMethod]
    public void Load_SortsUnorderedActions()
    {
        var path = WriteInput("""
            { "actions": [{ "at": 2000, "pos": 100 }, { "at": 0, "pos": 0 }] }
            """);

        var script = Funscript.Load(path);

        CollectionAssert.AreEqual(
            new[] { new FunscriptAction(0, 0), new FunscriptAction(2000, 100) }, script.Actions.ToArray());
    }

    [DataTestMethod]
    [DataRow("{}")]
    [DataRow("{\"actions\": []}")]
    [DataRow("{\"actions\": null}")]
    public void Load_AllowsEmptyEditorDocuments(string json)
    {
        var script = Funscript.Load(WriteInput(json));

        Assert.AreEqual(0, script.Actions.Count);
    }

    [TestMethod]
    public void Load_InvalidJsonThrowsInvalidDataException()
    {
        var path = WriteInput("{invalid");

        Assert.ThrowsException<InvalidDataException>(() => Funscript.Load(path));
    }

    [TestMethod]
    public void Save_WritesLowercaseActionFieldsAndReloads()
    {
        var script = new Funscript();
        script.AddOrReplace(new FunscriptAction(1234, 56));
        var path = Path.Combine(_directory, "output.funscript");

        script.Save(path);

        using var document = JsonDocument.Parse(File.ReadAllText(path));
        var root = document.RootElement;
        Assert.IsFalse(root.TryGetProperty("Actions", out _));
        var action = root.GetProperty("actions")[0];
        Assert.AreEqual(1234, action.GetProperty("at").GetInt32());
        Assert.AreEqual(56, action.GetProperty("pos").GetInt32());
        CollectionAssert.AreEqual(script.Actions.ToArray(), Funscript.Load(path).Actions.ToArray());
    }

    [TestMethod]
    public void FailedStagedSave_PreservesExistingFileAndRemovesPartialTemporaryFile()
    {
        const string originalJson = "{\"metadata\":{\"title\":\"keep\"},\"actions\":[{\"at\":0,\"pos\":10}]}";
        var path = Path.Combine(_directory, "existing.funscript");
        File.WriteAllText(path, originalJson);
        var originalBytes = File.ReadAllBytes(path);
        var script = Funscript.Load(path);
        script.AddOrReplace(new FunscriptAction(1000, 90));

        Assert.ThrowsException<IOException>(() => script.Save(path, (stream, json) =>
        {
            var partial = System.Text.Encoding.UTF8.GetBytes(json[..Math.Min(24, json.Length)]);
            stream.Write(partial);
            throw new IOException("Synthetic write failure.");
        }));

        CollectionAssert.AreEqual(originalBytes, File.ReadAllBytes(path));
        CollectionAssert.AreEqual(
            new[] { new FunscriptAction(0, 10), new FunscriptAction(1000, 90) }, script.Actions.ToArray());
        CollectionAssert.AreEqual(new[] { "existing.funscript" },
            Directory.GetFiles(_directory).Select(Path.GetFileName).OrderBy(name => name).ToArray());
    }

    [TestMethod]
    public void LoadEditSave_PreservesUnmodeledRootProperties()
    {
        const string json = """
            {
              "version": "1.0",
              "metadata": { "title": "Keep me", "tags": ["one", "two"] },
              "range": 90,
              "inverted": true,
              "custom": { "value": null, "enabled": false },
              "actions": [{ "at": 0, "pos": 10 }]
            }
            """;
        var script = Funscript.Load(WriteInput(json));
        script.AddOrReplace(new FunscriptAction(500, 50));
        var path = Path.Combine(_directory, "output.funscript");

        script.Save(path);

        using var original = JsonDocument.Parse(json);
        using var saved = JsonDocument.Parse(File.ReadAllText(path));
        foreach (var property in original.RootElement.EnumerateObject())
        {
            if (property.Name == "actions") continue;
            Assert.AreEqual(
                JsonSerializer.Serialize(property.Value),
                JsonSerializer.Serialize(saved.RootElement.GetProperty(property.Name)),
                $"Root property '{property.Name}' changed.");
        }

        Assert.AreEqual(2, saved.RootElement.GetProperty("actions").GetArrayLength());
        Assert.AreEqual(new FunscriptAction(500, 50), Funscript.Load(path).Actions[1]);
    }

    [TestMethod]
    public void ReplaceActionsAtomically_PreservesRootPropertiesThroughSave()
    {
        const string json = """
            { "version": "1.0", "metadata": { "title": "keep" }, "inverted": true,
              "actions": [{ "at": 0, "pos": 10 }, { "at": 4000, "pos": 70 }] }
            """;
        var script = Funscript.Load(WriteInput(json));
        var pattern = PatternGenerator.Generate(new PatternGenerationOptions(1, 2, 1));
        var candidate = PatternInsertionPlan.Create(script.Actions, pattern).Resolve(PatternConflictChoice.Replace);

        Assert.IsTrue(script.ReplaceActionsAtomically(candidate.Actions));
        CollectionAssert.AreEqual(new[]
        {
            new FunscriptAction(0, 10), new FunscriptAction(1000, 0),
            new FunscriptAction(2000, 100), new FunscriptAction(3000, 0),
            new FunscriptAction(4000, 70),
        }, script.Actions.ToArray());
        var path = Path.Combine(_directory, "replaced.funscript");
        script.Save(path);
        using var saved = JsonDocument.Parse(File.ReadAllText(path));

        Assert.AreEqual("1.0", saved.RootElement.GetProperty("version").GetString());
        Assert.AreEqual("keep", saved.RootElement.GetProperty("metadata").GetProperty("title").GetString());
        Assert.IsTrue(saved.RootElement.GetProperty("inverted").GetBoolean());
        CollectionAssert.AreEqual(candidate.Actions.ToArray(), Funscript.Load(path).Actions.ToArray());
    }

    [TestMethod]
    public void ToCsv_ReservesMetadataLineAndWritesOrderedActions()
    {
        var script = new Funscript();
        script.AddOrReplace(new FunscriptAction(1000, 100));
        script.AddOrReplace(new FunscriptAction(0, 0));

        Assert.AreEqual(
            $"{Environment.NewLine}0,0{Environment.NewLine}1000,100{Environment.NewLine}", script.ToCsv());
    }

    private string WriteInput(string json)
    {
        var path = Path.Combine(_directory, "input.funscript");
        File.WriteAllText(path, json);
        return path;
    }
}
