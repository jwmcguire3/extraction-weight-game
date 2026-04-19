#nullable enable
using System;
using ExtractionWeight.Core;
using UnityEngine;

namespace ExtractionWeight.Loot
{
    [Serializable]
    public struct AmbientAxisEffect : IAmbientEffect
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
    }
}
