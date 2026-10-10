using Microsoft.VisualStudio.TestTools.UnitTesting;
using ScriptSyncPilot.Common;

namespace ScriptSyncPilot.Common.Tests;

[TestClass]
public sealed class ActionHistoryTests
{
    private static FunscriptAction A(int at, int pos) => new(at, pos);

    private static Funscript Script(params FunscriptAction[] actions)
    {
        var script = new Funscript();
        foreach (var action in actions) script.AddOrReplace(action);
        return script;
    }

    private static ActionEdit? Edit(Funscript script, Action<Funscript> mutate)
    {
        var before = script.Actions.ToArray();
        mutate(script);
        return ActionEdit.Between(before, script.Actions);
    }

    [TestMethod]
    public void Between_ReturnsOnlyTheDifferingSection()
    {
        var before = new[] { A(0, 0), A(1000, 50), A(2000, 100), A(3000, 0) };
        var after = new[] { A(0, 0), A(1000, 60), A(1500, 80), A(2000, 100), A(3000, 0) };

        var edit = ActionEdit.Between(before, after)!;

        Assert.AreEqual(1, edit.Index);
        CollectionAssert.AreEqual(new[] { A(1000, 50) }, edit.Removed.ToArray());
        CollectionAssert.AreEqual(new[] { A(1000, 60), A(1500, 80) }, edit.Inserted.ToArray());
        Assert.IsNull(ActionEdit.Between(before, before.ToArray()));
    }

    [TestMethod]
    public void UndoRedo_MixedEditsRestoreEachStateInOrder()
    {
        var script = Script(A(0, 0), A(1000, 100));
        var history = new ActionHistory();
        var states = new List<FunscriptAction[]> { script.Actions.ToArray() };

        history.Record(Edit(script, s => s.ReplaceActionsAtomically([A(0, 0), A(250, 100), A(500, 0), A(750, 100), A(1000, 100)])));
        states.Add(script.Actions.ToArray());
        history.Record(Edit(script, s => s.AddOrReplace(A(1500, 40))));
        states.Add(script.Actions.ToArray());
        history.Record(Edit(script, s => s.RemoveAt(2)));
        states.Add(script.Actions.ToArray());
        history.Record(Edit(script, s =>
        {
            // A drag is many intermediate moves recorded as one entry.
            s.UpdateAt(1, A(260, 90));
            s.UpdateAt(1, A(300, 70));
        }));
        states.Add(script.Actions.ToArray());

        for (var index = states.Count - 2; index >= 0; index--)
        {
            Assert.IsNotNull(history.Undo(script));
            CollectionAssert.AreEqual(states[index], script.Actions.ToArray());
        }
        Assert.IsNull(history.Undo(script));

        for (var index = 1; index < states.Count; index++)
        {
            Assert.IsNotNull(history.Redo(script));
            CollectionAssert.AreEqual(states[index], script.Actions.ToArray());
        }
        Assert.IsNull(history.Redo(script));
    }

    [TestMethod]
    public void Record_NewEditClearsRedoButNoOpDoesNot()
    {
        var script = Script(A(0, 0));
        var history = new ActionHistory();
        history.Record(Edit(script, s => s.AddOrReplace(A(1000, 100))));
        history.Record(Edit(script, s => s.AddOrReplace(A(2000, 0))));
        history.Undo(script);

        Assert.IsFalse(history.Record(Edit(script, s => s.ReplaceActionsAtomically(s.Actions.ToArray()))));
        Assert.IsTrue(history.CanRedo);

        Assert.IsTrue(history.Record(Edit(script, s => s.AddOrReplace(A(1500, 50)))));
        Assert.IsFalse(history.CanRedo);
        Assert.IsTrue(history.CanUndo);
    }

    [TestMethod]
    public void SavedCheckpoint_TracksDirtyAcrossUndoRedoAndBranching()
    {
        var script = Script(A(0, 0));
        var history = new ActionHistory();
        Assert.IsFalse(history.IsDirty);

        history.Record(Edit(script, s => s.AddOrReplace(A(1000, 100))));
        Assert.IsTrue(history.IsDirty);
        history.MarkSaved();
        Assert.IsFalse(history.IsDirty);

        history.Undo(script);
        Assert.IsTrue(history.IsDirty);
        history.Redo(script);
        Assert.IsFalse(history.IsDirty);

        history.Undo(script);
        history.Record(Edit(script, s => s.AddOrReplace(A(2000, 50))));
        Assert.IsTrue(history.IsDirty, "Branching to an equally long history must not match the saved checkpoint.");

        history.Reset();
        Assert.IsFalse(history.IsDirty);
        Assert.IsFalse(history.CanUndo);
        Assert.IsFalse(history.CanRedo);
    }

    [TestMethod]
    public void ApplyEdit_RejectsEditThatDoesNotMatchCurrentActions()
    {
        var script = Script(A(0, 0), A(1000, 100));
        var edit = ActionEdit.Between(script.Actions.ToArray(), [A(0, 0), A(1000, 50)])!;
        script.AddOrReplace(A(1000, 75));

        Assert.ThrowsException<InvalidOperationException>(() => script.ApplyEdit(edit));
        CollectionAssert.AreEqual(new[] { A(0, 0), A(1000, 75) }, script.Actions.ToArray());
    }

    [TestMethod]
    public void ApplyEdit_RejectsShorterSourceEditThatCrossesUntouchedSuffix()
    {
        var original = new[] { A(0, 0), A(1000, 100) };
        var script = Script(original);
        var edit = ActionEdit.Between([A(0, 0)], [A(2000, 0)])!;

        Assert.ThrowsException<ArgumentException>(() => script.ApplyEdit(edit));
        CollectionAssert.AreEqual(original, script.Actions.ToArray());
    }

    [DataTestMethod]
    [DataRow(-1, 50)]
    [DataRow(0, 50)]
    [DataRow(2000, 50)]
    [DataRow(2500, 50)]
    [DataRow(1000, -1)]
    [DataRow(1000, 101)]
    public void ApplyEdit_RejectsInvalidCandidateWithoutMutation(int at, int pos)
    {
        var original = new[] { A(0, 0), A(1000, 50), A(2000, 100) };
        var script = Script(original);
        var edit = ActionEdit.Between([A(0, 0), A(1000, 50)], [A(0, 0), A(at, pos)])!;

        Assert.ThrowsException<ArgumentException>(() => script.ApplyEdit(edit));
        CollectionAssert.AreEqual(original, script.Actions.ToArray());
    }

    [TestMethod]
    public void UndoRedo_PreservesRootMetadata()
    {
        var path = Path.Combine(Path.GetTempPath(), $"ssp-history-{Guid.NewGuid():N}.funscript");
        try
        {
            File.WriteAllText(path, """{"version":"1.0","inverted":true,"actions":[{"at":0,"pos":0},{"at":1000,"pos":100}]}""");
            var script = Funscript.Load(path);
            var history = new ActionHistory();
            history.Record(Edit(script, s => s.AddOrReplace(A(500, 50))));
            history.Undo(script);
            history.Redo(script);
            script.Save(path);

            var json = File.ReadAllText(path);
            StringAssert.Contains(json, "\"version\"");
            StringAssert.Contains(json, "\"inverted\": true");
            CollectionAssert.AreEqual(new[] { A(0, 0), A(500, 50), A(1000, 100) }, Funscript.Load(path).Actions.ToArray());
        }
        finally
        {
            File.Delete(path);
        }
    }
}
