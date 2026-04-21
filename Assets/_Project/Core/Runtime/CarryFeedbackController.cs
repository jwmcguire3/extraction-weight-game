#nullable enable
using ExtractionWeight.Weight;
using Cinemachine;
using ExtractionWeight.Telemetry;
using UnityEngine;

namespace ExtractionWeight.Core
{
    [DisallowMultipleComponent]
    public sealed class CarryFeedbackController : MonoBehaviour
    {
        private const float MaxCapacityFraction = CarryState.MaxCapacityFraction;

        [SerializeField]
        private PlayerController? _playerController;

        [SerializeField]
        private Transform? _cameraRoot;

        [SerializeField]
        private CinemachineVirtualCamera? _virtualCamera;

        [SerializeField]
        private AudioSource? _footstepLightSource;

        [SerializeField]
        private AudioSource? _footstepLoadedSource;

        [SerializeField]
        private AudioSource? _footstepOverburdenedSource;

        [SerializeField]
        private AudioSource? _breathSource;

        [SerializeField]
        private AudioSource? _ambientItemSource;

        [Header("Visuals")]
        [Min(0f)]
        [SerializeField]
        private float _maxSwayAmplitude = 1.25f;

        [Min(0f)]
        [SerializeField]
        private float _maxCameraDrop = 0.15f;

        [Min(0f)]
        [SerializeField]
        private float _overburdenedFovReduction = 6f;

        [Header("Audio")]
        [Min(0.01f)]
        [SerializeField]
        private float _footstepIntervalBase = 0.5f;

        [Range(0f, 1f)]
        [SerializeField]
        private float _maxBreathVolume = 0.75f;

        [Range(0.5f, 1.5f)]
        [SerializeField]
        private float _overburdenedBreathPitch = 0.9f;

        private CarryBreakpoint _lastBreakpoint;
        private Vector3 _cameraRootLocalPosition;
        private float _baseFieldOfView;
        private float _footstepTimer;
        private CinemachineBasicMultiChannelPerlin? _noise;

        public ICarryFeedbackAudioDriver? AudioDriverOverride { get; set; }

        public IPlayerHaptics? HapticsOverride { get; set; }

        public event System.Action<CarryBreakpoint>? BreakpointCrossedUpward;

        public event System.Action<CarryBreakpoint>? BreakpointCrossedDownward;

        public event System.Action<CarryBreakpoint>? FootstepTriggered;

        private void Awake()
        {
            _playerController ??= GetComponent<PlayerController>();

            if (_cameraRoot != null)
            {
                _cameraRootLocalPosition = _cameraRoot.localPosition;
            }

            if (_virtualCamera != null)
            {
                _baseFieldOfView = _virtualCamera.m_Lens.FieldOfView;
                _noise = _virtualCamera.GetCinemachineComponent<CinemachineBasicMultiChannelPerlin>();
            }
        }

        private void OnEnable()
        {
            if (_playerController != null)
            {
                _lastBreakpoint = _playerController.CurrentBreakpoint;
                _playerController.CarryState.OnCarryChanged += HandleCarryChanged;
            }
        }

        private void OnDisable()
        {
            if (_playerController != null)
            {
                _playerController.CarryState.OnCarryChanged -= HandleCarryChanged;
            }
        }

        private void Update()
        {
            if (_playerController == null)
            {
                return;
            }

            var currentBreakpoint = _playerController.CurrentBreakpoint;
            if (currentBreakpoint != _lastBreakpoint)
            {
                HandleBreakpointTransition(_lastBreakpoint, currentBreakpoint);
            }

            _lastBreakpoint = currentBreakpoint;

            var capacityFraction = _playerController.CarryState.CapacityFraction;
            var normalizedFraction = Mathf.Clamp01(capacityFraction / MaxCapacityFraction);
            ApplyCameraFeedback(normalizedFraction, capacityFraction);
            UpdateBreathAudio(normalizedFraction);
            UpdateAmbientAudio();
            UpdateFootsteps();
        }

        private void HandleCarryChanged()
        {
            if (_playerController == null)
            {
                return;
            }

            var currentBreakpoint = _playerController.CurrentBreakpoint;
            if (currentBreakpoint != _lastBreakpoint)
            {
                HandleBreakpointTransition(_lastBreakpoint, currentBreakpoint);
            }

            _lastBreakpoint = currentBreakpoint;
            UpdateAmbientAudio();
        }

        private void HandleBreakpointCrossed(CarryBreakpoint previousBreakpoint, CarryBreakpoint currentBreakpoint)
        {
            AudioDriverOverride?.HandleBreakpointCrossed(previousBreakpoint, currentBreakpoint);

            if (HapticsOverride != null)
            {
                HapticsOverride.PlayBreakpointPulse(currentBreakpoint);
            }
            else if (Application.isMobilePlatform)
            {
                Handheld.Vibrate();
            }

            BreakpointCrossedUpward?.Invoke(currentBreakpoint);
        }

        private void HandleBreakpointTransition(CarryBreakpoint previousBreakpoint, CarryBreakpoint currentBreakpoint)
        {
            if (currentBreakpoint > previousBreakpoint)
            {
                HandleBreakpointCrossed(previousBreakpoint, currentBreakpoint);
                Phase1TelemetryService.Instance?.LogBreakpointCrossed(currentBreakpoint.ToString(), "Up", _playerController!.CarryState.CapacityFraction);
                return;
            }

            BreakpointCrossedDownward?.Invoke(currentBreakpoint);
            Phase1TelemetryService.Instance?.LogBreakpointCrossed(currentBreakpoint.ToString(), "Down", _playerController!.CarryState.CapacityFraction);
        }

