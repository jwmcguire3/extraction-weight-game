#nullable enable
using System.Collections;
using ExtractionWeight.Core;
using ExtractionWeight.Weight;
using NUnit.Framework;
using UnityEngine.Profiling;
using UnityEngine.TestTools;

namespace ExtractionWeight.Tests.PlayMode
{
    public class WeightAllocationTests
    {
        [UnityTest]
        public IEnumerator Compute_DoesNotAllocateGcMemory()
        {
            var carryState = new CarryState(1f);
            carryState.TryAdd(new TestLoadoutItem("item", new CostSignature(0.5f, 0.25f, 0.25f, 0.1f)));
            var weights = ZoneAxisWeights.Uniform;

            _ = WeightPenaltyCalculator.Compute(carryState, weights);

            var recorder = Recorder.Get("GC.Alloc");
            recorder.enabled = true;

            for (var i = 0; i < 10000; i++)
            {
                _ = WeightPenaltyCalculator.Compute(carryState, weights);
            }

            recorder.enabled = false;

            Assert.That(recorder.sampleBlockCount, Is.EqualTo(0), "Expected no GC allocations while computing penalties.");
            yield return null;
        }

        private sealed class TestLoadoutItem : ILoadoutItem
        {
            public TestLoadoutItem(string itemId, CostSignature baseCost)
            {
                ItemId = itemId;
                BaseCost = baseCost;
            }

            public string ItemId { get; }
            public CostSignature BaseCost { get; }
            public float Value => 1f;
            public bool IsVolatile => false;
        }
    }
}
