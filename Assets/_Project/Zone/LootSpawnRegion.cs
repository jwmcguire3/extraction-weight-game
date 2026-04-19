#nullable enable
using System;
using ExtractionWeight.Loot;
using UnityEngine;

namespace ExtractionWeight.Zone
{
    [Serializable]
    public sealed class LootSpawnRegion
    {
        [field: SerializeField] public Vector3 Center { get; private set; }
        [field: SerializeField] public float Radius { get; private set; }
        [field: SerializeField] public LootCategory[] AllowedCategories { get; private set; } = Array.Empty<LootCategory>();
        [field: SerializeField] public int MinSpawnCount { get; private set; }
        [field: SerializeField] public int MaxSpawnCount { get; private set; }
        [field: SerializeField] public float RegionTier { get; private set; }

        public LootSpawnRegion(
            Vector3 center,
            float radius,
            LootCategory[] allowedCategories,
            int minSpawnCount,
            int maxSpawnCount,
            float regionTier)
        {
            Center = center;
            Radius = radius;
            AllowedCategories = allowedCategories ?? Array.Empty<LootCategory>();
            MinSpawnCount = minSpawnCount;
            MaxSpawnCount = maxSpawnCount;
            RegionTier = regionTier;
        }

        public LootSpawnRegion()
        {
        }
    }
}
