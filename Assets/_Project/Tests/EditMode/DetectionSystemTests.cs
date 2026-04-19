#nullable enable
using ExtractionWeight.Core;
using ExtractionWeight.Threat;
using ExtractionWeight.Weight;
using NUnit.Framework;
using UnityEngine;

namespace ExtractionWeight.Tests.EditMode
{
    public class DetectionSystemTests
    {
        private static readonly DetectionProfile WardenProfile = new(0.1f, 0.9f, 25f, 25f, 38f);
        private static readonly DetectionProfile ListenerProfile = new(0.95f, 0.05f, 15f, 15f, 22f);

        [Test]
        public void Overlap_AlwaysDetected()
        {
            var carryState = new CarryState(1f);
            var penalties = WeightPenaltyCalculator.Compute(carryState, ZoneAxisWeights.Uniform);

            var state = DetectionSystem.Evaluate(Vector3.zero, carryState, penalties, Vector3.zero, WardenProfile);

            Assert.That(state, Is.EqualTo(DetectionState.Detected));
        }

        [Test]
        public void BeyondBaseRangeTimesOnePointFive_IsAlwaysUnaware()
        {
            var carryState = new CarryState(1f);
            carryState.TryAdd(new SyntheticLoadoutItem("loud", new CostSignature(1f, 1f, 0f, 0f)));
            var penalties = WeightPenaltyCalculator.Compute(carryState, ZoneAxisWeights.Uniform);
            var playerPosition = new Vector3(0f, 0f, 23f);

            var listenerState = DetectionSystem.Evaluate(playerPosition, carryState, penalties, Vector3.zero, ListenerProfile);
            var wardenState = DetectionSystem.Evaluate(new Vector3(0f, 0f, 38f), carryState, penalties, Vector3.zero, WardenProfile);

            Assert.That(listenerState, Is.EqualTo(DetectionState.Unaware));
            Assert.That(wardenState, Is.EqualTo(DetectionState.Unaware));
        }

        [Test]
        public void NoiseHeavyLoot_ShiftsListenerDetectionDistanceProportionally()
        {
            var quietState = new CarryState(1f);
            var loudState = new CarryState(1f);
            loudState.TryAdd(new SyntheticLoadoutItem("noise", new CostSignature(0.2f, 0f, 0f, 0f)));

            var quietPenalty = WeightPenaltyCalculator.Compute(quietState, ZoneAxisWeights.Uniform);
            var loudPenalty = WeightPenaltyCalculator.Compute(loudState, ZoneAxisWeights.Uniform);

            var quietRange = DetectionSystem.CalculateEffectiveDetectionRange(quietPenalty, ListenerProfile);
            var loudRange = DetectionSystem.CalculateEffectiveDetectionRange(loudPenalty, ListenerProfile);
            var expectedRatio = loudPenalty.NoiseMultiplier / quietPenalty.NoiseMultiplier;
            var actualRatio = loudRange / quietRange;

            Assert.That(actualRatio, Is.EqualTo(expectedRatio).Within(0.03f));
        }

        [Test]
        public void SilhouetteHeavyLoot_ShiftsWardenDetectionDistanceProportionally()
        {
            var lightState = new CarryState(1f);
            var silhouetteState = new CarryState(1f);
            silhouetteState.TryAdd(new SyntheticLoadoutItem("silhouette", new CostSignature(0f, 0.2f, 0f, 0f)));

            var lightPenalty = WeightPenaltyCalculator.Compute(lightState, ZoneAxisWeights.Uniform);
            var silhouettePenalty = WeightPenaltyCalculator.Compute(silhouetteState, ZoneAxisWeights.Uniform);

            var lightRange = DetectionSystem.CalculateEffectiveDetectionRange(lightPenalty, WardenProfile);
            var silhouetteRange = DetectionSystem.CalculateEffectiveDetectionRange(silhouettePenalty, WardenProfile);
            var expectedRange = WardenProfile.BaseDetectionRange * (
                (WardenProfile.NoiseWeight * silhouettePenalty.NoiseMultiplier) +
                (WardenProfile.SilhouetteWeight * silhouettePenalty.SilhouetteMultiplier));

            Assert.That(silhouetteRange, Is.EqualTo(expectedRange).Within(0.01f));
            Assert.That(silhouetteRange, Is.GreaterThan(lightRange));
        }

        [Test]
        public void NoiseHeavyLoot_DoesNotSignificantlyShiftWardenDetection()
        {
            var quietState = new CarryState(1f);
            var noiseState = new CarryState(1f);
            var silhouetteState = new CarryState(1f);
            noiseState.TryAdd(new SyntheticLoadoutItem("noise", new CostSignature(0.4f, 0f, 0f, 0f)));
            silhouetteState.TryAdd(new SyntheticLoadoutItem("silhouette", new CostSignature(0f, 0.4f, 0f, 0f)));

            var quietPenalty = WeightPenaltyCalculator.Compute(quietState, ZoneAxisWeights.Uniform);
            var noisePenalty = WeightPenaltyCalculator.Compute(noiseState, ZoneAxisWeights.Uniform);
            var silhouettePenalty = WeightPenaltyCalculator.Compute(silhouetteState, ZoneAxisWeights.Uniform);

            var quietRange = DetectionSystem.CalculateEffectiveDetectionRange(quietPenalty, WardenProfile);
            var noiseRange = DetectionSystem.CalculateEffectiveDetectionRange(noisePenalty, WardenProfile);
            var silhouetteRange = DetectionSystem.CalculateEffectiveDetectionRange(silhouettePenalty, WardenProfile);
            var noiseShift = noiseRange - quietRange;
            var silhouetteShift = silhouetteRange - quietRange;

            Assert.That(noiseShift, Is.GreaterThan(0f));
            Assert.That(noiseShift, Is.LessThan(silhouetteShift * 0.25f));
        }

        [Test]
        public void LineOfSightBlocking_PreventsWardenDetection()
        {
            var carryState = new CarryState(1f);
            carryState.TryAdd(new SyntheticLoadoutItem("silhouette", new CostSignature(0f, 1f, 0f, 0f)));
            var penalties = WeightPenaltyCalculator.Compute(carryState, ZoneAxisWeights.Uniform);

            var player = new GameObject("Player");
            var playerCollider = player.AddComponent<CapsuleCollider>();
            player.transform.position = new Vector3(0f, 0f, 12f);

            var blocker = GameObject.CreatePrimitive(PrimitiveType.Cube);
            blocker.transform.position = new Vector3(0f, 1f, 6f);
            blocker.transform.localScale = new Vector3(4f, 4f, 1f);
            Physics.SyncTransforms();

            try
            {
                var state = DetectionSystem.Evaluate(
                    player.transform.position,
                    carryState,
                    penalties,
                    Vector3.zero,
                    WardenProfile,
                    playerCollider);

                Assert.That(state, Is.EqualTo(DetectionState.Unaware));
            }
            finally
            {
                Object.DestroyImmediate(blocker);
                Object.DestroyImmediate(player);
            }
        }

        private sealed class SyntheticLoadoutItem : ILoadoutItem
        {
            public SyntheticLoadoutItem(string itemId, CostSignature baseCost)
            {
                ItemId = itemId;
                BaseCost = baseCost;
            }

            public string ItemId { get; }

            public CostSignature BaseCost { get; }

            public float Value => 0f;

            public bool IsVolatile => false;
        }
    }
}
