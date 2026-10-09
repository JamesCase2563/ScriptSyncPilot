using Microsoft.VisualStudio.TestTools.UnitTesting;
using ScriptSyncPilot.Common;

namespace ScriptSyncPilot.Common.Tests;

[TestClass]
public sealed class PatternGeneratorTests
{
    [TestMethod]
    public void Generate_TriangleMatchesExactFixture()
    {
        var result = PatternGenerator.Generate(new PatternGenerationOptions(1, 2, 1));

        CollectionAssert.AreEqual(new[] { new FunscriptAction(1000, 0), new FunscriptAction(2000, 100), new FunscriptAction(3000, 0) }, result.Actions.ToArray());
        Assert.AreEqual(30m, result.EffectiveCyclesPerMinute);
    }

    [TestMethod]
    public void Generate_FastUpUsesQuarterCycleRise()
    {
        var result = PatternGenerator.Generate(new PatternGenerationOptions(1, 2, 1) { Profile = PatternProfile.FastUpSlowDown });

        CollectionAssert.AreEqual(new[] { new FunscriptAction(1000, 0), new FunscriptAction(1500, 100), new FunscriptAction(3000, 0) }, result.Actions.ToArray());
    }

    [TestMethod]
    public void Generate_FractionalPeriodsDoNotAccumulateDrift()
    {
        var result = PatternGenerator.Generate(new PatternGenerationOptions(1, 1, 3));

        CollectionAssert.AreEqual(new[]
        {
            new FunscriptAction(1000, 0), new FunscriptAction(1167, 100), new FunscriptAction(1333, 0),
            new FunscriptAction(1500, 100), new FunscriptAction(1667, 0), new FunscriptAction(1833, 100), new FunscriptAction(2000, 0),
        }, result.Actions.ToArray());
    }

    [TestMethod]
    public void Generate_RoundsStartAndDurationOnce()
    {
        var result = PatternGenerator.Generate(new PatternGenerationOptions(1.2345m, 2.0005m, 1));

        CollectionAssert.AreEqual(new[] { new FunscriptAction(1235, 0), new FunscriptAction(2236, 100), new FunscriptAction(3236, 0) }, result.Actions.ToArray());
    }

    [TestMethod]
    public void Generate_CadenceComesDirectlyFromOutputCountAndDuration()
    {
        var result = PatternGenerator.Generate(new PatternGenerationOptions(0, 12, 5));

        Assert.AreEqual(25m, result.EffectiveCyclesPerMinute);
    }

    [TestMethod]
    public void Generate_CosineRefinesBeyondExtrema()
    {
        var result = PatternGenerator.Generate(new PatternGenerationOptions(0, 1, 1) { Profile = PatternProfile.SlowedDirectionChanges });

        Assert.IsTrue(result.Actions.Count > 3);
        Assert.AreEqual(new FunscriptAction(0, 0), result.Actions[0]);
        Assert.AreEqual(new FunscriptAction(1000, 0), result.Actions[^1]);
        for (var index = 1; index < result.Actions.Count; index++)
        {
            var left = result.Actions[index - 1];
            var right = result.Actions[index];
            Assert.IsTrue(right.At > left.At);
            for (var step = 0; step <= 100; step++)
            {
                var fraction = step / 100.0;
                var time = left.At + fraction * (right.At - left.At);
                var expected = 50 * (1 - Math.Cos(2 * Math.PI * time / 1000));
                var actual = left.Pos + fraction * (right.Pos - left.Pos);
                Assert.IsTrue(Math.Abs(expected - actual) <= 1 + 1e-9, $"Error at {time} ms.");
            }
        }
    }

    [TestMethod]
    public void Generate_CollapsedMandatoryTimesAreRejected()
    {
        Assert.ThrowsException<ArgumentException>(() => PatternGenerator.Generate(new PatternGenerationOptions(0, 0.001m, 2)));
    }

    [TestMethod]
    public void Generate_RangeAndSharedBoundariesMatchFixture()
    {
        var result = PatternGenerator.Generate(new PatternGenerationOptions(0, 4, 2) { MinimumPosition = 20, MaximumPosition = 80 });

        CollectionAssert.AreEqual(new[]
        {
            new FunscriptAction(0, 20), new FunscriptAction(1000, 80), new FunscriptAction(2000, 20),
            new FunscriptAction(3000, 80), new FunscriptAction(4000, 20),
        }, result.Actions.ToArray());
    }

