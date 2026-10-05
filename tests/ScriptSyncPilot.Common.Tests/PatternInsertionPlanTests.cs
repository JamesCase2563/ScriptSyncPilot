using Microsoft.VisualStudio.TestTools.UnitTesting;
using ScriptSyncPilot.Common;

namespace ScriptSyncPilot.Common.Tests;

[TestClass]
public sealed class PatternInsertionPlanTests
{
    private static GeneratedPattern Pattern() => PatternGenerator.Generate(new PatternGenerationOptions(1, 2, 1));

    private static FunscriptAction[] PopulatedDestination() =>
    [
        new(0, 10), new(1000, 20), new(1500, 30), new(2000, 40),
        new(2500, 50), new(3000, 60), new(4000, 70),
    ];

    [TestMethod]
    public void EmptyDestination_HasNoConflictsAndReturnsGeneratedCandidate()
    {
        var pattern = Pattern();
        var plan = PatternInsertionPlan.Create([], pattern);

        Assert.IsFalse(plan.HasConflicts);
        Assert.IsFalse(plan.RequiresMergeChoice);
        var candidate = plan.Resolve(PatternConflictChoice.Replace);
        Assert.IsTrue(candidate.HasChanges);
        CollectionAssert.AreEqual(pattern.Actions.ToArray(), candidate.Actions.ToArray());
    }

    [TestMethod]
    public void CrossingInterpolation_DoesNotCountAsConflict()
    {
        var plan = PatternInsertionPlan.Create([new(0, 10), new(4000, 70)], Pattern());

        Assert.IsFalse(plan.HasConflicts);
        CollectionAssert.AreEqual(new[]
        {
            new FunscriptAction(0, 10), new FunscriptAction(1000, 0), new FunscriptAction(2000, 100),
            new FunscriptAction(3000, 0), new FunscriptAction(4000, 70),
        }, plan.Resolve(PatternConflictChoice.Merge).Actions.ToArray());
    }

    [DataTestMethod]
    [DataRow(PatternConflictChoice.Replace, PatternMergeChoice.KeepExisting)]
    [DataRow(PatternConflictChoice.Merge, PatternMergeChoice.KeepExisting)]
    [DataRow(PatternConflictChoice.Merge, PatternMergeChoice.UseNew)]
    public void BoundaryOnlyPoints_ArePreservedWithoutConflict(PatternConflictChoice choice, PatternMergeChoice mergeChoice)
    {
        var plan = PatternInsertionPlan.Create([new(0, 10), new(1000, 20), new(3000, 60), new(4000, 70)], Pattern());

        Assert.IsFalse(plan.HasConflicts);
        Assert.IsFalse(plan.RequiresMergeChoice);
        Assert.IsTrue(plan.PreservesStartBoundary);
        Assert.IsTrue(plan.PreservesEndBoundary);
        CollectionAssert.AreEqual(new[]
        {
            new FunscriptAction(0, 10), new FunscriptAction(1000, 20), new FunscriptAction(2000, 100),
            new FunscriptAction(3000, 60), new FunscriptAction(4000, 70),
        }, plan.Resolve(choice, mergeChoice).Actions.ToArray());
    }

    [TestMethod]
    public void InteriorConflicts_ReportAllPointsAndOnlyDifferentTimestampCollisions()
    {
        var plan = PatternInsertionPlan.Create(PopulatedDestination(), Pattern());

        Assert.IsTrue(plan.HasConflicts);
        Assert.IsTrue(plan.RequiresMergeChoice);
        CollectionAssert.AreEqual(new[] { 1500, 2000, 2500 }, plan.ConflictingActions.Select(action => action.At).ToArray());
        Assert.AreEqual(1, plan.Collisions.Count);
        Assert.AreEqual(new PatternValueCollision(new(2000, 40), new(2000, 100)), plan.Collisions[0]);
    }

    [TestMethod]
    public void Stop_ReturnsOriginalSnapshotAndNoChanges()
    {
        var source = PopulatedDestination();
        var plan = PatternInsertionPlan.Create(source, Pattern());
        var candidate = plan.Resolve(PatternConflictChoice.Stop);

        Assert.IsFalse(candidate.HasChanges);
        CollectionAssert.AreEqual(source, candidate.Actions.ToArray());
    }

    [TestMethod]
    public void Replace_RemovesOnlyInteriorPoints()
    {
        var plan = PatternInsertionPlan.Create(PopulatedDestination(), Pattern());
        var candidate = plan.Resolve(PatternConflictChoice.Replace);

        Assert.IsTrue(candidate.HasChanges);
        CollectionAssert.AreEqual(new[]
        {
            new FunscriptAction(0, 10), new FunscriptAction(1000, 20), new FunscriptAction(2000, 100),
            new FunscriptAction(3000, 60), new FunscriptAction(4000, 70),
        }, candidate.Actions.ToArray());
    }

