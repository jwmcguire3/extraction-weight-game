#nullable enable
using System.Collections.Generic;
using ExtractionWeight.Core;
using ExtractionWeight.MetaState;
using ExtractionWeight.Telemetry;
using ExtractionWeight.Threat;
using ExtractionWeight.Zone;
using UnityEngine;

namespace ExtractionWeight.Extraction
{
    [DisallowMultipleComponent]
    public sealed class ExtractionController : MonoBehaviour
    {
        [SerializeField]
        private string _pointId = string.Empty;

        [SerializeField]
        private ZoneRuntime? _zoneRuntime;

        [SerializeField]
        private PlayerContextActionTarget? _contextActionTarget;

        [SerializeField]
        private Transform? _holdCenter;

        [SerializeField]
        private Transform? _boundaryMarker;

        [SerializeField]
        private Warden? _extraThreat;

        private readonly ExtractionStateMachine _stateMachine = new();

        private PlayerController? _activePlayer;
        private ExtractionPointData? _pointData;
        private float _stateElapsedSeconds;
        private bool _extraThreatSpawned;

        public ExtractionPhaseState CurrentState => _stateMachine.State;

        public bool HasSpawnedExtraThreat => _extraThreatSpawned;

        public string LastFailureMessage { get; private set; } = string.Empty;

        private void Awake()
        {
            _zoneRuntime ??= FindAnyObjectByType<ZoneRuntime>();
            _contextActionTarget ??= GetComponent<PlayerContextActionTarget>();
            _holdCenter ??= transform;

            if (_extraThreat != null)
            {
                _extraThreat.gameObject.SetActive(false);
            }
        }

        private void OnEnable()
        {
            if (_contextActionTarget != null)
            {
                _contextActionTarget.Activated += HandleActivated;
            }
        }

        private void OnDisable()
        {
            if (_contextActionTarget != null)
            {
                _contextActionTarget.Activated -= HandleActivated;
            }
        }

        private void Update()
        {
            _zoneRuntime ??= FindAnyObjectByType<ZoneRuntime>();
            ResolvePointData();
            SyncClosedState();
            UpdateActionAvailability();

            if (_pointData == null || _activePlayer == null)
            {
                return;
            }

            _stateElapsedSeconds += Time.deltaTime;

            switch (_stateMachine.State)
            {
                case ExtractionPhaseState.Initiation:
                    UpdateInitiation();
                    break;
                case ExtractionPhaseState.Approach:
                    UpdateApproach();
                    break;
                case ExtractionPhaseState.Hold:
                    UpdateHold();
                    break;
                case ExtractionPhaseState.Departure:
                    UpdateDeparture();
                    break;
            }
        }

        public void TriggerExtraction(PlayerController playerController)
        {
            HandleActivated(playerController);
        }

        private void HandleActivated(PlayerController playerController)
        {
            _zoneRuntime ??= FindAnyObjectByType<ZoneRuntime>();
            ResolvePointData();
            if (_pointData == null || _zoneRuntime == null)
            {
                return;
            }

            if (!_zoneRuntime.IsExtractionOpen(_pointData.PointId))
            {
                Fail("Extraction closed");
                return;
            }

            if (_stateMachine.State != ExtractionPhaseState.Idle)
            {
                return;
            }

            _activePlayer = playerController;
            GameFlowManager.Instance?.NotifyExtractionStarted();
            Phase1TelemetryService.Instance?.LogExtractionInitiated(_pointData.PointId, _pointData.ExtractionType.ToString(), _zoneRuntime.ElapsedRunSeconds);
            _stateMachine.TransitionTo(ExtractionPhaseState.Initiation);
            _stateElapsedSeconds = 0f;
            _extraThreatSpawned = false;
            LastFailureMessage = string.Empty;
        }

