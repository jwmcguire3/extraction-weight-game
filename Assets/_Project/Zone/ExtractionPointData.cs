#nullable enable
using System;
using UnityEngine;

namespace ExtractionWeight.Zone
{
    [Serializable]
    public sealed class ExtractionPointData
    {
        [field: SerializeField] public string PointId { get; private set; } = string.Empty;
        [field: SerializeField] public ExtractionType ExtractionType { get; private set; }
        [field: SerializeField] public Vector3 Position { get; private set; }
        [field: SerializeField] public float TideCloseTime { get; private set; }
        [field: SerializeField] public ItemSizeFilter ItemSizeFilter { get; private set; }
        [field: SerializeField] public float HoldDurationSeconds { get; private set; }
        [field: SerializeField] public float InteractionRadius { get; private set; } = 3f;
        [field: SerializeField] public float HoldRadius { get; private set; } = 8f;
        [field: SerializeField] public float InitiationDurationSeconds { get; private set; } = 5f;
        [field: SerializeField] public float DepartureDurationSeconds { get; private set; } = 3f;
        [field: SerializeField] public float ApproachDurationSeconds { get; private set; } = 0f;
        [field: SerializeField] public float MaxCarryCapacityFraction { get; private set; } = 1.2f;

        public ExtractionPointData(
            string pointId,
            ExtractionType extractionType,
            Vector3 position,
            float tideCloseTime,
            ItemSizeFilter itemSizeFilter,
            float holdDurationSeconds,
            float interactionRadius = 3f,
            float holdRadius = 8f,
            float initiationDurationSeconds = 5f,
            float departureDurationSeconds = 3f,
            float approachDurationSeconds = 0f,
            float maxCarryCapacityFraction = 1.2f)
        {
            PointId = pointId;
            ExtractionType = extractionType;
            Position = position;
            TideCloseTime = tideCloseTime;
            ItemSizeFilter = itemSizeFilter;
            HoldDurationSeconds = holdDurationSeconds;
            InteractionRadius = interactionRadius;
            HoldRadius = holdRadius;
            InitiationDurationSeconds = initiationDurationSeconds;
            DepartureDurationSeconds = departureDurationSeconds;
            ApproachDurationSeconds = approachDurationSeconds;
            MaxCarryCapacityFraction = maxCarryCapacityFraction;
        }

        public ExtractionPointData()
        {
        }
    }
}
