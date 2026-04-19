#nullable enable
using System;
using UnityEngine;

namespace ExtractionWeight.Weight
{
    [Serializable]
    public readonly struct ZoneAxisWeights
    {
        private const float SumEpsilon = 0.0001f;

        public float Noise { get; }
        public float Silhouette { get; }
        public float Handling { get; }
        public float Mobility { get; }

        public ZoneAxisWeights(float noise, float silhouette, float handling, float mobility)
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

            Noise = noise;
            Silhouette = silhouette;
            Handling = handling;
            Mobility = mobility;
        }

        public static ZoneAxisWeights Uniform => new(0.25f, 0.25f, 0.25f, 0.25f);
    }
}
