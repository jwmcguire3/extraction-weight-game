#nullable enable
using ExtractionWeight.Core;
using ExtractionWeight.Extraction;
using ExtractionWeight.Loot;
using ExtractionWeight.Weight;
using ExtractionWeight.Zone;
using NUnit.Framework;
using UnityEngine;

namespace ExtractionWeight.Tests.EditMode
{
    public class ExtractionLogicTests
    {
        [Test]
        public void CarryCompatibilityCheck_RejectsOversizedItemsForSmallOnlyFilter()
        {
            var carryState = new CarryState(1f);
            carryState.TryAdd(CreateLootItem("large", Vector3.one));

            var result = ExtractionRules.IsCarryCompatible(carryState, ItemSizeFilter.SmallOnly);

            Assert.That(result, Is.False);
        }

        [Test]
        public void CarryCapacityCheck_RejectsOverweightForCapacityLimitedExits()
        {
            var carryState = new CarryState(1f);
            carryState.TryAdd(new SyntheticLoadoutItem("heavy", new CostSignature(0.5f, 0f, 0f, 0f)));

            var result = ExtractionRules.IsCarryWithinCapacity(carryState, 0.45f);

            Assert.That(result, Is.False);
        }

        [Test]
        public void TideClosesExtractionsAtCorrectTime()
        {
            var pointData = new ExtractionPointData("C", ExtractionType.Drone, Vector3.zero, 540f, ItemSizeFilter.SmallOnly, 20f);

            Assert.That(ZoneRuntime.IsOpenAtTime(pointData, 539.9f), Is.True);
            Assert.That(ZoneRuntime.IsOpenAtTime(pointData, 540f), Is.False);
        }

        [Test]
        public void StateMachineTransitionsOnlyFireInValidOrders()
        {
            var stateMachine = new ExtractionStateMachine();

            stateMachine.TransitionTo(ExtractionPhaseState.Initiation);
            stateMachine.TransitionTo(ExtractionPhaseState.Approach);
            stateMachine.TransitionTo(ExtractionPhaseState.Hold);
            stateMachine.TransitionTo(ExtractionPhaseState.Departure);
            stateMachine.TransitionTo(ExtractionPhaseState.Completed);

            Assert.That(stateMachine.State, Is.EqualTo(ExtractionPhaseState.Completed));
            Assert.That(() => new ExtractionStateMachine().TransitionTo(ExtractionPhaseState.Hold), Throws.TypeOf<System.InvalidOperationException>());
            Assert.That(ExtractionStateMachine.CanTransition(ExtractionPhaseState.Completed, ExtractionPhaseState.Idle), Is.False);
        }

        private static LootItem CreateLootItem(string itemId, Vector3 size)
        {
            var iconTexture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            iconTexture.SetPixels(new[] { Color.white, Color.white, Color.white, Color.white });
            iconTexture.Apply();
            var icon = Sprite.Create(iconTexture, new Rect(0f, 0f, 2f, 2f), new Vector2(0.5f, 0.5f));

            var definition = ScriptableObject.CreateInstance<LootDefinition>();
            definition.EditorSetData(
                itemId,
                itemId,
                icon,
                LootCategory.Relic,
                new CostSignature(0.1f, 0f, 0f, 0f),
                10f,
                false,
                size,
                null,
                null,
                default);
            return new LootItem(definition);
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
