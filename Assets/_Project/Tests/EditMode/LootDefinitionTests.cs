#nullable enable
using System.Linq;
using ExtractionWeight.Core;
using ExtractionWeight.Loot;
using ExtractionWeight.Loot.Editor;
using ExtractionWeight.Weight;
using NUnit.Framework;
using UnityEngine;

namespace ExtractionWeight.Tests.EditMode
{
    public class LootDefinitionTests
    {
        private const float FloatTolerance = 0.0001f;
        private LootDatabase _database = null!;

        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            LootSeedDataUtility.CreatePhaseOneLootSeedData();
        }

        [SetUp]
        public void SetUp()
        {
            _database = UnityEditor.AssetDatabase.LoadAssetAtPath<LootDatabase>(LootDatabase.AssetPath);
            Assert.That(_database, Is.Not.Null, "Expected seeded LootDatabase asset to exist.");
        }

        [Test]
        public void LootDatabaseDetectsDuplicateIds()
        {
            var texture = new Texture2D(4, 4, TextureFormat.RGBA32, false);
            var icon = Sprite.Create(texture, new Rect(0f, 0f, 4f, 4f), new Vector2(0.5f, 0.5f));

            try
            {
                var duplicateA = ScriptableObject.CreateInstance<LootDefinition>();
                duplicateA.EditorSetData("duplicate-id", "Duplicate A", icon, LootCategory.Currency, new CostSignature(0.1f, 0f, 0f, 0f), 10f, false, Vector3.one, null, null, default);

                var duplicateB = ScriptableObject.CreateInstance<LootDefinition>();
                duplicateB.EditorSetData("duplicate-id", "Duplicate B", icon, LootCategory.Relic, new CostSignature(0f, 0.1f, 0f, 0f), 20f, false, Vector3.one, null, null, default);

                var database = ScriptableObject.CreateInstance<LootDatabase>();
                database.EditorSetDefinitions(new[] { duplicateA, duplicateB });

                var errors = database.Validate();

                Assert.That(errors.Any(error => error.Contains("duplicate ItemId 'duplicate-id'")), Is.True);
            }
            finally
            {
                Object.DestroyImmediate(icon);
                Object.DestroyImmediate(texture);
            }
        }

        [TestCase("currency-small-bag")]
        [TestCase("currency-cash-roll")]
        [TestCase("currency-data-chip")]
        [TestCase("currency-coin-stack")]
        [TestCase("relic-small-sculpture")]
        [TestCase("relic-medium-painting")]
        [TestCase("relic-large-machine")]
        [TestCase("relic-old-console")]
        [TestCase("relic-preserved-specimen")]
        [TestCase("volatile-leaking-battery")]
        [TestCase("volatile-caged-bird")]
        [TestCase("volatile-cracked-vial")]
        public void GetByIdReturnsCorrectDefinitionForEachSeededItem(string itemId)
        {
            var definition = _database.GetById(itemId);

            Assert.That(definition.ItemId, Is.EqualTo(itemId));
        }

        [Test]
        public void GetByCategoryFiltersCorrectly()
        {
            var currencies = _database.GetByCategory(LootCategory.Currency);
            var relics = _database.GetByCategory(LootCategory.Relic);
            var volatiles = _database.GetByCategory(LootCategory.Volatile);

            Assert.That(currencies.Count, Is.EqualTo(4));
            Assert.That(relics.Count, Is.EqualTo(5));
            Assert.That(volatiles.Count, Is.EqualTo(3));
            Assert.That(currencies.All(definition => definition.Category == LootCategory.Currency), Is.True);
            Assert.That(relics.All(definition => definition.Category == LootCategory.Relic), Is.True);
            Assert.That(volatiles.All(definition => definition.Category == LootCategory.Volatile), Is.True);
        }

        [Test]
        public void LootItemCorrectlyExposesILoadoutItemInterface()
        {
            var definition = _database.GetById("volatile-caged-bird");
            ILoadoutItem item = new LootItem(definition);

            Assert.That(item.ItemId, Is.EqualTo(definition.ItemId));
            Assert.That(item.BaseCost, Is.EqualTo(definition.BaseCost));
            Assert.That(item.Value, Is.EqualTo(definition.Value));
            Assert.That(item.IsVolatile, Is.EqualTo(definition.IsVolatile));
        }

        [Test]
        public void SeededLootDatabaseValidationPasses()
        {
            var errors = _database.Validate();

            Assert.That(errors, Is.Empty);
        }

        [Test]
        public void AddingFiveAuthoredLootItemsProducesExpectedCapacityFraction()
        {
            var carryState = new CarryState(2f);
            var selectedItems = new[]
            {
                "currency-small-bag",
                "currency-data-chip",
                "relic-small-sculpture",
                "relic-old-console",
                "volatile-caged-bird",
            };

            var expectedMagnitude = 0f;
            for (var i = 0; i < selectedItems.Length; i++)
            {
                var definition = _database.GetById(selectedItems[i]);
                expectedMagnitude += definition.BaseCost.Noise * definition.BaseCost.Noise;
                expectedMagnitude += definition.BaseCost.Silhouette * definition.BaseCost.Silhouette;
                expectedMagnitude += definition.BaseCost.Handling * definition.BaseCost.Handling;
                expectedMagnitude += definition.BaseCost.Mobility * definition.BaseCost.Mobility;

                Assert.That(carryState.TryAdd(new LootItem(definition)), Is.True);
            }

            var totalNoise = carryState.TotalCost.Noise;
            var totalSilhouette = carryState.TotalCost.Silhouette;
            var totalHandling = carryState.TotalCost.Handling;
            var totalMobility = carryState.TotalCost.Mobility;
            var expectedCapacityFraction = Mathf.Sqrt(
                (totalNoise * totalNoise) +
                (totalSilhouette * totalSilhouette) +
                (totalHandling * totalHandling) +
                (totalMobility * totalMobility)) / carryState.CarryCapacity;

            Assert.That(expectedMagnitude, Is.GreaterThan(0f));
            Assert.That(carryState.CapacityFraction, Is.EqualTo(expectedCapacityFraction).Within(FloatTolerance));
            Assert.That(carryState.CapacityFraction, Is.EqualTo(0.6637959f).Within(FloatTolerance));
        }
    }
}
