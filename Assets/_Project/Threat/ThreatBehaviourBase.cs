#nullable enable
using ExtractionWeight.Core;
using ExtractionWeight.Weight;
using UnityEngine;
using WeightCarryState = ExtractionWeight.Weight.CarryState;

namespace ExtractionWeight.Threat
{
    public abstract class ThreatBehaviourBase : MonoBehaviour, IThreat
    {
        [SerializeField]
        private string _threatId = string.Empty;

        [SerializeField]
        protected DetectionProfile _profile;

        [Header("Detection")]
        [SerializeField]
        private PlayerController? _player;

        [SerializeField]
        private Collider? _playerCollider;

        [SerializeField]
        private LayerMask _lineOfSightMask = ~0;

        [Header("Audio")]
        [SerializeField]
        protected AudioSource? _idleAudioSource;

        [SerializeField]
        protected AudioSource? _alertAudioSource;

        [Header("Movement")]
        [SerializeField]
        protected float _rotationDegreesPerSecond = 360f;

        protected DetectionState _currentState;
        protected bool _isPursuing;

        private float _giveUpTimerSeconds;
        private bool _alertQueued;

        public string ThreatId => _threatId;

        public DetectionProfile Profile => _profile;

        public DetectionState CurrentState => _currentState;

        protected PlayerController? Player => _player;

        protected Collider? PlayerCollider => _playerCollider;

        protected virtual float GiveUpDelaySeconds => 0f;

        protected virtual void Reset()
        {
            if (string.IsNullOrWhiteSpace(_threatId))
            {
                _threatId = $"{GetType().Name}-{name}";
            }
        }

        protected virtual void Awake()
        {
            if (string.IsNullOrWhiteSpace(_threatId))
            {
                _threatId = $"{GetType().Name}-{name}";
            }

            ResolvePlayerReferences();
            ConfigureAudioSource(_idleAudioSource, loop: true);
            ConfigureAudioSource(_alertAudioSource, loop: false);
        }

        protected virtual void Start()
        {
            if (_idleAudioSource != null && _idleAudioSource.clip != null)
            {
                _idleAudioSource.Play();
            }
        }

        protected virtual void Update()
        {
            ResolvePlayerReferences();
            if (_player == null)
            {
                SetState(DetectionState.Unaware);
                OnNoPlayer();
                return;
            }

            var playerPosition = _player.transform.position;
            var distanceToPlayer = Vector3.Distance(transform.position, playerPosition);
            var detectedState = DetectionSystem.Evaluate(
                playerPosition,
                _player.CarryState,
                _player.CurrentPenalty,
                transform.position,
                _profile,
                _playerCollider,
                _lineOfSightMask);

            UpdatePursuitState(detectedState, distanceToPlayer);
            var effectiveState = _isPursuing ? DetectionState.Detected : detectedState;
            SetState(effectiveState);
            TickState(_currentState, _player, distanceToPlayer, Time.deltaTime);
        }

        protected virtual void OnNoPlayer()
        {
        }

        protected abstract void TickState(DetectionState state, PlayerController player, float distanceToPlayer, float deltaTime);

        protected virtual void OnStateChanged(DetectionState previousState, DetectionState nextState)
        {
            if (nextState == DetectionState.Detected && !_alertQueued)
            {
                _alertQueued = true;
                if (_alertAudioSource != null && _alertAudioSource.clip != null)
                {
                    _alertAudioSource.PlayOneShot(_alertAudioSource.clip);
                }
            }
            else if (nextState != DetectionState.Detected)
            {
                _alertQueued = false;
            }
        }

        protected void ResolvePlayerReferences()
        {
            if (_player == null)
            {
                _player = FindAnyObjectByType<PlayerController>();
            }

            if (_playerCollider == null && _player != null)
            {
                _playerCollider = _player.GetComponent<Collider>();
            }
        }

        protected void FaceTowards(Vector3 worldPosition, float deltaTime)
        {
            var flatDirection = worldPosition - transform.position;
            flatDirection.y = 0f;
            if (flatDirection.sqrMagnitude <= 0.0001f)
            {
                return;
            }

            var targetRotation = Quaternion.LookRotation(flatDirection.normalized, Vector3.up);
            transform.rotation = Quaternion.RotateTowards(
                transform.rotation,
                targetRotation,
                _rotationDegreesPerSecond * deltaTime);
        }

        protected void MoveTowards(Vector3 worldPosition, float speed, float deltaTime)
        {
            var target = new Vector3(worldPosition.x, transform.position.y, worldPosition.z);
            transform.position = Vector3.MoveTowards(transform.position, target, speed * deltaTime);
            FaceTowards(target, deltaTime);
        }

        private void UpdatePursuitState(DetectionState detectedState, float distanceToPlayer)
        {
            if (detectedState == DetectionState.Detected && distanceToPlayer <= _profile.PursuitRange)
            {
                _isPursuing = true;
                _giveUpTimerSeconds = 0f;
                return;
            }

            if (!_isPursuing)
            {
                _giveUpTimerSeconds = 0f;
                return;
            }

            if (distanceToPlayer <= _profile.GiveUpRange)
            {
                _giveUpTimerSeconds = 0f;
                return;
            }

            if (detectedState == DetectionState.Unaware && distanceToPlayer >= _profile.GiveUpRange * 2f)
            {
                _isPursuing = false;
                _giveUpTimerSeconds = 0f;
                return;
            }

            if (GiveUpDelaySeconds <= 0f)
            {
                _isPursuing = false;
                _giveUpTimerSeconds = 0f;
                return;
            }

            _giveUpTimerSeconds += Time.deltaTime;
            if (_giveUpTimerSeconds >= GiveUpDelaySeconds)
            {
                _isPursuing = false;
                _giveUpTimerSeconds = 0f;
            }
        }

        private void SetState(DetectionState nextState)
        {
            if (_currentState == nextState)
            {
                return;
            }

            var previousState = _currentState;
            _currentState = nextState;
            OnStateChanged(previousState, nextState);
        }

        private static void ConfigureAudioSource(AudioSource? source, bool loop)
        {
            if (source == null)
            {
                return;
            }

            source.playOnAwake = false;
            source.loop = loop;
            source.spatialBlend = 1f;
            source.dopplerLevel = 0f;
            source.rolloffMode = AudioRolloffMode.Linear;
        }

#if UNITY_EDITOR
        public void EditorAssignPlayer(PlayerController? player)
        {
            _player = player;
            _playerCollider = player != null ? player.GetComponent<Collider>() : null;
        }

        public void EditorSetThreatId(string threatId)
        {
            _threatId = threatId;
        }
#endif
    }
}
