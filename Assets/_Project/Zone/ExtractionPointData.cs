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

        public ExtractionPointData(
            string pointId,
            ExtractionType extractionType,
            Vector3 position,
            float tideCloseTime,
            ItemSizeFilter itemSizeFilter,
            float holdDurationSeconds)
        {
            PointId = pointId;
            ExtractionType = extractionType;
            Position = position;
            TideCloseTime = tideCloseTime;
            ItemSizeFilter = itemSizeFilter;
            HoldDurationSeconds = holdDurationSeconds;
        }

        public ExtractionPointData()
        {
        }
    }
}
