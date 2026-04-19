#nullable enable
using System;
using UnityEngine;

namespace ExtractionWeight.Core
{
    [Serializable]
    public struct CostSignature : IEquatable<CostSignature>
    {
        public const float MinAxisValue = 0f;
        public const float MaxAxisValue = 1f;
        public const float EqualityEpsilon = 0.0001f;

        public float Noise;
        public float Silhouette;
        public float Handling;
        public float Mobility;

        public CostSignature(float noise, float silhouette, float handling, float mobility)
            : this(noise, silhouette, handling, mobility, clampAxes: true)
        {
        }

        private CostSignature(float noise, float silhouette, float handling, float mobility, bool clampAxes)
        {
            if (clampAxes)
            {
                Noise = Mathf.Clamp(noise, MinAxisValue, MaxAxisValue);
                Silhouette = Mathf.Clamp(silhouette, MinAxisValue, MaxAxisValue);
                Handling = Mathf.Clamp(handling, MinAxisValue, MaxAxisValue);
                Mobility = Mathf.Clamp(mobility, MinAxisValue, MaxAxisValue);
                return;
            }

            Noise = noise;
            Silhouette = silhouette;
            Handling = handling;
            Mobility = mobility;
        }

        public float Magnitude => Mathf.Sqrt((Noise * Noise) + (Silhouette * Silhouette) + (Handling * Handling) + (Mobility * Mobility));

        public static CostSignature operator +(CostSignature left, CostSignature right)
        {
            return new CostSignature(
                left.Noise + right.Noise,
                left.Silhouette + right.Silhouette,
                left.Handling + right.Handling,
                left.Mobility + right.Mobility,
                clampAxes: false);
        }

        public static CostSignature operator -(CostSignature left, CostSignature right)
        {
            return new CostSignature(
                left.Noise - right.Noise,
                left.Silhouette - right.Silhouette,
                left.Handling - right.Handling,
                left.Mobility - right.Mobility,
                clampAxes: false);
        }

        public static CostSignature operator *(CostSignature signature, float scalar)
        {
            return new CostSignature(
                signature.Noise * scalar,
                signature.Silhouette * scalar,
                signature.Handling * scalar,
                signature.Mobility * scalar,
                clampAxes: false);
        }

        public static bool operator ==(CostSignature left, CostSignature right) => left.Equals(right);

        public static bool operator !=(CostSignature left, CostSignature right) => !left.Equals(right);

        public bool Equals(CostSignature other)
        {
            return Mathf.Abs(Noise - other.Noise) <= EqualityEpsilon
                && Mathf.Abs(Silhouette - other.Silhouette) <= EqualityEpsilon
                && Mathf.Abs(Handling - other.Handling) <= EqualityEpsilon
                && Mathf.Abs(Mobility - other.Mobility) <= EqualityEpsilon;
        }

        public override bool Equals(object? obj) => obj is CostSignature other && Equals(other);

        public override int GetHashCode() => HashCode.Combine(Noise, Silhouette, Handling, Mobility);

        public override string ToString()
        {
            return $"CostSignature(Noise: {Noise:F3}, Silhouette: {Silhouette:F3}, Handling: {Handling:F3}, Mobility: {Mobility:F3})";
        }
    }
}
