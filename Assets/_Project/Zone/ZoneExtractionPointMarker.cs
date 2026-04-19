#nullable enable
using UnityEngine;

namespace ExtractionWeight.Zone
{
    public sealed class ZoneExtractionPointMarker : MonoBehaviour
    {
        [field: SerializeField] public string PointId { get; private set; } = string.Empty;
        [field: SerializeField] public ExtractionType ExtractionType { get; private set; }

        public void Initialize(ExtractionPointData pointData)
        {
            PointId = pointData.PointId;
            ExtractionType = pointData.ExtractionType;
            gameObject.name = $"ExtractionPointMarker_{pointData.PointId}";
        }
    }
}
