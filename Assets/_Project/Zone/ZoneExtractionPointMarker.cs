#nullable enable
using UnityEngine;

namespace ExtractionWeight.Zone
{
    public sealed class ZoneExtractionPointMarker : MonoBehaviour
    {
        [SerializeField]
        private Color _openColor = new(0.9f, 0.22f, 0.16f, 1f);

        [SerializeField]
        private Color _closedColor = new(0.28f, 0.28f, 0.32f, 0.9f);

        private MeshRenderer? _meshRenderer;
        private ZoneRuntime? _zoneRuntime;

        [field: SerializeField] public string PointId { get; private set; } = string.Empty;
        [field: SerializeField] public ExtractionType ExtractionType { get; private set; }

        private void Awake()
        {
            _meshRenderer = GetComponent<MeshRenderer>();
        }

        private void Update()
        {
            _zoneRuntime ??= FindAnyObjectByType<ZoneRuntime>();
            if (_zoneRuntime == null || _meshRenderer == null || _meshRenderer.sharedMaterial == null)
            {
                return;
            }

            _meshRenderer.sharedMaterial.color = _zoneRuntime.IsExtractionOpen(PointId) ? _openColor : _closedColor;
        }

        public void Initialize(ExtractionPointData pointData)
        {
            PointId = pointData.PointId;
            ExtractionType = pointData.ExtractionType;
            gameObject.name = $"ExtractionPointMarker_{pointData.PointId}";
        }
    }
}
