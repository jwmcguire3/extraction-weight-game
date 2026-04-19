#nullable enable
using System;
using ExtractionWeight.Core;
using UnityEngine;

namespace ExtractionWeight.Loot
{
    [Serializable]
    public struct AmbientAxisEffect
    {
        [SerializeField]
        private CostAxis _affectedAxis;

        [Min(0f)]
        [SerializeField]
        private float _axisIncreasePerSecond;

        public AmbientAxisEffect(CostAxis affectedAxis, float axisIncreasePerSecond)
        {
            _affectedAxis = affectedAxis;
            _axisIncreasePerSecond = Mathf.Max(0f, axisIncreasePerSecond);
        }

        public CostAxis AffectedAxis => _affectedAxis;

        public float AxisIncreasePerSecond => _axisIncreasePerSecond;

        public bool IsConfigured => _axisIncreasePerSecond > 0f;

        public CostSignature ToContribution()
        {
            return _affectedAxis switch
            {
                CostAxis.Noise => new CostSignature(_axisIncreasePerSecond, 0f, 0f, 0f),
                CostAxis.Silhouette => new CostSignature(0f, _axisIncreasePerSecond, 0f, 0f),
                CostAxis.Handling => new CostSignature(0f, 0f, _axisIncreasePerSecond, 0f),
                _ => new CostSignature(0f, 0f, 0f, _axisIncreasePerSecond),
            };
        }
    }
}
