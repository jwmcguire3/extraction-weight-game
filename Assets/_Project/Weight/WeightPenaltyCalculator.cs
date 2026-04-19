#nullable enable
using UnityEngine;

namespace ExtractionWeight.Weight
{
    public static class WeightPenaltyCalculator
    {
        private const float LightThreshold = CarryState.LoadedThreshold;
        private const float LoadedThreshold = CarryState.OverburdenedThreshold;
        private const float OverburdenedThreshold = CarryState.SoftCeilingThreshold;
        private const float MaxThreshold = CarryState.MaxCapacityFraction;

        public static AppliedPenalty Compute(CarryState carryState, ZoneAxisWeights zoneAxisWeights)
        {
            var noiseSeverity = GetAmplifiedSeverity(carryState.TotalCost.Noise / carryState.CarryCapacity, zoneAxisWeights.Noise);
            var silhouetteSeverity = GetAmplifiedSeverity(carryState.TotalCost.Silhouette / carryState.CarryCapacity, zoneAxisWeights.Silhouette);
            var handlingSeverity = GetAmplifiedSeverity(carryState.TotalCost.Handling / carryState.CarryCapacity, zoneAxisWeights.Handling);
            var mobilitySeverity = GetAmplifiedSeverity(carryState.TotalCost.Mobility / carryState.CarryCapacity, zoneAxisWeights.Mobility);

            var noiseMultiplier = Mathf.Clamp(1f + (1.5f * noiseSeverity), 0.5f, 2.5f);
            var silhouetteMultiplier = Mathf.Clamp(1f + (1.5f * silhouetteSeverity), 0.5f, 2.5f);
            var handlingMultiplier = Mathf.Clamp(1f + handlingSeverity, 0.5f, 2f);
            var mobilityMultiplier = Mathf.Clamp(1f - (0.5f * mobilitySeverity), 0.5f, 1f);

            return new AppliedPenalty(noiseMultiplier, silhouetteMultiplier, handlingMultiplier, mobilityMultiplier);
        }

        private static float GetAmplifiedSeverity(float axisFraction, float zoneWeight)
        {
            var severity = EvaluateSeverityCurve(axisFraction);
            var amplified = severity * (1f + zoneWeight);
            return Mathf.Clamp01(amplified);
        }

        private static float EvaluateSeverityCurve(float fraction)
        {
            var clamped = Mathf.Clamp(fraction, 0f, MaxThreshold);

            if (clamped <= LightThreshold)
            {
                return 0.1f * NormalizedSmooth(clamped, 0f, LightThreshold);
            }

            if (clamped <= LoadedThreshold)
            {
                return Mathf.Lerp(0.1f, 0.45f, NormalizedSmooth(clamped, LightThreshold, LoadedThreshold));
            }

            if (clamped <= OverburdenedThreshold)
            {
                return Mathf.Lerp(0.45f, 0.8f, NormalizedSmooth(clamped, LoadedThreshold, OverburdenedThreshold));
            }

            return Mathf.Lerp(0.8f, 1f, NormalizedSmooth(clamped, OverburdenedThreshold, MaxThreshold));
        }

        private static float NormalizedSmooth(float value, float min, float max)
        {
            var t = Mathf.InverseLerp(min, max, value);
            return t * t * (3f - (2f * t));
        }
    }
}
