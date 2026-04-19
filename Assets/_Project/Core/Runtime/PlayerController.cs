#nullable enable
using System;
using ExtractionWeight.Weight;
using UnityEngine;
using UnityEngine.InputSystem;
using WeightCarryState = ExtractionWeight.Weight.CarryState;

namespace ExtractionWeight.Core
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CharacterController))]
    public sealed class PlayerController : MonoBehaviour, IPlayerCarryInteractor, IPickupInteractionSink
    {
        private const float DefaultCarryCapacity = 1f;
        public const float MaxCapacityFraction = WeightCarryState.MaxCapacityFraction;
        private const float ControllerStandingHeight = 1.8f;
        private const float ControllerStandingCenterY = 0.9f;
        private const float ControllerCrouchedHeight = 1.2f;
        private const float ControllerCrouchedCenterY = 0.6f;
        private const float MinimumMoveMagnitude = 0.0001f;
        private const int MaxActionColliders = 16;

        [Header("References")]
        [SerializeField]
        private CharacterController? _characterController;

        [SerializeField]
        private InputActionAsset? _playerControls;

        [SerializeField]
        private Transform? _movementReference;

        [SerializeField]
        private InteractionTracker? _interactionTracker;

        [Header("Movement")]
        [Min(0f)]
        [SerializeField]
        private float _walkSpeed = 4f;

        [Min(0f)]
        [SerializeField]
        private float _sprintSpeed = 7f;

        [Range(0.1f, 1f)]
        [SerializeField]
        private float _crouchSpeedMultiplier = 0.65f;

        [Min(0f)]
        [SerializeField]
        private float _gravity = 30f;

        [Range(0f, 1f)]
        [SerializeField]
        private float _autoSprintThreshold = 0.9f;

        [Range(0f, 1f)]
        [SerializeField]
        private float _inputDeadzone = 0.1f;

        [Header("Carry")]
        [Min(0.1f)]
        [SerializeField]
        private float _carryCapacity = DefaultCarryCapacity;

        [SerializeField]
        private ZoneAxisWeights _zoneAxisWeights = default;

        [Header("Stamina")]
        [Min(1f)]
        [SerializeField]
        private float _maxStamina = 100f;

        [Min(0f)]
        [SerializeField]
        private float _baseSprintDrainPerSecond = 18f;

        [Min(0f)]
        [SerializeField]
        private float _sprintDrainCarryScale = 1f;

        [Min(0f)]
        [SerializeField]
        private float _staminaRegenPerSecond = 16f;

        [Header("Context")]
        [Min(0f)]
        [SerializeField]
        private float _contextQueryRadius = 2.5f;

        [SerializeField]
        private LayerMask _contextQueryMask = ~0;

        [Min(0f)]
        [SerializeField]
        private float _initialTideSecondsRemaining = 150f;

        private readonly Collider[] _actionResults = new Collider[MaxActionColliders];

        private WeightCarryState? _carryState;
        private AppliedPenalty _currentPenalty;
        private InputAction? _moveAction;
        private InputAction? _sprintAction;
        private InputAction? _contextAction;
        private InputAction? _crouchAction;
        private Vector2 _uiMoveInput;
        private bool _uiActionQueued;
        private bool _uiActionHeld;
        private bool _uiSprintHeld;
        private bool _uiCrouchQueued;
        private bool _useUiInput;
        private float _verticalVelocity;
        private float _currentSpeed;
        private float _currentTideSecondsRemaining;
        private bool _previousCrouchPressed;
        private bool _previousActionPressed;
        private InputState _cachedInputState;

        public float WalkSpeed => _walkSpeed;

        public float SprintSpeed => _sprintSpeed;

        public float CurrentSpeed => _currentSpeed;

        public float CurrentStamina { get; private set; }

        public float MaxStamina => _maxStamina;

        public WeightCarryState CarryState => _carryState ??= new WeightCarryState(_carryCapacity);

        public AppliedPenalty CurrentPenalty => _currentPenalty;

        public float CapacityFraction => CarryState.CapacityFraction;

        public bool IsSprinting { get; private set; }

        public bool IsCrouched { get; private set; }

        public bool IsMoving { get; private set; }

        public bool IsGrounded => _characterController != null && _characterController.isGrounded;

        public CarryBreakpoint CurrentBreakpoint => CarryState.CurrentBreakpoint;

        public int CurrentBreakpointIndex => (int)CurrentBreakpoint;

        public bool CanClimb => CurrentBreakpoint != CarryBreakpoint.SoftCeiling;

        public ContextActionKind CurrentContextActionKind { get; private set; }

        public string CurrentContextActionLabel { get; private set; } = string.Empty;

        public float CurrentContextActionProgress { get; private set; }

        public string CurrentHudMessage => _interactionTracker?.HudMessage ?? string.Empty;

        public float CurrentTideSecondsRemaining => _currentTideSecondsRemaining;

        public event Action<PlayerContextActionTarget?>? ContextTargetChanged;

        public PlayerContextActionTarget? CurrentContextTarget { get; private set; }

        public float CurrentHandlingMultiplier => _currentPenalty.HandlingMultiplier;

        private void Awake()
        {
            _characterController ??= GetComponent<CharacterController>();
            _movementReference ??= transform;
            _interactionTracker ??= GetComponent<InteractionTracker>();
            _interactionTracker ??= gameObject.AddComponent<InteractionTracker>();

            if (_zoneAxisWeights.Sum <= 0f)
            {
                _zoneAxisWeights = ZoneAxisWeights.Uniform;
            }

            _ = CarryState;
            CurrentStamina = _maxStamina;
            _currentPenalty = WeightPenaltyCalculator.Compute(CarryState, _zoneAxisWeights);
            _currentTideSecondsRemaining = _initialTideSecondsRemaining;

            ApplyControllerShape();
            BindActions();
        }

        private void OnEnable()
        {
            EnableActions();
        }

        private void OnDisable()
        {
            DisableActions();
        }

        private void Update()
        {
            CarryState.TickAmbientEffects(Time.deltaTime);
            _currentPenalty = WeightPenaltyCalculator.Compute(CarryState, _zoneAxisWeights);
            _currentTideSecondsRemaining = Mathf.Max(0f, _currentTideSecondsRemaining - Time.deltaTime);

            var inputState = ReadInputState();
            _cachedInputState = inputState;
            HandleCrouchToggle(inputState.CrouchPressed);
            _interactionTracker?.Tick(this, inputState.ActionHeld, Time.deltaTime);
            UpdateContextState();

            if ((_interactionTracker == null || !_interactionTracker.HasPickupCandidate) &&
                inputState.ActionHeld &&
                !_previousActionPressed)
            {
                TriggerContextAction();
            }

            _previousActionPressed = inputState.ActionHeld;
        }

        private void FixedUpdate()
        {
            var moveInput = ApplyDeadzone(_cachedInputState.Move);
            var requestedSprint = _cachedInputState.SprintHeld || moveInput.magnitude >= _autoSprintThreshold;
            var canSprint = CanSprint(CurrentBreakpoint, IsCrouched, CurrentStamina);

            IsSprinting = requestedSprint && canSprint && moveInput.sqrMagnitude > MinimumMoveMagnitude;
            CurrentStamina = UpdateStamina(
                CurrentStamina,
                Time.fixedDeltaTime,
                IsSprinting,
                GetSprintDrainPerSecond(_baseSprintDrainPerSecond, CarryState.CapacityFraction, _sprintDrainCarryScale),
                _staminaRegenPerSecond,
                _maxStamina);

            var targetSpeed = CalculateSpeed(
                _walkSpeed,
                _sprintSpeed,
                _currentPenalty.MobilityMultiplier,
                IsSprinting,
                IsCrouched,
                _crouchSpeedMultiplier);

            Move(moveInput, targetSpeed, Time.fixedDeltaTime);
        }

        public void SetUiMoveInput(Vector2 moveInput)
        {
            _useUiInput = true;
            _uiMoveInput = Vector2.ClampMagnitude(moveInput, 1f);
        }

        public void SetUiSprintHeld(bool isHeld)
        {
            _useUiInput = true;
            _uiSprintHeld = isHeld;
        }

        public void QueueUiContextAction()
        {
            _useUiInput = true;
            _uiActionQueued = true;
        }

        public void SetUiContextActionHeld(bool isHeld)
        {
            _useUiInput = true;
            _uiActionHeld = isHeld;
        }

        public void QueueUiCrouchToggle()
        {
            _useUiInput = true;
            _uiCrouchQueued = true;
        }

        public void SetTideSecondsRemaining(float secondsRemaining)
        {
            _currentTideSecondsRemaining = Mathf.Max(0f, secondsRemaining);
        }

        public static float CalculateSpeed(
            float walkSpeed,
            float sprintSpeed,
            float mobilityMultiplier,
            bool isSprinting,
            bool isCrouched,
            float crouchSpeedMultiplier)
        {
            var baseSpeed = isSprinting ? sprintSpeed : walkSpeed;
            var crouchFactor = isCrouched ? crouchSpeedMultiplier : 1f;
            return baseSpeed * mobilityMultiplier * crouchFactor;
        }

        public static float GetSprintDrainPerSecond(float baseDrainPerSecond, float capacityFraction, float carryScale)
        {
            return baseDrainPerSecond * (1f + (capacityFraction * carryScale));
        }

        public static float UpdateStamina(
            float currentStamina,
            float deltaTime,
            bool isSprinting,
            float sprintDrainPerSecond,
            float regenPerSecond,
            float maxStamina)
        {
            var nextStamina = isSprinting
                ? currentStamina - (sprintDrainPerSecond * deltaTime)
                : currentStamina + (regenPerSecond * deltaTime);
            return Mathf.Clamp(nextStamina, 0f, maxStamina);
        }

        public static bool CanSprint(CarryBreakpoint breakpoint, bool isCrouched, float stamina)
        {
            return breakpoint < CarryBreakpoint.Overburdened && !isCrouched && stamina > 0f;
        }

        public bool TryAddCarryItem(ILoadoutItem item)
        {
            return CarryState.TryAdd(item);
        }

        public void AttachAmbientEffect(IAmbientEffect effect)
        {
            CarryState.AttachAmbientEffect(effect);
        }

        public void RegisterPickupCandidate(IPickupInteractable pickup)
        {
            _interactionTracker?.RegisterPickupCandidate(pickup);
        }

        public void UnregisterPickupCandidate(IPickupInteractable pickup)
        {
            _interactionTracker?.UnregisterPickupCandidate(pickup);
        }

        public void DebugApplyMobilityLoad(float capacityFraction)
        {
            CarryState.Clear();
            var remaining = Mathf.Clamp(capacityFraction, 0f, WeightCarryState.MaxCapacityFraction);
            var itemIndex = 0;

            while (remaining > 0.0001f)
            {
                var chunk = Mathf.Min(remaining, 1f);
                CarryState.TryAdd(new SyntheticLoadoutItem($"debug-{itemIndex}", new CostSignature(0f, 0f, 0f, chunk)));
                remaining -= chunk;
                itemIndex++;
            }

            _currentPenalty = WeightPenaltyCalculator.Compute(CarryState, _zoneAxisWeights);
        }

        private void Move(Vector2 moveInput, float targetSpeed, float deltaTime)
        {
            if (_characterController == null)
            {
                return;
            }

            var reference = _movementReference != null ? _movementReference : transform;
            var right = reference.right;
            var forward = reference.forward;
            right.y = 0f;
            forward.y = 0f;
            right.Normalize();
            forward.Normalize();

            var planarDirection = (right * moveInput.x) + (forward * moveInput.y);
            if (planarDirection.sqrMagnitude > 1f)
            {
                planarDirection.Normalize();
            }

            IsMoving = planarDirection.sqrMagnitude > MinimumMoveMagnitude;
            _currentSpeed = targetSpeed;

            if (_characterController.isGrounded && _verticalVelocity < 0f)
            {
                _verticalVelocity = -2f;
            }

            _verticalVelocity -= _gravity * deltaTime;

            var velocity = (planarDirection * targetSpeed) + (Vector3.up * _verticalVelocity);
            _characterController.Move(velocity * deltaTime);
        }

        private void TriggerContextAction()
        {
            CurrentContextTarget?.Execute(this);
        }

        private void HandleCrouchToggle(bool crouchPressed)
        {
            if (crouchPressed && !_previousCrouchPressed)
            {
                IsCrouched = !IsCrouched;
                ApplyControllerShape();
            }

            _previousCrouchPressed = crouchPressed;
        }

        private void ApplyControllerShape()
        {
            if (_characterController == null)
            {
                return;
            }

            _characterController.height = IsCrouched ? ControllerCrouchedHeight : ControllerStandingHeight;
            _characterController.center = new Vector3(0f, IsCrouched ? ControllerCrouchedCenterY : ControllerStandingCenterY, 0f);
        }

        private InputState ReadInputState()
        {
            var inputState = new InputState
            {
                Move = _moveAction?.ReadValue<Vector2>() ?? Vector2.zero,
                SprintHeld = (_sprintAction?.IsPressed() ?? false),
                ActionHeld = (_contextAction?.IsPressed() ?? false),
                CrouchPressed = (_crouchAction?.IsPressed() ?? false),
            };

            if (!_useUiInput)
            {
                return inputState;
            }

            inputState.Move = _uiMoveInput;
            inputState.SprintHeld |= _uiSprintHeld;
            inputState.ActionHeld |= _uiActionHeld || _uiActionQueued;
            inputState.CrouchPressed |= _uiCrouchQueued;

            _uiActionQueued = false;
            _uiCrouchQueued = false;

            return inputState;
        }

        private void UpdateContextState()
        {
            if (_interactionTracker != null && _interactionTracker.HasPickupCandidate)
            {
                CurrentContextTarget = null;
                CurrentContextActionKind = ContextActionKind.Pickup;
                CurrentContextActionLabel = _interactionTracker.ActionLabel;
                CurrentContextActionProgress = _interactionTracker.HoldProgress;
                return;
            }

            CurrentContextActionProgress = 0f;
            UpdateContextTarget();
        }

        private void UpdateContextTarget()
        {
            var origin = transform.position + Vector3.up;
            var hitCount = Physics.OverlapSphereNonAlloc(
                origin,
                _contextQueryRadius,
                _actionResults,
                _contextQueryMask,
                QueryTriggerInteraction.Collide);

            PlayerContextActionTarget? bestTarget = null;
            var bestScore = float.NegativeInfinity;

            for (var i = 0; i < hitCount; i++)
            {
                var collider = _actionResults[i];
                if (collider == null)
                {
                    continue;
                }

                var candidate = collider.GetComponentInParent<PlayerContextActionTarget>();
                if (candidate == null || !candidate.IsEnabled)
                {
                    continue;
                }

                var distance = Vector3.Distance(transform.position, candidate.transform.position);
                if (distance > candidate.InteractionRadius)
                {
                    continue;
                }

                var score = candidate.Priority - distance;
                if (score <= bestScore)
                {
                    continue;
                }

                bestTarget = candidate;
                bestScore = score;
            }

            if (ReferenceEquals(bestTarget, CurrentContextTarget))
            {
                return;
            }

            CurrentContextTarget = bestTarget;
            CurrentContextActionKind = bestTarget?.ActionKind ?? ContextActionKind.None;
            CurrentContextActionLabel = bestTarget?.Label ?? string.Empty;
            ContextTargetChanged?.Invoke(CurrentContextTarget);
        }

        private void BindActions()
        {
            if (_playerControls == null)
            {
                return;
            }

            var actionMap = _playerControls.FindActionMap("MobileTouch", throwIfNotFound: false);
            if (actionMap == null)
            {
                return;
            }

            _moveAction = actionMap.FindAction("Move", throwIfNotFound: false);
            _sprintAction = actionMap.FindAction("Sprint", throwIfNotFound: false);
            _contextAction = actionMap.FindAction("ContextAction", throwIfNotFound: false);
            _crouchAction = actionMap.FindAction("Crouch", throwIfNotFound: false);
        }

        private void EnableActions()
        {
            _moveAction?.Enable();
            _sprintAction?.Enable();
            _contextAction?.Enable();
            _crouchAction?.Enable();
        }

        private void DisableActions()
        {
            _moveAction?.Disable();
            _sprintAction?.Disable();
            _contextAction?.Disable();
            _crouchAction?.Disable();
        }

        private Vector2 ApplyDeadzone(Vector2 moveInput)
        {
            return moveInput.magnitude < _inputDeadzone ? Vector2.zero : Vector2.ClampMagnitude(moveInput, 1f);
        }

        private struct InputState
        {
            public Vector2 Move { get; set; }
            public bool SprintHeld { get; set; }
            public bool ActionHeld { get; set; }
            public bool CrouchPressed { get; set; }
        }

        private sealed class SyntheticLoadoutItem : ILoadoutItem
        {
            public SyntheticLoadoutItem(string itemId, CostSignature baseCost)
            {
                ItemId = itemId;
                BaseCost = baseCost;
            }

            public string ItemId { get; }

            public CostSignature BaseCost { get; }

            public float Value => 0f;

            public bool IsVolatile => false;
        }

#if UNITY_EDITOR
        public void EditorConfigure(CharacterController characterController, InputActionAsset playerControls, Transform movementReference)
        {
            _characterController = characterController;
            _playerControls = playerControls;
            _movementReference = movementReference;
        }
#endif
    }
}
