#nullable enable
using System;
using UnityEngine;

namespace ExtractionWeight.Telemetry
{
    [Serializable]
    public sealed class RunStartedPayload
    {
        public string zoneId = string.Empty;
        public string zoneDisplayName = string.Empty;
    }

    [Serializable]
    public sealed class ItemPickedUpPayload
    {
        public string itemId = string.Empty;
        public string itemName = string.Empty;
        public float value;
        public string breakpoint = string.Empty;
        public bool isVolatile;
        public float capacityFraction;
    }

    [Serializable]
    public sealed class BreakpointCrossedPayload
    {
        public string breakpoint = string.Empty;
        public string direction = string.Empty;
        public float capacityFraction;
    }

    [Serializable]
    public sealed class ThreatDetectedPayload
    {
        public string threatId = string.Empty;
        public string previousState = string.Empty;
        public string currentState = string.Empty;
        public Vector3 threatPosition;
    }

    [Serializable]
    public sealed class ExtractionInitiatedPayload
    {
        public string pointId = string.Empty;
        public string extractionType = string.Empty;
        public float elapsedRunSeconds;
    }

    [Serializable]
    public sealed class ExtractionCompletedPayload
    {
        public string pointId = string.Empty;
        public string extractionType = string.Empty;
        public float elapsedRunSeconds;
        public int bankedItemCount;
        public float bankedValue;
    }

    [Serializable]
    public sealed class PlayerDiedPayload
    {
        public float lostLootValue;
        public float elapsedRunSeconds;
    }

    [Serializable]
    public sealed class TelemetryEnvelopeProbe
    {
        public string timestampUtc = string.Empty;
        public string runId = string.Empty;
        public string eventName = string.Empty;
    }

    public static class TelemetryEventNames
    {
        public const string RunStarted = "RunStarted";
        public const string ItemPickedUp = "ItemPickedUp";
        public const string BreakpointCrossed = "BreakpointCrossed";
        public const string ThreatDetected = "ThreatDetected";
        public const string ExtractionInitiated = "ExtractionInitiated";
        public const string ExtractionCompleted = "ExtractionCompleted";
        public const string PlayerDied = "PlayerDied";
    }
}