    [TestMethod]
    public void Generate_DirectCycleCountSetsOutputCadence()
    {
        var result = PatternGenerator.Generate(new PatternGenerationOptions(0, 12, 5));
        var changedCount = PatternGenerator.Generate(new PatternGenerationOptions(0, 12, 4));

        Assert.AreEqual(25m, result.EffectiveCyclesPerMinute);
        Assert.AreEqual(20m, changedCount.EffectiveCyclesPerMinute);
        Assert.AreEqual(11, result.Actions.Count);
        Assert.AreEqual(12000, result.EndMs);
        Assert.AreEqual(0, result.Actions[^1].Pos);
    }

    [DataTestMethod]
    [DataRow(0, 100, 0.25)]
    [DataRow(100, 0, 0.75)]
    [DataRow(50, 50, 0.25)]
    public void Generate_ContinuationResolvesInterpolatedValueAndDirection(int first, int second, double expectedPhase)
    {
        var source = new[] { new FunscriptAction(0, first), new FunscriptAction(1000, second) };
        var copy = source.ToArray();
        var options = new PatternGenerationOptions(0.5m, 2, 1) { StartMode = PatternStartMode.ContinueExisting };

        var result = PatternGenerator.Generate(options, source);

        Assert.AreEqual((decimal)expectedPhase, result.StartPhase);
        Assert.IsFalse(result.UsedPatternStartFallback);
        Assert.AreEqual(50, result.Actions[0].Pos);
        Assert.AreEqual(50, result.Actions[^1].Pos);
        Assert.AreEqual(2500, result.EndMs);
        CollectionAssert.AreEqual(copy, source);
    }

    [TestMethod]
    public void Generate_ContinuationAtPeakUsesDescendingOutgoingBranch()
    {
        var source = new[] { new FunscriptAction(0, 0), new FunscriptAction(1000, 100), new FunscriptAction(2000, 0) };
        var result = PatternGenerator.Generate(new PatternGenerationOptions(1, 2, 1) { StartMode = PatternStartMode.ContinueExisting }, source);

        Assert.AreEqual(0.5m, result.StartPhase);
        CollectionAssert.AreEqual(new[] { new FunscriptAction(1000, 100), new FunscriptAction(2000, 0), new FunscriptAction(3000, 100) }, result.Actions.ToArray());
    }

    [TestMethod]
    public void Generate_ContinuationPrefersIncomingDirectionAtExactPoint()
    {
        var source = new[] { new FunscriptAction(0, 0), new FunscriptAction(1000, 50), new FunscriptAction(2000, 0) };
        var result = PatternGenerator.Generate(new PatternGenerationOptions(1, 2, 1) { StartMode = PatternStartMode.ContinueExisting }, source);

        Assert.AreEqual(0.25m, result.StartPhase);
    }

    [TestMethod]
    public void Generate_ContinuationUsesOutgoingWhenIncomingIsFlat()
    {
        var source = new[] { new FunscriptAction(0, 50), new FunscriptAction(1000, 50), new FunscriptAction(2000, 0) };
        var result = PatternGenerator.Generate(new PatternGenerationOptions(1, 2, 1) { StartMode = PatternStartMode.ContinueExisting }, source);

        Assert.AreEqual(0.75m, result.StartPhase);
    }

    [DataTestMethod]
    [DataRow(0)]
    [DataRow(3)]
    public void Generate_ContinuationDoesNotExtrapolate(int start)
    {
        var source = new[] { new FunscriptAction(1000, 0), new FunscriptAction(2000, 100) };
        var result = PatternGenerator.Generate(new PatternGenerationOptions(start, 2, 1) { StartMode = PatternStartMode.ContinueExisting }, source);

        Assert.IsTrue(result.UsedPatternStartFallback);
        Assert.AreEqual(0m, result.StartPhase);
        Assert.AreEqual(0, result.Actions[0].Pos);
    }

    [TestMethod]
    public void Generate_ContinuationOutOfRangeAndMissingSourceFallBack()
    {
        var options = new PatternGenerationOptions(1, 2, 1)
        {
            StartMode = PatternStartMode.ContinueExisting, MinimumPosition = 20, MaximumPosition = 80,
        };
        var result = PatternGenerator.Generate(options, new[] { new FunscriptAction(1000, 100) });

        Assert.IsTrue(result.UsedPatternStartFallback);
        Assert.AreEqual(20, result.Actions[0].Pos);
        Assert.IsTrue(PatternGenerator.Generate(options).UsedPatternStartFallback);
    }

