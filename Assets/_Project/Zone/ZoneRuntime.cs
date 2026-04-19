#nullable enable
using System;
using System.Collections.Generic;
using ExtractionWeight.Core;
using UnityEngine;

namespace ExtractionWeight.Zone
{
    [DisallowMultipleComponent]
    public sealed class ZoneRuntime : MonoBehaviour
    {
        private readonly Dictionary<string, ExtractionPointData> _pointLookup = new(StringComparer.Ordinal);
        private readonly Dictionary<string, bool> _openStateLookup = new(StringComparer.Ordinal);

        [SerializeField]
        private ZoneDefinition? _zoneDefinition;

        [SerializeField]
        private PlayerController? _playerController;

        public ZoneDefinition? CurrentZoneDefinition => _zoneDefinition;

        public float ElapsedRunSeconds { get; private set; }

        public string OpenExtractionSummary { get; private set; } = string.Empty;

        public event Action? TideStateChanged;

        private void Awake()
        {
            _playerController ??= FindAnyObjectByType<PlayerController>();
            RebuildLookup();
        }

        private void Update()
        {
            if (_zoneDefinition == null)
            {
                return;
            }

            ElapsedRunSeconds += Time.deltaTime;
            _playerController ??= FindAnyObjectByType<PlayerController>();
            _playerController?.SetTideSecondsRemaining(_zoneDefinition.TideDurationSeconds - ElapsedRunSeconds);

            var changed = RefreshOpenStates();
            if (changed)
            {
                TideStateChanged?.Invoke();
            }
        }

        public void Initialize(ZoneDefinition zoneDefinition)
        {
            _zoneDefinition = zoneDefinition;
            ElapsedRunSeconds = 0f;
            RebuildLookup();
            RefreshOpenStates(force: true);
            TideStateChanged?.Invoke();
        }

        public bool TryGetExtractionPointData(string pointId, out ExtractionPointData pointData)
        {
            return _pointLookup.TryGetValue(pointId, out pointData!);
        }

        public bool IsExtractionOpen(string pointId)
        {
            return _openStateLookup.TryGetValue(pointId, out var isOpen) && isOpen;
        }

        public static bool IsOpenAtTime(ExtractionPointData pointData, float elapsedRunSeconds)
        {
            if (pointData == null)
            {
                throw new ArgumentNullException(nameof(pointData));
            }

            return elapsedRunSeconds < pointData.TideCloseTime;
        }

        private void RebuildLookup()
        {
            _pointLookup.Clear();
            _openStateLookup.Clear();

            if (_zoneDefinition == null)
            {
                OpenExtractionSummary = string.Empty;
                return;
            }

            for (var i = 0; i < _zoneDefinition.ExtractionPoints.Count; i++)
            {
                var point = _zoneDefinition.ExtractionPoints[i];
                _pointLookup[point.PointId] = point;
                _openStateLookup[point.PointId] = IsOpenAtTime(point, ElapsedRunSeconds);
            }

            OpenExtractionSummary = BuildSummary();
        }

        private bool RefreshOpenStates(bool force = false)
        {
            var changed = force;
            if (_zoneDefinition == null)
            {
                OpenExtractionSummary = string.Empty;
                return changed;
            }

            for (var i = 0; i < _zoneDefinition.ExtractionPoints.Count; i++)
            {
                var point = _zoneDefinition.ExtractionPoints[i];
                var nextOpenState = IsOpenAtTime(point, ElapsedRunSeconds);
                if (!_openStateLookup.TryGetValue(point.PointId, out var existingState) || existingState != nextOpenState)
                {
                    _openStateLookup[point.PointId] = nextOpenState;
                    changed = true;
                }
            }

            var summary = BuildSummary();
            if (!string.Equals(summary, OpenExtractionSummary, StringComparison.Ordinal))
            {
                OpenExtractionSummary = summary;
                changed = true;
            }

            return changed;
        }

        private string BuildSummary()
        {
            if (_zoneDefinition == null)
            {
                return string.Empty;
            }

            var openIds = new List<string>();
            for (var i = 0; i < _zoneDefinition.ExtractionPoints.Count; i++)
            {
                var point = _zoneDefinition.ExtractionPoints[i];
                if (IsOpenAtTime(point, ElapsedRunSeconds))
                {
                    openIds.Add(point.PointId);
                }
            }

            return openIds.Count == 0
                ? "Exits closed"
                : $"Open exits: {string.Join(" ", openIds)}";
        }

#if UNITY_EDITOR
        public void EditorConfigure(ZoneDefinition zoneDefinition)
        {
            _zoneDefinition = zoneDefinition;
            ElapsedRunSeconds = 0f;
            RebuildLookup();
        }
#endif
    }
}
