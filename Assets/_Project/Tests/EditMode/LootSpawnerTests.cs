#nullable enable
using System.Collections.Generic;
using ExtractionWeight.Core;
using ExtractionWeight.Loot;
using ExtractionWeight.Zone;
using NUnit.Framework;
using UnityEngine;

namespace ExtractionWeight.Tests.EditMode
{
    public class LootSpawnerTests
    {
        private Sprite _icon = null!;
        private List<LootDefinition> _definitions = null!;

        [SetUp]
        public void SetUp()
        {
            var texture = new Texture2D(4, 4, TextureFormat.RGBA32, false);
            var pixels = new Color[16];
            for (var i = 0; i < pixels.Length; i++)
            {
                pixels[i] = Color.white;
            }

            texture.SetPixels(pixels);
            texture.Apply();
            _icon = Sprite.Create(texture, new Rect(0f, 0f, 4f, 4f), new Vector2(0.5f, 0.5f));

            _definitions = new List<LootDefinition>
            {
                CreateDefinition("currency-small", LootCategory.Currency, new Vector3(0.1f, 0.1f, 0.1f)),
                CreateDefinition("currency-stack", LootCategory.Currency, new Vector3(0.2f, 0.2f, 0.1f)),
                CreateDefinition("relic-small", LootCategory.Relic, new Vector3(0.3f, 0.2f, 0.2f)),
                CreateDefinition("relic-medium", LootCategory.Relic, new Vector3(0.6f, 0.4f, 0.2f)),
                CreateDefinition("relic-large", LootCategory.Relic, new Vector3(0.9f, 0.7f, 0.6f)),
                CreateDefinition("volatile-battery", LootCategory.Volatile, new Vector3(0.2f, 0.2f, 0.1f), true, new AmbientAxisEffect(CostAxis.Noise, 0.05f)),
                CreateDefinition("volatile-bird", LootCategory.Volatile, new Vector3(0.4f, 0.4f, 0.4f), true, new AmbientAxisEffect(CostAxis.Noise, 0.03f)),
            };
        }

        [TearDown]
        public void TearDown()
        {
            if (_definitions != null)
            {
                for (var i = 0; i < _definitions.Count; i++)
                {
                    Object.DestroyImmediate(_definitions[i]);
                }
            }

            if (_icon != null)
            {
                Object.DestroyImmediate(_icon.texture);
                Object.DestroyImmediate(_icon);
            }
        }

        [Test]
        public void FixedSeed_ProducesDeterministicOutput()
        {
            var regions = new[]
            {
                new LootSpawnRegion(Vector3.zero, 8f, new[] { LootCategory.Currency, LootCategory.Relic, LootCategory.Volatile }, 3, 3, 0.55f),
            };

            var first = LootSpawner.BuildSpawnPlan(regions, _definitions, 1234);
            var second = LootSpawner.BuildSpawnPlan(regions, _definitions, 1234);

            Assert.That(first.Count, Is.EqualTo(second.Count));
            for (var i = 0; i < first.Count; i++)
            {
                Assert.That(first[i].Definition.ItemId, Is.EqualTo(second[i].Definition.ItemId));
                Assert.That(first[i].Position, Is.EqualTo(second[i].Position));
                Assert.That(first[i].RegionIndex, Is.EqualTo(second[i].RegionIndex));
            }
        }

        [TestCase(0.0f, 0.70f, 0.25f, 0.05f)]
        [TestCase(0.5f, 0.40f, 0.50f, 0.10f)]
        [TestCase(1.0f, 0.15f, 0.60f, 0.25f)]
        public void CategoryDistribution_PerTierStaysWithinExpectedBounds(float tier, float expectedCurrency, float expectedRelic, float expectedVolatile)
        {
            var region = new LootSpawnRegion(Vector3.zero, 10f, new[] { LootCategory.Currency, LootCategory.Relic, LootCategory.Volatile }, 1, 1, tier);
            var categoryCounts = new Dictionary<LootCategory, int>();

            for (var seed = 0; seed < 1000; seed++)
            {
                var plan = LootSpawner.BuildSpawnPlan(new[] { region }, _definitions, seed);
                var category = plan[0].Definition.Category;
                categoryCounts.TryGetValue(category, out var currentCount);
                categoryCounts[category] = currentCount + 1;
            }

            AssertCategoryRate(categoryCounts, LootCategory.Currency, expectedCurrency);
            AssertCategoryRate(categoryCounts, LootCategory.Relic, expectedRelic);
            AssertCategoryRate(categoryCounts, LootCategory.Volatile, expectedVolatile);
        }

        [Test]
        public void SpawnPositions_NeverLeaveRegionRadius()
        {
            var region = new LootSpawnRegion(new Vector3(12f, 0f, -8f), 7f, new[] { LootCategory.Currency, LootCategory.Relic, LootCategory.Volatile }, 10, 10, 0.45f);
            var plan = LootSpawner.BuildSpawnPlan(new[] { region }, _definitions, 42);

            for (var i = 0; i < plan.Count; i++)
            {
                var planarOffset = plan[i].Position - region.Center;
                planarOffset.y = 0f;
                Assert.That(planarOffset.magnitude, Is.LessThanOrEqualTo(region.Radius + 0.0001f));
            }
        }

        private LootDefinition CreateDefinition(
            string itemId,
            LootCategory category,
            Vector3 size,
            bool isVolatile = false,
            AmbientAxisEffect ambientEffect = default)
        {
            var definition = ScriptableObject.CreateInstance<LootDefinition>();
            definition.EditorSetData(
                itemId,
                itemId,
                _icon,
                category,
                new CostSignature(0.1f, 0.1f, 0.1f, 0.1f),
                10f,
                isVolatile,
                size,
                null,
                null,
                ambientEffect);
            return definition;
        }

        private static void AssertCategoryRate(IReadOnlyDictionary<LootCategory, int> counts, LootCategory category, float expectedRate)
        {
            counts.TryGetValue(category, out var count);
            var observedRate = count / 1000f;
            Assert.That(observedRate, Is.EqualTo(expectedRate).Within(0.06f), $"{category} observed rate was {observedRate:0.000}.");
        }
    }
}