        private void UpdateInitiation()
        {
            if (_pointData == null || _activePlayer == null)
            {
                return;
            }

            if (!_zoneRuntime!.IsExtractionOpen(_pointData.PointId))
            {
                Fail("Extraction closed");
                return;
            }

            if (!ExtractionRules.IsCarryCompatible(_activePlayer.CarryState, _pointData.ItemSizeFilter))
            {
                Fail(ExtractionRules.GetCompatibilityFailureMessage(_pointData));
                return;
            }

            if (!ExtractionRules.IsCarryWithinCapacity(_activePlayer.CarryState, _pointData.MaxCarryCapacityFraction))
            {
                Fail(ExtractionRules.GetCapacityFailureMessage(_pointData.ExtractionType));
                return;
            }

            if (_stateElapsedSeconds < _pointData.InitiationDurationSeconds)
            {
                return;
            }

            if (_pointData.ExtractionType == ExtractionType.Overland)
            {
                _stateMachine.TransitionTo(ExtractionPhaseState.Approach);
            }
            else
            {
                _stateMachine.TransitionTo(ExtractionPhaseState.Hold);
            }

            _stateElapsedSeconds = 0f;
        }

        private void UpdateApproach()
        {
            if (_pointData == null || _activePlayer == null)
            {
                return;
            }

            if (_pointData.ExtractionType != ExtractionType.Overland)
            {
                _stateMachine.TransitionTo(ExtractionPhaseState.Hold);
                _stateElapsedSeconds = 0f;
                return;
            }

            TrySpawnExtraThreat(_pointData.ApproachDurationSeconds);

            if (_boundaryMarker != null &&
                Vector3.Distance(_activePlayer.transform.position, _boundaryMarker.position) <= 1.25f)
            {
                _stateMachine.TransitionTo(ExtractionPhaseState.Departure);
                _stateElapsedSeconds = 0f;
            }
        }

        private void UpdateHold()
        {
            if (_pointData == null || _activePlayer == null || _holdCenter == null)
            {
                return;
            }

            var distanceFromHoldCenter = Vector3.Distance(_activePlayer.transform.position, _holdCenter.position);
            if (distanceFromHoldCenter > _pointData.HoldRadius)
            {
                Fail("Extraction aborted");
                return;
            }

            TrySpawnExtraThreat(_pointData.HoldDurationSeconds);

            if (_stateElapsedSeconds < _pointData.HoldDurationSeconds)
            {
                return;
            }

            _stateMachine.TransitionTo(ExtractionPhaseState.Departure);
            _stateElapsedSeconds = 0f;
        }

        private void UpdateDeparture()
        {
            if (_pointData == null || _activePlayer == null || _zoneRuntime?.CurrentZoneDefinition == null)
            {
                return;
            }

            if (_stateElapsedSeconds < _pointData.DepartureDurationSeconds)
            {
                return;
            }

            var bankedItems = CollectLootItems(_activePlayer.CarryState);
            Phase1TelemetryService.Instance?.LogExtractionCompleted(
                _pointData.PointId,
                _pointData.ExtractionType.ToString(),
                _zoneRuntime.ElapsedRunSeconds,
                bankedItems.Count,
                CalculateBankedValue(bankedItems));
            GameFlowManager.Instance?.CompleteSuccessfulRun(_zoneRuntime.CurrentZoneDefinition.ZoneId, bankedItems);
            Phase1RunResultStore.CompleteSuccessfulExtraction(
                _zoneRuntime.CurrentZoneDefinition.ZoneId,
                _pointData.PointId,
                _pointData.ExtractionType.ToString(),
                _activePlayer.CarryState);

            _activePlayer.gameObject.SetActive(false);
            _stateMachine.TransitionTo(ExtractionPhaseState.Completed);
            _stateElapsedSeconds = 0f;
        }