    [TestMethod]
    public void Generate_ContinuationRoundsHalfPositionsAwayFromZero()
    {
        var source = new[] { new FunscriptAction(0, 50), new FunscriptAction(1000, 51) };
        var result = PatternGenerator.Generate(new PatternGenerationOptions(0.5m, 2, 1) { StartMode = PatternStartMode.ContinueExisting }, source);

        Assert.AreEqual(0.2525m, result.StartPhase);
        Assert.AreEqual(51, result.Actions[0].Pos);
        Assert.AreEqual(51, result.Actions[^1].Pos);
    }

    [TestMethod]
    public void Generate_CosineErrorBoundHoldsAtAnalyticExtremaIncludingShiftedPhase()
    {
        foreach (var startMode in new[] { PatternStartMode.PatternStart, PatternStartMode.ContinueExisting })
        {
            var options = new PatternGenerationOptions(1, 3.007m, 7)
            {
                Profile = PatternProfile.SlowedDirectionChanges, StartMode = startMode,
            };
            var source = new[] { new FunscriptAction(0, 0), new FunscriptAction(2000, 100) };
            var result = PatternGenerator.Generate(options, source);
            var repeated = PatternGenerator.Generate(options, source);
            CollectionAssert.AreEqual(result.Actions.ToArray(), repeated.Actions.ToArray());
            AssertCosineErrorBound(result, options);
        }
    }

    [TestMethod]
    public void Generate_SectionDurationLimitIsConfigurable()
    {
        Assert.AreEqual(10800000, PatternGenerator.Generate(new PatternGenerationOptions(0, 10800, 1)).EndMs);
        Assert.ThrowsException<ArgumentException>(() => PatternGenerator.Generate(new PatternGenerationOptions(0, 10800.001m, 1)));
        Assert.AreEqual(10800001, PatternGenerator.Generate(new PatternGenerationOptions(0, 10800.001m, 1) { MaximumDurationSeconds = 10801 }).EndMs);
    }

    [TestMethod]
    public void Generate_ActionCapCheckedBeforeAllocatingLargeOutput()
    {
        var result = PatternGenerator.Generate(new PatternGenerationOptions(0, 10000, 49999));

        Assert.AreEqual(99999, result.Actions.Count);
        Assert.ThrowsException<ArgumentException>(() => PatternGenerator.Generate(new PatternGenerationOptions(0, 10000, 50000)));
        Assert.ThrowsException<ArgumentException>(() => PatternGenerator.Generate(new PatternGenerationOptions(0, 1, int.MaxValue)));
    }

    [TestMethod]
    public void Generate_AdaptiveSamplingCannotExceedActionCap()
    {
        Assert.ThrowsException<ArgumentException>(() => PatternGenerator.Generate(new PatternGenerationOptions(0, 10000, 10000)
        {
            Profile = PatternProfile.SlowedDirectionChanges,
        }));
    }

    [TestMethod]
    public void Generate_InvalidInputsAndOverflowAreRejected()
    {
        var valid = new PatternGenerationOptions(0, 2, 1);
        var invalid = new[]
        {
            valid with { StartSeconds = -1 }, valid with { DurationSeconds = 0 }, valid with { CycleCount = 0 },
            valid with { MinimumPosition = -1 }, valid with { MaximumPosition = 101 }, valid with { MinimumPosition = 100 },
            valid with { RiseFraction = 0 }, valid with { RiseFraction = 1 }, valid with { MaximumDurationSeconds = 0 },
            valid with { Profile = (PatternProfile)99 }, valid with { StartMode = (PatternStartMode)99 },
            valid with { StartSeconds = int.MaxValue / 1000m }, valid with { DurationSeconds = 0.0004m },
        };
        foreach (var options in invalid)
            Assert.ThrowsException<ArgumentException>(() => PatternGenerator.Generate(options), $"Accepted {options}");
    }

    [TestMethod]
    public void Generate_InvalidContinuationSnapshotIsRejectedWithoutMutation()
    {
        var options = new PatternGenerationOptions(0, 2, 1) { StartMode = PatternStartMode.ContinueExisting };
        var source = new[] { new FunscriptAction(1000, 50), new FunscriptAction(0, 10) };
        var copy = source.ToArray();

        Assert.ThrowsException<ArgumentException>(() => PatternGenerator.Generate(options, source));
        CollectionAssert.AreEqual(copy, source);
    }

