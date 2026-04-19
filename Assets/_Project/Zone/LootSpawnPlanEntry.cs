#nullable enable
using ExtractionWeight.Loot;
using UnityEngine;

namespace ExtractionWeight.Zone
{
    public readonly struct LootSpawnPlanEntry
    {
        public LootSpawnPlanEntry(LootDefinition definition, Vector3 position, int regionIndex)
        {
            Definition = definition;
            Position = position;
            RegionIndex = regionIndex;
        }

        public LootDefinition Definition { get; }

        public Vector3 Position { get; }

        public int RegionIndex { get; }
    }
}
