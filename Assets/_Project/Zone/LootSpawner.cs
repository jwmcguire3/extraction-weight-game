#nullable enable
using System;
using System.Collections.Generic;
using ExtractionWeight.Loot;
using UnityEngine;

namespace ExtractionWeight.Zone
{
    [DisallowMultipleComponent]
    public sealed class LootSpawner : MonoBehaviour
    {
        [SerializeField]
        private LootDatabase? _lootDatabase;

        [SerializeField]
        private Transform? _spawnRoot;

        [SerializeField]
        private int _fallbackSeed = 1337;

        private readonly List<GameObject> _spawnedLoot = new();
        private bool _hasSpawned;

        public IReadOnlyList<GameObject> SpawnedLoot => _spawnedLoot;

        private void Start()
        {
            if (_hasSpawned)
            {
                return;
            }

            var loader = FindAnyObjectByType<ZoneLoader>();
            var zoneDefinition = loader?.CurrentZoneDefinition;
            var seed = loader?.CurrentRunSeed ?? _fallbackSeed;
            if (zoneDefinition == null)
            {
                return;
            }

            SpawnForZone(zoneDefinition, seed);
        }

        public void SpawnForZone(ZoneDefinition zoneDefinition, int seed)
        {
            _lootDatabase ??= LootDatabase.Instance;
#if UNITY_EDITOR
            _lootDatabase ??= UnityEditor.AssetDatabase.LoadAssetAtPath<LootDatabase>(LootDatabase.AssetPath);
#endif
            if (_lootDatabase == null)
            {
                throw new InvalidOperationException("LootDatabase is required to spawn loot.");
            }

            ClearSpawnedLoot();

            var plan = BuildSpawnPlan(zoneDefinition.LootSpawnRegions, _lootDatabase.Definitions, seed);
            _spawnRoot ??= transform;

            for (var i = 0; i < plan.Count; i++)
            {
                var entry = plan[i];
                var pickupObject = new GameObject($"Loot_{entry.Definition.ItemId}_{i}");
                pickupObject.transform.SetParent(_spawnRoot, false);
                pickupObject.transform.position = entry.Position + Vector3.up * 0.1f;

                var collider = pickupObject.AddComponent<SphereCollider>();
                collider.radius = 0.8f;
                collider.isTrigger = true;

                var pickup = pickupObject.AddComponent<LootPickup>();
                pickup.Configure(entry.Definition);

                _spawnedLoot.Add(pickupObject);
            }

            _hasSpawned = true;
        }

        public static IReadOnlyList<LootSpawnPlanEntry> BuildSpawnPlan(
            IReadOnlyList<LootSpawnRegion> regions,
            IReadOnlyList<LootDefinition> definitions,
            int seed)
        {
            var plan = new List<LootSpawnPlanEntry>();
            var random = new System.Random(seed);

            for (var regionIndex = 0; regionIndex < regions.Count; regionIndex++)
            {
                var region = regions[regionIndex];
                var minCount = Math.Min(region.MinSpawnCount, region.MaxSpawnCount);
                var maxCount = Math.Max(region.MinSpawnCount, region.MaxSpawnCount);
                var spawnCount = random.Next(minCount, maxCount + 1);

                for (var spawnIndex = 0; spawnIndex < spawnCount; spawnIndex++)
                {
                    var definition = ChooseDefinition(definitions, region.RegionTier, random);
                    var position = region.Center + SamplePointInCircle(region.Radius, random);
                    plan.Add(new LootSpawnPlanEntry(definition, position, regionIndex));
                }
            }

            return plan;
        }

        public static LootCategory SampleCategory(float regionTier, System.Random random)
        {
            var weights = ResolveCategoryWeights(regionTier);
            var roll = (float)random.NextDouble();
            if (roll <= weights.Currency)
            {
                return LootCategory.Currency;
            }

            if (roll <= weights.Currency + weights.Relic)
            {
                return LootCategory.Relic;
            }

            return LootCategory.Volatile;
        }

        private static LootDefinition ChooseDefinition(
            IReadOnlyList<LootDefinition> definitions,
            float regionTier,
            System.Random random)
        {
            var category = SampleCategory(regionTier, random);
            var sizePreference = ResolveRelicSizePreference(regionTier);
            var matches = new List<LootDefinition>();

            for (var i = 0; i < definitions.Count; i++)
            {
                var definition = definitions[i];
                if (definition == null || definition.Category != category)
                {
                    continue;
                }

                if (category == LootCategory.Relic &&
                    sizePreference != null &&
                    definition.GetSizeClass() != sizePreference.Value)
                {
                    continue;
                }

                matches.Add(definition);
            }

            if (matches.Count == 0)
            {
                for (var i = 0; i < definitions.Count; i++)
                {
                    var definition = definitions[i];
                    if (definition != null && definition.Category == category)
                    {
                        matches.Add(definition);
                    }
                }
            }

            if (matches.Count == 0)
            {
                throw new InvalidOperationException($"No loot definitions are available for category '{category}'.");
            }

            return matches[random.Next(matches.Count)];
        }

        private static CategoryWeights ResolveCategoryWeights(float tier)
        {
            var clampedTier = Mathf.Clamp01(tier);
            if (clampedTier <= 0.5f)
            {
                var t = clampedTier / 0.5f;
                return CategoryWeights.Lerp(
                    new CategoryWeights(0.70f, 0.25f, 0.05f),
                    new CategoryWeights(0.40f, 0.50f, 0.10f),
                    t);
            }

            return CategoryWeights.Lerp(
                new CategoryWeights(0.40f, 0.50f, 0.10f),
                new CategoryWeights(0.15f, 0.60f, 0.25f),
                (clampedTier - 0.5f) / 0.5f);
        }

        private static LootItemSize? ResolveRelicSizePreference(float tier)
        {
            var clampedTier = Mathf.Clamp01(tier);
            if (clampedTier <= 0.25f)
            {
                return LootItemSize.Small;
            }

            if (clampedTier >= 0.75f)
            {
                return LootItemSize.Large;
            }

            return null;
        }

        private static Vector3 SamplePointInCircle(float radius, System.Random random)
        {
            var angle = (float)(random.NextDouble() * Mathf.PI * 2f);
            var distance = Mathf.Sqrt((float)random.NextDouble()) * Mathf.Max(0f, radius);
            return new Vector3(Mathf.Cos(angle) * distance, 0f, Mathf.Sin(angle) * distance);
        }

        private void ClearSpawnedLoot()
        {
            for (var i = 0; i < _spawnedLoot.Count; i++)
            {
                var spawned = _spawnedLoot[i];
                if (spawned != null)
                {
                    Destroy(spawned);
                }
            }

            _spawnedLoot.Clear();
        }

        private readonly struct CategoryWeights
        {
            public CategoryWeights(float currency, float relic, float volatileChance)
            {
                Currency = currency;
                Relic = relic;
                Volatile = volatileChance;
            }

            public float Currency { get; }

            public float Relic { get; }

            public float Volatile { get; }

            public static CategoryWeights Lerp(CategoryWeights from, CategoryWeights to, float t)
            {
                return new CategoryWeights(
                    Mathf.Lerp(from.Currency, to.Currency, t),
                    Mathf.Lerp(from.Relic, to.Relic, t),
                    Mathf.Lerp(from.Volatile, to.Volatile, t));
            }
        }
    }
}
