#nullable enable
using ExtractionWeight.Core;
using ExtractionWeight.Weight;
using UnityEngine;

namespace ExtractionWeight.Threat
{
    public static class DetectionSystem
    {
        private const float OverlapDistanceEpsilon = 0.05f;
        private const float MaximumDetectionScale = 1.5f;
        private const float SuspiciousBandScale = 1f / 0.7f;
        private const float MinimumLineOfSightSilhouetteWeight = 0.5f;

        public static DetectionState Evaluate(
            Vector3 playerPosition,
            CarryState carryState,
            AppliedPenalty penalties,
            Vector3 threatPosition,
            DetectionProfile profile,
            Collider? playerCollider = null,
            int lineOfSightMask = Physics.DefaultRaycastLayers)
        {
            _ = carryState;

            var distance = Vector3.Distance(playerPosition, threatPosition);
            if (distance <= OverlapDistanceEpsilon)
            {
                return DetectionState.Detected;
            }

            var effectiveRange = CalculateEffectiveDetectionRange(penalties, profile);
            if (effectiveRange <= 0f)
            {
                return DetectionState.Unaware;
            }

            var suspiciousRange = CalculateSuspiciousRange(penalties, profile);
            if (distance > suspiciousRange)
            {
                return DetectionState.Unaware;
            }

            if (RequiresLineOfSight(profile) &&
                !HasLineOfSight(threatPosition, playerPosition, playerCollider, lineOfSightMask))
            {
                return DetectionState.Unaware;
            }

            return distance <= effectiveRange
                ? DetectionState.Detected
                : DetectionState.Suspicious;
        }

        public static float CalculateEffectiveDetectionRange(AppliedPenalty penalties, DetectionProfile profile)
        {
            var weightedMultiplier =
                (profile.NoiseWeight * penalties.NoiseMultiplier) +
                (profile.SilhouetteWeight * penalties.SilhouetteMultiplier);

            var cappedMultiplier = Mathf.Clamp(weightedMultiplier, 0f, MaximumDetectionScale);
            return profile.BaseDetectionRange * cappedMultiplier;
        }

        public static float CalculateSuspiciousRange(AppliedPenalty penalties, DetectionProfile profile)
        {
            var effectiveRange = CalculateEffectiveDetectionRange(penalties, profile);
            var expandedRange = effectiveRange * SuspiciousBandScale;
            var hardCap = profile.BaseDetectionRange * MaximumDetectionScale;
            return Mathf.Min(expandedRange, hardCap);
        }

        public static bool RequiresLineOfSight(DetectionProfile profile)
        {
            return profile.SilhouetteWeight >= MinimumLineOfSightSilhouetteWeight &&
                   profile.SilhouetteWeight > profile.NoiseWeight;
        }

        public static bool HasLineOfSight(
            Vector3 threatPosition,
            Vector3 playerPosition,
            Collider? playerCollider = null,
            int lineOfSightMask = Physics.DefaultRaycastLayers)
        {
            var origin = threatPosition + (Vector3.up * 0.6f);
            var target = playerPosition + (Vector3.up * 0.9f);
            var direction = target - origin;
            var distance = direction.magnitude;
            if (distance <= OverlapDistanceEpsilon)
            {
                return true;
            }

            direction /= distance;
            if (!Physics.Raycast(origin, direction, out var hit, distance, lineOfSightMask, QueryTriggerInteraction.Ignore))
            {
                return true;
            }

            return playerCollider != null && IsPlayerCollider(hit.collider, playerCollider);
        }

        private static bool IsPlayerCollider(Collider collider, Collider playerCollider)
        {
            if (ReferenceEquals(collider, playerCollider))
            {
                return true;
            }

            return collider.transform.IsChildOf(playerCollider.transform) || playerCollider.transform.IsChildOf(collider.transform);
        }
    }
}