    [TestMethod]
    public void MergeKeepExisting_DetectsNoOp()
    {
        var source = PopulatedDestination();
        var candidate = PatternInsertionPlan.Create(source, Pattern()).Resolve(PatternConflictChoice.Merge, PatternMergeChoice.KeepExisting);

        Assert.IsFalse(candidate.HasChanges);
        CollectionAssert.AreEqual(source, candidate.Actions.ToArray());
    }

    [TestMethod]
    public void MergeUseNew_ReplacesInteriorCollisionButNotBoundaries()
    {
        var candidate = PatternInsertionPlan.Create(PopulatedDestination(), Pattern()).Resolve(PatternConflictChoice.Merge, PatternMergeChoice.UseNew);

        Assert.IsTrue(candidate.HasChanges);
        CollectionAssert.AreEqual(new[]
        {
            new FunscriptAction(0, 10), new FunscriptAction(1000, 20), new FunscriptAction(1500, 30),
            new FunscriptAction(2000, 100), new FunscriptAction(2500, 50), new FunscriptAction(3000, 60), new FunscriptAction(4000, 70),
        }, candidate.Actions.ToArray());
    }

    [DataTestMethod]
    [DataRow(PatternMergeChoice.KeepExisting)]
    [DataRow(PatternMergeChoice.UseNew)]
    public void IdenticalInteriorPairs_DeduplicateWithoutRequiringCollisionChoice(PatternMergeChoice mergeChoice)
    {
        var plan = PatternInsertionPlan.Create([new(0, 10), new(1500, 30), new(2000, 100), new(4000, 70)], Pattern());

        Assert.IsTrue(plan.HasConflicts);
        Assert.IsFalse(plan.RequiresMergeChoice);
        CollectionAssert.AreEqual(new[]
        {
            new FunscriptAction(0, 10), new FunscriptAction(1000, 0), new FunscriptAction(1500, 30),
            new FunscriptAction(2000, 100), new FunscriptAction(3000, 0), new FunscriptAction(4000, 70),
        }, plan.Resolve(PatternConflictChoice.Merge, mergeChoice).Actions.ToArray());
    }

