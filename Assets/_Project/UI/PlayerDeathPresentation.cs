#nullable enable
using System.Collections;
using Cinemachine;
using ExtractionWeight.Core;
using UnityEngine;

namespace ExtractionWeight.UI
{
    [DisallowMultipleComponent]
    public sealed class PlayerDeathPresentation : MonoBehaviour, IPlayerDeathPresentation
    {
        private const float GreyFadeSeconds = 2f;
        private const float BlackFadeSeconds = 0.6f;

        [SerializeField]
        private PlayerHealth? _playerHealth;

        [SerializeField]
        private Transform? _cameraRoot;

        [SerializeField]
        private Camera? _mainCamera;

        [SerializeField]
        private CinemachineBrain? _cameraBrain;

        [SerializeField]
        private CinemachineVirtualCamera? _virtualCamera;

        [SerializeField]
        private MobileUIHUD? _mobileHud;

        private Coroutine? _sequenceRoutine;

        public bool IsPlaying => _sequenceRoutine != null;

        private void Awake()
        {
            _playerHealth ??= GetComponent<PlayerHealth>();
            _cameraRoot ??= transform.Find("CameraRoot");
            _mainCamera ??= GetComponentInChildren<Camera>(true);
            _cameraBrain ??= _mainCamera != null ? _mainCamera.GetComponent<CinemachineBrain>() : null;
            _virtualCamera ??= GetComponentInChildren<CinemachineVirtualCamera>(true);
            _mobileHud ??= GetComponentInChildren<MobileUIHUD>(true);
        }

        public float GetPresentationDurationSeconds()
        {
            return GreyFadeSeconds + BlackFadeSeconds;
        }

        public void Play(float lostLootValue)
        {
            if (_sequenceRoutine != null)
            {
                return;
            }

            _sequenceRoutine = StartCoroutine(PlaySequence(lostLootValue));
        }

        private IEnumerator PlaySequence(float lostLootValue)
        {
            DetachCamera();

            var elapsed = 0f;
            while (elapsed < GreyFadeSeconds)
            {
                elapsed += Time.deltaTime;
                var t = Mathf.Clamp01(elapsed / GreyFadeSeconds);
                _mobileHud?.SetDeathOverlay(new Color(0.5f, 0.5f, 0.5f, t * 0.92f), lostLootValue);
                yield return null;
            }

            elapsed = 0f;
            while (elapsed < BlackFadeSeconds)
            {
                elapsed += Time.deltaTime;
                var t = Mathf.Clamp01(elapsed / BlackFadeSeconds);
                _mobileHud?.SetDeathOverlay(Color.Lerp(new Color(0.5f, 0.5f, 0.5f, 0.92f), new Color(0f, 0f, 0f, 1f), t), lostLootValue);
                yield return null;
            }

            _mobileHud?.SetDeathOverlay(new Color(0f, 0f, 0f, 1f), lostLootValue);
            _sequenceRoutine = null;
        }

        private void DetachCamera()
        {
            if (_mainCamera == null)
            {
                return;
            }

            var worldPosition = _cameraRoot != null ? _cameraRoot.position : _mainCamera.transform.position;
            var worldRotation = _cameraRoot != null ? _cameraRoot.rotation : _mainCamera.transform.rotation;

            if (_virtualCamera != null)
            {
                _virtualCamera.enabled = false;
            }

            if (_cameraBrain != null)
            {
                _cameraBrain.enabled = false;
            }

            _mainCamera.transform.SetParent(null, true);
            _mainCamera.transform.position = worldPosition;
            _mainCamera.transform.rotation = worldRotation;
        }

#if UNITY_EDITOR
        public void EditorConfigure(
            PlayerHealth playerHealth,
            Transform cameraRoot,
            Camera mainCamera,
            CinemachineBrain cameraBrain,
            CinemachineVirtualCamera virtualCamera,
            MobileUIHUD mobileHud)
        {
            _playerHealth = playerHealth;
            _cameraRoot = cameraRoot;
            _mainCamera = mainCamera;
            _cameraBrain = cameraBrain;
            _virtualCamera = virtualCamera;
            _mobileHud = mobileHud;
        }
#endif
    }
}
