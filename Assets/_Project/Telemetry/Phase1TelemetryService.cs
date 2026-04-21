#nullable enable
using System;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;

namespace ExtractionWeight.Telemetry
{
    [DisallowMultipleComponent]
    public sealed class Phase1TelemetryService : MonoBehaviour
    {
        private static Phase1TelemetryService? s_instance;
        private static string? s_testDirectoryOverride;

        private TelemetryLogger? _logger;
        private string? _currentRunId;

        public static Phase1TelemetryService? Instance
        {
            get
            {
                if (s_instance == null)
                {
                    EnsureExists();
                }

                return s_instance;
            }
        }

        public string LogFilePath => _logger?.FilePath ?? string.Empty;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void EnsureExists()
        {
            if (s_instance != null)
            {
                return;
            }

            var root = new GameObject(nameof(Phase1TelemetryService));
            s_instance = root.AddComponent<Phase1TelemetryService>();
        }

        private void Awake()
        {
            if (s_instance != null && s_instance != this)
            {
                Destroy(gameObject);
                return;
            }

            s_instance = this;
            DontDestroyOnLoad(gameObject);
            _logger ??= new TelemetryLogger(BuildLogFilePath());
        }

        private void OnDestroy()
        {
            if (s_instance == this)
            {
                _logger?.Dispose();
                _logger = null;
                s_instance = null;
            }
        }

        public void BeginRun(string zoneId, string zoneDisplayName)
        {
            _currentRunId = Guid.NewGuid().ToString("N");
            _logger ??= new TelemetryLogger(BuildLogFilePath());
            _logger.Enqueue(TelemetryEventNames.RunStarted, _currentRunId, new RunStartedPayload
            {
                zoneId = zoneId,
                zoneDisplayName = zoneDisplayName,
            });
        }

        public void LogItemPickedUp(string itemId, string itemName, float value, string breakpoint, bool isVolatile, float capacityFraction)
        {
            if (_currentRunId == null || _logger == null)
            {
                return;
            }

            _logger.Enqueue(TelemetryEventNames.ItemPickedUp, _currentRunId, new ItemPickedUpPayload
            {
                itemId = itemId,
                itemName = itemName,
                value = value,
                breakpoint = breakpoint,
                isVolatile = isVolatile,
                capacityFraction = capacityFraction,
            });
        }

        public void LogBreakpointCrossed(string breakpoint, string direction, float capacityFraction)
        {
            if (_currentRunId == null || _logger == null)
            {
                return;
            }

            _logger.Enqueue(TelemetryEventNames.BreakpointCrossed, _currentRunId, new BreakpointCrossedPayload
            {
                breakpoint = breakpoint,
                direction = direction,
                capacityFraction = capacityFraction,
            });
        }

        public void LogThreatDetected(string threatId, string previousState, string currentState, Vector3 threatPosition)
        {
            if (_currentRunId == null || _logger == null)
            {
                return;
            }

            _logger.Enqueue(TelemetryEventNames.ThreatDetected, _currentRunId, new ThreatDetectedPayload
            {
                threatId = threatId,
                previousState = previousState,
                currentState = currentState,
                threatPosition = threatPosition,
            });
        }

        public void LogExtractionInitiated(string pointId, string extractionType, float elapsedRunSeconds)
        {
            if (_currentRunId == null || _logger == null)
            {
                return;
            }

            _logger.Enqueue(TelemetryEventNames.ExtractionInitiated, _currentRunId, new ExtractionInitiatedPayload
            {
                pointId = pointId,
                extractionType = extractionType,
                elapsedRunSeconds = elapsedRunSeconds,
            });
        }

        public void LogExtractionCompleted(string pointId, string extractionType, float elapsedRunSeconds, int bankedItemCount, float bankedValue)
        {
            if (_currentRunId == null || _logger == null)
            {
                return;
            }

            _logger.Enqueue(TelemetryEventNames.ExtractionCompleted, _currentRunId, new ExtractionCompletedPayload
            {
                pointId = pointId,
                extractionType = extractionType,
                elapsedRunSeconds = elapsedRunSeconds,
                bankedItemCount = bankedItemCount,
                bankedValue = bankedValue,
            });
        }

        public void LogPlayerDied(float lostLootValue, float elapsedRunSeconds)
        {
            if (_currentRunId == null || _logger == null)
            {
                return;
            }

            _logger.Enqueue(TelemetryEventNames.PlayerDied, _currentRunId, new PlayerDiedPayload
            {
                lostLootValue = lostLootValue,
                elapsedRunSeconds = elapsedRunSeconds,
            });
        }

        public Task FlushAsync()
        {
            return _logger?.FlushAsync() ?? Task.CompletedTask;
        }

#if UNITY_EDITOR
        public static void EditorSetLogDirectoryOverride(string? directoryPath)
        {
            s_testDirectoryOverride = directoryPath;
        }

        public static void EditorResetForTests()
        {
            if (s_instance != null)
            {
                DestroyImmediate(s_instance.gameObject);
            }

            s_instance = null;
        }
#endif

        private static string BuildLogFilePath()
        {
            var rootDirectory = !string.IsNullOrWhiteSpace(s_testDirectoryOverride)
                ? s_testDirectoryOverride!
                : Path.Combine(Application.persistentDataPath, "telemetry");
            return Path.Combine(rootDirectory, $"phase1-events-{DateTime.UtcNow:yyyyMMdd-HHmmss}.jsonl");
        }
    }
}