        private void UpdateFootsteps()
        {
            if (_playerController == null || !_playerController.IsMoving || !_playerController.IsGrounded)
            {
                _footstepTimer = 0f;
                return;
            }

            _footstepTimer += Time.deltaTime;
            var stepInterval = _footstepIntervalBase / Mathf.Max(0.1f, _playerController.CurrentPenalty.MobilityMultiplier);
            if (_footstepTimer < stepInterval)
            {
                return;
            }

            _footstepTimer = 0f;
            var breakpoint = _playerController.CurrentBreakpoint;
            var capacityFraction = _playerController.CarryState.CapacityFraction;

            if (AudioDriverOverride != null)
            {
                AudioDriverOverride.PlayFootstep(breakpoint, capacityFraction);
            }
            else
            {
                PlayFootstepDefault(capacityFraction);
            }

            if (HapticsOverride != null)
            {
                HapticsOverride.PlayFootstepTap();
            }
            else if (Application.isMobilePlatform)
            {
                Handheld.Vibrate();
            }

            FootstepTriggered?.Invoke(breakpoint);
        }

        private void ApplyCameraFeedback(float normalizedFraction, float capacityFraction)
        {
            if (_noise != null)
            {
                _noise.m_AmplitudeGain = _maxSwayAmplitude * normalizedFraction;
            }

            if (_cameraRoot != null)
            {
                var offset = Vector3.down * (_maxCameraDrop * normalizedFraction);
                _cameraRoot.localPosition = _cameraRootLocalPosition + offset;
            }

            if (_virtualCamera == null)
            {
                return;
            }

            var overburdenedT = Mathf.InverseLerp(CarryState.OverburdenedThreshold, MaxCapacityFraction, capacityFraction);
            var lens = _virtualCamera.m_Lens;
            lens.FieldOfView = _baseFieldOfView - (_overburdenedFovReduction * overburdenedT);
            _virtualCamera.m_Lens = lens;
        }

        private void UpdateBreathAudio(float normalizedFraction)
        {
            var breathPitch = _playerController != null && _playerController.CurrentBreakpoint >= CarryBreakpoint.Overburdened
                ? _overburdenedBreathPitch
                : 1f;
            var volume = normalizedFraction * _maxBreathVolume;

            if (AudioDriverOverride != null)
            {
                AudioDriverOverride.UpdateBreath(volume, breathPitch);
                return;
            }

            if (_breathSource == null)
            {
                return;
            }

            _breathSource.loop = true;
            _breathSource.volume = volume;
            _breathSource.pitch = breathPitch;

            if (volume > 0.01f && !_breathSource.isPlaying && _breathSource.clip != null)
            {
                _breathSource.Play();
            }
            else if (volume <= 0.01f && _breathSource.isPlaying)
            {
                _breathSource.Pause();
            }
        }

        private void UpdateAmbientAudio()
        {
            if (_playerController == null)
            {
                return;
            }

            AudioClip? ambientClip = null;
            var items = _playerController.CarryState.Items;
            for (var i = 0; i < items.Count; i++)
            {
                if (items[i] is not IAmbientLoadoutItem ambientItem || ambientItem.AmbientSound == null)
                {
                    continue;
                }

                ambientClip = ambientItem.AmbientSound;
                break;
            }

            if (AudioDriverOverride != null)
            {
                AudioDriverOverride.UpdateAmbient(ambientClip);
                return;
            }

            if (_ambientItemSource == null)
            {
                return;
            }

            if (_ambientItemSource.clip != ambientClip)
            {
                _ambientItemSource.Stop();
                _ambientItemSource.clip = ambientClip;
            }

            if (ambientClip != null)
            {
                _ambientItemSource.loop = true;
                if (!_ambientItemSource.isPlaying)
                {
                    _ambientItemSource.Play();
                }
            }
        }

        private void PlayFootstepDefault(float capacityFraction)
        {
            var normalized = Mathf.Clamp01(capacityFraction / MaxCapacityFraction);
            var lightWeight = Mathf.Clamp01(1f - (normalized * 2f));
            var overburdenedWeight = Mathf.Clamp01((normalized - 0.66f) / 0.34f);
            var loadedWeight = Mathf.Clamp01(1f - Mathf.Abs((normalized - 0.5f) / 0.5f));

            PlayFootstepSource(_footstepLightSource, lightWeight);
            PlayFootstepSource(_footstepLoadedSource, loadedWeight);
            PlayFootstepSource(_footstepOverburdenedSource, overburdenedWeight);
        }

        private static void PlayFootstepSource(AudioSource? source, float volume)
        {
            if (source == null || source.clip == null || volume <= 0.001f)
            {
                return;
            }

            source.PlayOneShot(source.clip, volume);
        }

#if UNITY_EDITOR
        public void EditorConfigure(
            PlayerController playerController,
            Transform cameraRoot,
            CinemachineVirtualCamera virtualCamera,
            AudioSource footstepLightSource,
            AudioSource footstepLoadedSource,
            AudioSource footstepOverburdenedSource,
            AudioSource breathSource,
            AudioSource ambientItemSource)
        {
            _playerController = playerController;
            _cameraRoot = cameraRoot;
            _virtualCamera = virtualCamera;
            _footstepLightSource = footstepLightSource;
            _footstepLoadedSource = footstepLoadedSource;
            _footstepOverburdenedSource = footstepOverburdenedSource;
            _breathSource = breathSource;
            _ambientItemSource = ambientItemSource;
        }
#endif
    }
}
