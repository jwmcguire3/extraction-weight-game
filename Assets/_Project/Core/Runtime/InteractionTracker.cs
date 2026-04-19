#nullable enable
using System.Collections.Generic;
using UnityEngine;

namespace ExtractionWeight.Core
{
    [DisallowMultipleComponent]
    public sealed class InteractionTracker : MonoBehaviour
    {
        private const float MessageDurationSeconds = 1.2f;
        private const int MaxNearbyColliders = 16;

        private readonly List<IPickupInteractable> _eligiblePickups = new();
        private readonly Collider[] _nearbyResults = new Collider[MaxNearbyColliders];
        private float _currentHoldSeconds;
        private float _requiredHoldSeconds;
        private float _messageSecondsRemaining;

        [Min(0f)]
        [SerializeField]
        private float _fallbackScanRadius = 2f;

        public IPickupInteractable? CurrentPickup { get; private set; }

        public float CurrentHoldSeconds => _currentHoldSeconds;

        public float RequiredHoldSeconds => _requiredHoldSeconds;

        public float HoldProgress => _requiredHoldSeconds <= 0f ? 0f : Mathf.Clamp01(_currentHoldSeconds / _requiredHoldSeconds);

        public bool HasPickupCandidate => CurrentPickup != null;

        public string ActionLabel => CurrentPickup == null
            ? string.Empty
            : $"Pickup (holding: {_currentHoldSeconds:0.0}s)";

        public string HudMessage { get; private set; } = string.Empty;

        private void Update()
        {
            if (_messageSecondsRemaining <= 0f)
            {
                return;
            }

            _messageSecondsRemaining = Mathf.Max(0f, _messageSecondsRemaining - Time.deltaTime);
            if (_messageSecondsRemaining <= 0f)
            {
                HudMessage = string.Empty;
            }
        }

        public void RegisterPickupCandidate(IPickupInteractable pickup)
        {
            if (pickup is null || _eligiblePickups.Contains(pickup))
            {
                return;
            }

            _eligiblePickups.Add(pickup);
        }

        public void UnregisterPickupCandidate(IPickupInteractable pickup)
        {
            if (pickup is null)
            {
                return;
            }

            if (_eligiblePickups.Remove(pickup) && ReferenceEquals(CurrentPickup, pickup))
            {
                ResetHold();
                CurrentPickup = null;
            }
        }

        public void Tick(IPlayerCarryInteractor player, bool actionHeld, float deltaTimeSeconds)
        {
            RefreshCurrentPickup(player);

            if (CurrentPickup == null)
            {
                ResetHold();
                return;
            }

            _requiredHoldSeconds = Mathf.Max(0f, CurrentPickup.GetRequiredHoldDuration(player));
            if (!actionHeld)
            {
                ResetHold();
                return;
            }

            _currentHoldSeconds += Mathf.Max(0f, deltaTimeSeconds);
            if (_currentHoldSeconds < _requiredHoldSeconds)
            {
                return;
            }

            if (!CurrentPickup.TryCompletePickup(player, out var failureMessage))
            {
                ShowHudMessage(failureMessage);
            }

            ResetHold();
            RefreshCurrentPickup(player);
        }

        private void RefreshCurrentPickup(IPlayerCarryInteractor player)
        {
            RefreshNearbyPickups();

            IPickupInteractable? bestPickup = null;
            var bestDistance = float.PositiveInfinity;
            var playerPosition = transform.position;

            for (var i = _eligiblePickups.Count - 1; i >= 0; i--)
            {
                var pickup = _eligiblePickups[i];
                if (pickup == null || !pickup.IsAvailable)
                {
                    _eligiblePickups.RemoveAt(i);
                    continue;
                }

                var distance = Vector3.SqrMagnitude(pickup.WorldPosition - playerPosition);
                if (distance >= bestDistance)
                {
                    continue;
                }

                bestDistance = distance;
                bestPickup = pickup;
            }

            if (ReferenceEquals(bestPickup, CurrentPickup))
            {
                return;
            }

            CurrentPickup = bestPickup;
            ResetHold();
            _requiredHoldSeconds = CurrentPickup?.GetRequiredHoldDuration(player) ?? 0f;
        }

        private void RefreshNearbyPickups()
        {
            if (_fallbackScanRadius <= 0f)
            {
                return;
            }

            var hitCount = Physics.OverlapSphereNonAlloc(
                transform.position,
                _fallbackScanRadius,
                _nearbyResults,
                ~0,
                QueryTriggerInteraction.Collide);

            for (var i = 0; i < hitCount; i++)
            {
                var collider = _nearbyResults[i];
                if (collider == null)
                {
                    continue;
                }

                var pickup = FindPickupInteractable(collider);
                if (pickup != null)
                {
                    RegisterPickupCandidate(pickup);
                }

                _nearbyResults[i] = null;
            }
        }

        private static IPickupInteractable? FindPickupInteractable(Component source)
        {
            Transform? current = source.transform;
            while (current != null)
            {
                var behaviours = current.GetComponents<MonoBehaviour>();
                for (var i = 0; i < behaviours.Length; i++)
                {
                    if (behaviours[i] is IPickupInteractable pickup)
                    {
                        return pickup;
                    }
                }

                current = current.parent;
            }

            return null;
        }

        public void ShowHudMessage(string message)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                return;
            }

            HudMessage = message;
            _messageSecondsRemaining = MessageDurationSeconds;
        }

        private void ResetHold()
        {
            _currentHoldSeconds = 0f;
        }
    }
}
