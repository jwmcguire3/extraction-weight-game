#nullable enable
using ExtractionWeight.Core;
using ExtractionWeight.Weight;
using NUnit.Framework;

namespace ExtractionWeight.Tests.EditMode
{
    public class WeightSystemTests
    {
        private const float FloatTolerance = 0.0001f;

        [Test]
        public void EmptyCarryState_ReturnsBaselinePenalties()
        {
            var carryState = new CarryState(carryCapacity: 1f);
            var penalties = WeightPenaltyCalculator.Compute(carryState, ZoneAxisWeights.Uniform);

            Assert.That(penalties.NoiseMultiplier, Is.EqualTo(1f).Within(FloatTolerance));
            Assert.That(penalties.SilhouetteMultiplier, Is.EqualTo(1f).Within(FloatTolerance));
            Assert.That(penalties.HandlingMultiplier, Is.EqualTo(1f).Within(FloatTolerance));
            Assert.That(penalties.MobilityMultiplier, Is.EqualTo(1f).Within(FloatTolerance));
        }

        [Test]
        public void AddingItems_AccumulatesTotalCost()
        {
            var carryState = new CarryState(carryCapacity: 3f);
            var itemA = new TestLoadoutItem("a", new CostSignature(0.10f, 0.20f, 0.30f, 0.40f));
            var itemB = new TestLoadoutItem("b", new CostSignature(0.25f, 0.15f, 0.10f, 0.05f));

            Assert.That(carryState.TryAdd(itemA), Is.True);
            Assert.That(carryState.TryAdd(itemB), Is.True);

            Assert.That(carryState.TotalCost.Noise, Is.EqualTo(0.35f).Within(FloatTolerance));
            Assert.That(carryState.TotalCost.Silhouette, Is.EqualTo(0.35f).Within(FloatTolerance));
            Assert.That(carryState.TotalCost.Handling, Is.EqualTo(0.40f).Within(FloatTolerance));
            Assert.That(carryState.TotalCost.Mobility, Is.EqualTo(0.45f).Within(FloatTolerance));
        }

        [Test]
        public void CapacityFraction_MatchesExpectedMagnitudeOverCapacity()
        {
            var carryState = new CarryState(carryCapacity: 2f);
            var item = new TestLoadoutItem("single", new CostSignature(1f, 0f, 0f, 0f));

            carryState.TryAdd(item);

            Assert.That(carryState.CapacityFraction, Is.EqualTo(0.5f).Within(FloatTolerance));
        }

        [Test]
        public void BreakpointTransitions_HappenAtExpectedThresholds()
        {
            var capacity = 1f;

            var light = new CarryState(capacity);
            light.TryAdd(new TestLoadoutItem("light", new CostSignature(0.39f, 0f, 0f, 0f)));
            Assert.That(light.CurrentBreakpoint, Is.EqualTo(CarryBreakpoint.Light));

            var loaded = new CarryState(capacity);
            loaded.TryAdd(new TestLoadoutItem("loaded", new CostSignature(0.40f, 0f, 0f, 0f)));
            Assert.That(loaded.CurrentBreakpoint, Is.EqualTo(CarryBreakpoint.Loaded));

            var overburdened = new CarryState(capacity);
            overburdened.TryAdd(new TestLoadoutItem("overburdened", new CostSignature(0.80f, 0f, 0f, 0f)));
            Assert.That(overburdened.CurrentBreakpoint, Is.EqualTo(CarryBreakpoint.Overburdened));

            var softCeiling = new CarryState(capacity);
            softCeiling.TryAdd(new TestLoadoutItem("soft-a", new CostSignature(1.0f, 0f, 0f, 0f)));
            softCeiling.TryAdd(new TestLoadoutItem("soft-b", new CostSignature(0.01f, 0f, 0f, 0f)));
            Assert.That(softCeiling.CurrentBreakpoint, Is.EqualTo(CarryBreakpoint.SoftCeiling));
        }

        [Test]
        public void PenaltyCurve_IsMonotonicAsCapacityIncreases()
        {
            var carryState = new CarryState(1f);
            var weights = ZoneAxisWeights.Uniform;
            var previous = WeightPenaltyCalculator.Compute(carryState, weights);

            for (var sample = 1; sample <= 120; sample++)
            {
                carryState.Clear();
                var load = sample / 100f;
                AddNoiseAxisLoad(carryState, $"sample-{sample}", load);

                var current = WeightPenaltyCalculator.Compute(carryState, weights);
                Assert.That(current.NoiseMultiplier, Is.GreaterThanOrEqualTo(previous.NoiseMultiplier));
                Assert.That(current.SilhouetteMultiplier, Is.GreaterThanOrEqualTo(previous.SilhouetteMultiplier));
                Assert.That(current.HandlingMultiplier, Is.GreaterThanOrEqualTo(previous.HandlingMultiplier));
                Assert.That(current.MobilityMultiplier, Is.LessThanOrEqualTo(previous.MobilityMultiplier));

                previous = current;
            }
        }

