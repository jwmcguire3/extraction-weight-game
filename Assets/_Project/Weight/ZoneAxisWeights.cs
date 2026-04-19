#nullable enable
using System;
using UnityEngine;

namespace ExtractionWeight.Weight
{
    [Serializable]
    public struct ZoneAxisWeights : ISerializationCallbackReceiver
    {
        private const float SumEpsilon = 0.0001f;

        [SerializeField] private float _noise;
        [SerializeField] private float _silhouette;
        [SerializeField] private float _handling;
        [SerializeField] private float _mobility;

        public float Noise => _noise;
        public float Silhouette => _silhouette;
        public float Handling => _handling;
        public float Mobility => _mobility;
        public float Sum => _noise + _silhouette + _handling + _mobility;

        public ZoneAxisWeights(float noise, float silhouette, float handling, float mobility)
        {
            Validate(noise, silhouette, handling, mobility);

            _noise = noise;
            _silhouette = silhouette;
            _handling = handling;
            _mobility = mobility;
        }

        public static ZoneAxisWeights Uniform => new(0.25f, 0.25f, 0.25f, 0.25f);

        public bool IsApproximatelyNormalized()
        {
            return Mathf.Abs(Sum - 1f) <= SumEpsilon;
        }

        public ZoneAxisWeights Normalized()
        {
            if (_noise < 0f || _silhouette < 0f || _handling < 0f || _mobility < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(_noise), "Zone axis weights must be non-negative.");
            }

            if (Mathf.Abs(Sum) <= SumEpsilon)
            {
                throw new ArgumentException("Zone axis weights cannot all be zero.");
            }

            return new ZoneAxisWeights(
                _noise / Sum,
                _silhouette / Sum,
                _handling / Sum,
                _mobility / Sum);
        }

        public void OnBeforeSerialize()
        {
        }

        public void OnAfterDeserialize()
        {
            if (_noise < 0f || _silhouette < 0f || _handling < 0f || _mobility < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(_noise), "Zone axis weights must be non-negative.");
            }
        }

        private static void Validate(float noise, float silhouette, float handling, float mobility)
        {
            if (noise < 0f || silhouette < 0f || handling < 0f || mobility < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(noise), "Zone axis weights must be non-negative.");
            }

            var sum = noise + silhouette + handling + mobility;
            if (Mathf.Abs(sum - 1f) > SumEpsilon)
            {
                throw new ArgumentException($"Zone axis weights must sum to 1.0. Received {sum:F4}.");
            }
        }
    }
}