    [TestMethod]
    public void Generate_ResultCannotBeMutatedAndDoesNotEditSourceScript()
    {
        var script = new Funscript();
        script.AddOrReplace(new FunscriptAction(0, 0));
        script.AddOrReplace(new FunscriptAction(1000, 100));
        var copy = script.Actions.ToArray();
        var result = PatternGenerator.Generate(new PatternGenerationOptions(0.5m, 2, 1) { StartMode = PatternStartMode.ContinueExisting }, script.Actions);

        Assert.ThrowsException<NotSupportedException>(() => ((IList<FunscriptAction>)result.Actions).Add(new FunscriptAction(4000, 50)));
        CollectionAssert.AreEqual(copy, script.Actions.ToArray());
        Assert.AreEqual(2500, result.EndMs);
    }

    [TestMethod]
    public void Generate_CustomRiseAndFastContinuationUseRelativeProfileTimes()
    {
        var configured = PatternGenerator.Generate(new PatternGenerationOptions(0, 2, 1)
        {
            Profile = PatternProfile.FastUpSlowDown, RiseFraction = 0.4m,
        });
        CollectionAssert.AreEqual(new[] { new FunscriptAction(0, 0), new FunscriptAction(800, 100), new FunscriptAction(2000, 0) }, configured.Actions.ToArray());

        var continued = PatternGenerator.Generate(new PatternGenerationOptions(0.5m, 2, 1)
        {
            Profile = PatternProfile.FastUpSlowDown, StartMode = PatternStartMode.ContinueExisting,
        }, new[] { new FunscriptAction(0, 100), new FunscriptAction(1000, 0) });
        Assert.AreEqual(0.625m, continued.StartPhase);
        Assert.AreEqual(50, continued.Actions[0].Pos);
        Assert.AreEqual(50, continued.Actions[^1].Pos);
    }

    [TestMethod]
    public void Generate_UnrepresentableLinearSegmentIsRejectedInsteadOfRelaxingAccuracy()
    {
        Assert.ThrowsException<ArgumentException>(() => PatternGenerator.Generate(new PatternGenerationOptions(0, 1, 1)
        {
            Profile = PatternProfile.FastUpSlowDown, RiseFraction = 0.0005m,
        }));
    }

    [TestMethod]
    public void Generate_FutureStartIsNotLimitedByExistingScriptExtent()
    {
        var source = new[] { new FunscriptAction(0, 0), new FunscriptAction(1000, 100) };
        var result = PatternGenerator.Generate(new PatternGenerationOptions(70, 2, 1), source);

        Assert.AreEqual(70000, result.StartMs);
        Assert.AreEqual(72000, result.EndMs);
        Assert.AreEqual(2, source.Length);
    }

    private static void AssertCosineErrorBound(GeneratedPattern result, PatternGenerationOptions options)
    {
        var amplitude = (options.MaximumPosition - options.MinimumPosition) / 2.0;
        var angularRate = 2 * Math.PI * options.CycleCount / (result.EndMs - result.StartMs);
        var angleOffset = 2 * Math.PI * (double)result.StartPhase;
        for (var index = 1; index < result.Actions.Count; index++)
        {
            var left = result.Actions[index - 1];
            var right = result.Actions[index];
            var slope = (double)(right.Pos - left.Pos) / (right.At - left.At);
            var times = new List<double> { left.At, right.At };
            var ratio = slope / (amplitude * angularRate);
            if (Math.Abs(ratio) <= 1)
            {
                var angle = Math.Asin(ratio);
                for (var turn = -1; turn <= options.CycleCount + 1; turn++)
                {
                    foreach (var root in new[] { angle, Math.PI - angle })
                    {
                        var time = result.StartMs + (root + turn * 2 * Math.PI - angleOffset) / angularRate;
                        if (time > left.At && time < right.At) times.Add(time);
                    }
                }
            }
            foreach (var time in times)
            {
                var expected = options.MinimumPosition + amplitude * (1 - Math.Cos(angleOffset + (time - result.StartMs) * angularRate));
                var actual = left.Pos + slope * (time - left.At);
                Assert.IsTrue(Math.Abs(expected - actual) <= 1 + 1e-9, $"Error at {time} ms, phase {result.StartPhase}.");
            }
        }
    }
}