    [DataTestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public void SingleExistingBoundary_PreservesItAndEmitsMissingEndpoint(bool hasStart)
    {
        FunscriptAction[] source = hasStart ? [new(1000, 20)] : [new(3000, 60)];
        var plan = PatternInsertionPlan.Create(source, Pattern());

        Assert.IsFalse(plan.HasConflicts);
        Assert.AreEqual(hasStart, plan.PreservesStartBoundary);
        Assert.AreEqual(!hasStart, plan.PreservesEndBoundary);
        CollectionAssert.AreEqual(new[]
        {
            new FunscriptAction(1000, hasStart ? 20 : 0), new FunscriptAction(2000, 100), new FunscriptAction(3000, hasStart ? 0 : 60),
        }, plan.Resolve(PatternConflictChoice.Replace).Actions.ToArray());
    }

    [TestMethod]
    public void CreateAndResolve_NeverMutateSourceScriptOrGeneratedActions()
    {
        var script = new Funscript();
        foreach (var action in PopulatedDestination()) script.AddOrReplace(action);
        var original = script.Actions.ToArray();
        var pattern = Pattern();
        var generated = pattern.Actions.ToArray();
        var plan = PatternInsertionPlan.Create(script.Actions, pattern);

        foreach (var choice in Enum.GetValues<PatternConflictChoice>())
        {
            foreach (var mergeChoice in Enum.GetValues<PatternMergeChoice>())
            {
                var first = plan.Resolve(choice, mergeChoice);
                var second = plan.Resolve(choice, mergeChoice);
                CollectionAssert.AreEqual(first.Actions.ToArray(), second.Actions.ToArray());
                CollectionAssert.AreEqual(original, script.Actions.ToArray());
                CollectionAssert.AreEqual(generated, pattern.Actions.ToArray());
            }
        }
    }

    [TestMethod]
    public void PlanOwnsSnapshots_WhenCallersChangeTheirCollections()
    {
        var source = PopulatedDestination();
        var expectedOriginal = source.ToArray();
        var generated = Pattern().Actions.ToArray();
        var pattern = Pattern() with { Actions = generated };
        var plan = PatternInsertionPlan.Create(source, pattern);
        source[3] = new FunscriptAction(2000, 90);
        generated[1] = new FunscriptAction(2000, 10);

        CollectionAssert.AreEqual(expectedOriginal, plan.OriginalActions.ToArray());
        Assert.AreEqual(new FunscriptAction(2000, 100), plan.GeneratedActions[1]);
        Assert.AreEqual(new PatternValueCollision(new(2000, 40), new(2000, 100)), plan.Collisions[0]);
        Assert.AreEqual(new FunscriptAction(2000, 100), plan.Resolve(PatternConflictChoice.Replace).Actions[2]);
        var newerPlan = PatternInsertionPlan.Create(source, Pattern());
        Assert.AreEqual(90, newerPlan.Collisions[0].Existing.Pos);
    }

    [TestMethod]
    public void PlanAndCandidates_ExposeOnlyReadOnlyCollections()
    {
        var plan = PatternInsertionPlan.Create(PopulatedDestination(), Pattern());
        var lists = new[]
        {
            plan.OriginalActions, plan.GeneratedActions, plan.ConflictingActions,
            plan.Resolve(PatternConflictChoice.Stop).Actions,
            plan.Resolve(PatternConflictChoice.Replace).Actions,
            plan.Resolve(PatternConflictChoice.Merge).Actions,
        };
        foreach (var list in lists)
            Assert.ThrowsException<NotSupportedException>(() => ((IList<FunscriptAction>)list)[0] = new FunscriptAction(0, 0));
        Assert.ThrowsException<NotSupportedException>(() => ((IList<PatternValueCollision>)plan.Collisions).Clear());
    }

    [TestMethod]
    public void InvalidDestinationActions_AreRejectedWithoutChangingSource()
    {
        FunscriptAction[][] invalid =
        [
            [new(2000, 50), new(1000, 50)],
            [new(1000, 20), new(1000, 30)],
            [new(-1, 50)],
            [new(1000, -1)],
            [new(1000, 101)],
        ];
        foreach (var source in invalid)
        {
            var original = source.ToArray();
            Assert.ThrowsException<ArgumentException>(() => PatternInsertionPlan.Create(source, Pattern()));
            CollectionAssert.AreEqual(original, source);
        }
    }

    [TestMethod]
    public void InvalidGeneratedSections_AreRejectedBeforePlanning()
    {
        var pattern = Pattern();
        var invalid = new[]
        {
            pattern with { Actions = Array.Empty<FunscriptAction>() },
            pattern with { Actions = new[] { new FunscriptAction(1000, 0) } },
            pattern with { Actions = new[] { new FunscriptAction(3000, 0), new FunscriptAction(1000, 0) } },
            pattern with { Actions = new[] { new FunscriptAction(1000, 0), new FunscriptAction(1000, 100), new FunscriptAction(3000, 0) } },
            pattern with { Actions = new[] { new FunscriptAction(1000, 0), new FunscriptAction(2000, 101), new FunscriptAction(3000, 0) } },
            pattern with { StartMs = 999 }, pattern with { EndMs = 3001 },
            pattern with { StartMs = -1 }, pattern with { EndMs = 1000 },
        };
        var source = PopulatedDestination();
        var copy = source.ToArray();
        foreach (var candidate in invalid)
        {
            Assert.ThrowsException<ArgumentException>(() => PatternInsertionPlan.Create(source, candidate));
            CollectionAssert.AreEqual(copy, source);
        }
    }

    [TestMethod]
    public void Resolve_RejectsUnknownPolicies()
    {
        var plan = PatternInsertionPlan.Create(PopulatedDestination(), Pattern());

        Assert.ThrowsException<ArgumentOutOfRangeException>(() => plan.Resolve((PatternConflictChoice)99));
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => plan.Resolve(PatternConflictChoice.Merge, (PatternMergeChoice)99));
    }

    [TestMethod]
    public void BoundaryOnlyGeneratedSection_CanResolveToNoOp()
    {
        var pattern = Pattern() with { Actions = new[] { new FunscriptAction(1000, 0), new FunscriptAction(3000, 0) } };
        FunscriptAction[] source = [new(1000, 20), new(3000, 60)];
        var plan = PatternInsertionPlan.Create(source, pattern);

        Assert.IsFalse(plan.HasConflicts);
        Assert.IsFalse(plan.Resolve(PatternConflictChoice.Replace).HasChanges);
        CollectionAssert.AreEqual(source, plan.Resolve(PatternConflictChoice.Merge, PatternMergeChoice.UseNew).Actions.ToArray());
    }
}