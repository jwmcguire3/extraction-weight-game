#nullable enable
using System.Collections.Generic;
using ExtractionWeight.Weight;
using UnityEngine;

namespace ExtractionWeight.Zone
{
    [CreateAssetMenu(fileName = "ZoneDefinition", menuName = "Extraction Weight/Zone Definition")]
    public sealed class ZoneDefinition : ScriptableObject
    {
        [field: SerializeField] public string ZoneId { get; private set; } = string.Empty;
        [field: SerializeField] public string DisplayName { get; private set; } = string.Empty;
        [field: SerializeField] public string SceneAddressableKey { get; private set; } = string.Empty;
        [field: SerializeField] public ZoneAxisWeights DefaultAxisWeights { get; private set; } = ZoneAxisWeights.Uniform;
        [field: SerializeField] public float TideDurationSeconds { get; private set; } = 600f;
        [field: SerializeField] public List<ExtractionPointData> ExtractionPoints { get; private set; } = new();
        [field: SerializeField] public List<LootSpawnRegion> LootSpawnRegions { get; private set; } = new();
        [field: SerializeField] public List<ThreatPatrolRoute> PatrolRoutes { get; private set; } = new();
        [field: SerializeField] public string EditorScenePath { get; private set; } = string.Empty;

        private void OnValidate()
        {
            if (!DefaultAxisWeights.IsApproximatelyNormalized())
            {
                DefaultAxisWeights = DefaultAxisWeights.Normalized();
            }

            TideDurationSeconds = Mathf.Max(0f, TideDurationSeconds);
        }

#if UNITY_EDITOR
        public void EditorSetData(
            string zoneId,
            string displayName,
            string sceneAddressableKey,
            ZoneAxisWeights defaultAxisWeights,
            float tideDurationSeconds,
            List<ExtractionPointData> extractionPoints,
            List<LootSpawnRegion> lootSpawnRegions,
            List<ThreatPatrolRoute> patrolRoutes,
            string editorScenePath)
        {
            ZoneId = zoneId;
            DisplayName = displayName;
            SceneAddressableKey = sceneAddressableKey;
            DefaultAxisWeights = defaultAxisWeights;
            TideDurationSeconds = tideDurationSeconds;
            ExtractionPoints = extractionPoints;
            LootSpawnRegions = lootSpawnRegions;
            PatrolRoutes = patrolRoutes;
            EditorScenePath = editorScenePath;
        }
#endif
    }
}