        private void TrySpawnExtraThreat(float durationSeconds)
        {
            if (_extraThreatSpawned || _extraThreat == null || durationSeconds <= 0f)
            {
                return;
            }

            if (_pointData == null)
            {
                return;
            }

            if (_pointData.ExtractionType == ExtractionType.Drone)
            {
                return;
            }

            var progress = _stateElapsedSeconds / durationSeconds;
            if (progress < 0.5f)
            {
                return;
            }

            _extraThreat.gameObject.SetActive(true);
            _extraThreatSpawned = true;
        }

        private void UpdateActionAvailability()
        {
            if (_contextActionTarget == null || _pointData == null || _zoneRuntime == null)
            {
                return;
            }

            var isAvailable = _stateMachine.State == ExtractionPhaseState.Idle && _zoneRuntime.IsExtractionOpen(_pointData.PointId);
            _contextActionTarget.enabled = isAvailable;
        }

        private void SyncClosedState()
        {
            if (_pointData == null || _zoneRuntime == null)
            {
                return;
            }

            var isClosed = !_zoneRuntime.IsExtractionOpen(_pointData.PointId);
            if (isClosed && _stateMachine.State == ExtractionPhaseState.Idle)
            {
                _stateMachine.TransitionTo(ExtractionPhaseState.Closed);
                return;
            }

            if (!isClosed && _stateMachine.State == ExtractionPhaseState.Closed)
            {
                _stateMachine.ResetToIdle();
            }
        }

        private void ResolvePointData()
        {
            if (_pointData != null || _zoneRuntime == null)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(_pointId))
            {
                _pointId = name.Replace("ExtractionPoint_", string.Empty);
            }

            if (_zoneRuntime.TryGetExtractionPointData(_pointId, out var pointData))
            {
                _pointData = pointData;
                if (_contextActionTarget != null)
                {
                    _contextActionTarget.Configure(ContextActionKind.Extract, GetActionLabel(pointData), pointData.InteractionRadius, 5);
                }
            }
        }

        private static List<ExtractionWeight.Loot.LootItem> CollectLootItems(ExtractionWeight.Weight.CarryState carryState)
        {
            var items = new List<ExtractionWeight.Loot.LootItem>(carryState.Items.Count);
            for (var i = 0; i < carryState.Items.Count; i++)
            {
                if (carryState.Items[i] is ExtractionWeight.Loot.LootItem lootItem)
                {
                    items.Add(lootItem);
                }
            }

            return items;
        }

        private static float CalculateBankedValue(IReadOnlyList<ExtractionWeight.Loot.LootItem> items)
        {
            var total = 0f;
            for (var i = 0; i < items.Count; i++)
            {
                total += items[i].Value;
            }

            return total;
        }

        private string GetActionLabel(ExtractionPointData pointData)
        {
            return pointData.ExtractionType switch
            {
                ExtractionType.Standard => "Standard Extract",
                ExtractionType.Drone => "Drone Extract",
                ExtractionType.Overland => "Overland Extract",
                _ => "Extract",
            };
        }

        private void Fail(string message)
        {
            LastFailureMessage = message;
            if (_activePlayer != null)
            {
                var tracker = _activePlayer.GetComponent<InteractionTracker>();
                if (tracker != null)
                {
                    tracker.ShowHudMessage(message);
                }
            }

            _stateMachine.ResetToIdle();
            _stateElapsedSeconds = 0f;
            _activePlayer = null;
            if (_extraThreat != null)
            {
                _extraThreat.gameObject.SetActive(false);
            }

            SyncClosedState();
        }

#if UNITY_EDITOR
        public void EditorConfigure(string pointId, Transform holdCenter, Transform? boundaryMarker, Warden? extraThreat)
        {
            _pointId = pointId;
            _holdCenter = holdCenter;
            _boundaryMarker = boundaryMarker;
            _extraThreat = extraThreat;
        }

        public void EditorAssignRuntime(ZoneRuntime zoneRuntime)
        {
            _zoneRuntime = zoneRuntime;
            _pointData = null;
        }
#endif
    }
}