        [Test]
        public void PenaltyCurve_IsContinuousAcrossBreakpoints()
        {
            var carryState = new CarryState(1f);
            var weights = ZoneAxisWeights.Uniform;
            AppliedPenalty? previous = null;
            const float allowedAdjacentDelta = 0.08f;

            for (var step = 0; step <= 1200; step++)
            {
                var load = step / 1000f;
                carryState.Clear();
                AddNoiseAxisLoad(carryState, $"sample-{step}", load);

                var current = WeightPenaltyCalculator.Compute(carryState, weights);
                if (previous.HasValue)
                {
                    Assert.That(System.Math.Abs(current.NoiseMultiplier - previous.Value.NoiseMultiplier), Is.LessThanOrEqualTo(allowedAdjacentDelta));
                    Assert.That(System.Math.Abs(current.SilhouetteMultiplier - previous.Value.SilhouetteMultiplier), Is.LessThanOrEqualTo(allowedAdjacentDelta));
                    Assert.That(System.Math.Abs(current.HandlingMultiplier - previous.Value.HandlingMultiplier), Is.LessThanOrEqualTo(allowedAdjacentDelta));
                    Assert.That(System.Math.Abs(current.MobilityMultiplier - previous.Value.MobilityMultiplier), Is.LessThanOrEqualTo(allowedAdjacentDelta));
                }

                previous = current;
            }
        }

        [Test]
        public void ZoneAxisWeights_AmplifiesAsSpecified()
        {
            var carryState = new CarryState(1f);
            carryState.TryAdd(new TestLoadoutItem("heavy-noise", new CostSignature(0.9f, 0f, 0f, 0f)));

            var basePenalty = WeightPenaltyCalculator.Compute(carryState, new ZoneAxisWeights(0f, 1f / 3f, 1f / 3f, 1f / 3f));
            var amplifiedPenalty = WeightPenaltyCalculator.Compute(carryState, new ZoneAxisWeights(0.5f, 0.25f, 0.25f, 0f));

            var baseDelta = basePenalty.NoiseMultiplier - 1f;
            var amplifiedDelta = amplifiedPenalty.NoiseMultiplier - 1f;

            Assert.That(amplifiedDelta, Is.EqualTo(baseDelta * 1.5f).Within(0.02f));
        }

        [Test]
        public void TryAdd_FailsPastSoftCeiling()
        {
            var carryState = new CarryState(1f);
            var firstItem = new TestLoadoutItem("first", new CostSignature(0.9f, 0f, 0f, 0f));
            var secondItem = new TestLoadoutItem("second", new CostSignature(0.4f, 0f, 0f, 0f));

            Assert.That(carryState.TryAdd(firstItem), Is.True);
            Assert.That(carryState.TryAdd(secondItem), Is.False);
            Assert.That(carryState.Items.Count, Is.EqualTo(1));
        }

        [Test]
        public void Remove_DecreasesTotalCost()
        {
            var carryState = new CarryState(3f);
            var itemA = new TestLoadoutItem("a", new CostSignature(0.5f, 0.5f, 0f, 0f));
            var itemB = new TestLoadoutItem("b", new CostSignature(0.1f, 0.2f, 0f, 0f));
            carryState.TryAdd(itemA);
            carryState.TryAdd(itemB);

            carryState.Remove(itemA);

            Assert.That(carryState.TotalCost.Noise, Is.EqualTo(0.1f).Within(FloatTolerance));
            Assert.That(carryState.TotalCost.Silhouette, Is.EqualTo(0.2f).Within(FloatTolerance));
        }

        [Test]
        public void OnCarryChanged_FiresExactlyOncePerAddRemove()
        {
            var carryState = new CarryState(1f);
            var item = new TestLoadoutItem("id", new CostSignature(0.1f, 0.1f, 0.1f, 0.1f));
            var callCount = 0;
            carryState.OnCarryChanged += () => callCount++;

            carryState.TryAdd(item);
            carryState.Remove(item);

            Assert.That(callCount, Is.EqualTo(2));
        }

        private sealed class TestLoadoutItem : ILoadoutItem
        {
            public TestLoadoutItem(string itemId, CostSignature baseCost, float value = 1f, bool isVolatile = false)
            {
                ItemId = itemId;
                BaseCost = baseCost;
                Value = value;
                IsVolatile = isVolatile;
            }

            public string ItemId { get; }
            public CostSignature BaseCost { get; }
            public float Value { get; }
            public bool IsVolatile { get; }
        }

        private static void AddNoiseAxisLoad(CarryState carryState, string id, float load)
        {
            var first = load > 1f ? 1f : load;
            var second = load - first;

            if (first > 0f)
            {
                carryState.TryAdd(new TestLoadoutItem($"{id}-a", new CostSignature(first, 0f, 0f, 0f)));
            }

            if (second > 0f)
            {
                carryState.TryAdd(new TestLoadoutItem($"{id}-b", new CostSignature(second, 0f, 0f, 0f)));
            }
        }
    }
}
