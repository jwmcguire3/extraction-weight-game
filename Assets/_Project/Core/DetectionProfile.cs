#nullable enable
using System;
using UnityEngine;

namespace ExtractionWeight.Core
{
    [Serializable]
    public struct DetectionProfile
    {
        [Range(0f, 1f)]
        [SerializeField]
        private float _noiseWeight;

        [Range(0f, 1f)]
        [SerializeField]
        private float _silhouetteWeight;

        [Min(0f)]
        [SerializeField]
        private float _baseDetectionRange;

        [Min(0f)]
        [SerializeField]
        private float _pursuitRange;

        [Min(0f)]
        [SerializeField]
        private float _giveUpRange;

        public DetectionProfile(
            float noiseWeight,
            float silhouetteWeight,
            float baseDetectionRange,
            float pursuitRange,
            float giveUpRange)
        {
            _noiseWeight = Mathf.Clamp01(noiseWeight);
            _silhouetteWeight = Mathf.Clamp01(silhouetteWeight);
            _baseDetectionRange = Mathf.Max(0f, baseDetectionRange);
            _pursuitRange = Mathf.Max(0f, pursuitRange);
            _giveUpRange = Mathf.Max(_pursuitRange, giveUpRange);
        }

        public float NoiseWeight => _noiseWeight;

        public float SilhouetteWeight => _silhouetteWeight;

        public float BaseDetectionRange => _baseDetectionRange;

        public float PursuitRange => _pursuitRange;

        public float GiveUpRange => _giveUpRange;

        public float CombinedWeight => _noiseWeight + _silhouetteWeight;
    }
}
