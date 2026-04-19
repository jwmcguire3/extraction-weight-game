#nullable enable
using ExtractionWeight.Core;
using UnityEngine;

namespace ExtractionWeight.Threat
{
    [DisallowMultipleComponent]
    public sealed class Listener : ThreatBehaviourBase
    {
        private static readonly DetectionProfile DefaultProfile = new(0.95f, 0.05f, 15f, 15f, 22f);

        [Header("Listener")]
        [SerializeField]
        private float _chargeSpeed = 7f;

        [SerializeField]
        private float _returnSpeed = 3.5f;

        [SerializeField]
        private Transform? _headPivot;

        private Vector3 _restPosition;
        private Quaternion _restRotation;

        public bool IsResting => !_isPursuing && CurrentState == DetectionState.Unaware && Vector3.Distance(transform.position, _restPosition) <= 0.1f;

        protected override void Reset()
        {
            base.Reset();
            _profile = DefaultProfile;
            _rotationDegreesPerSecond = 300f;
        }

        protected override void Awake()
        {
            if (_profile.BaseDetectionRange <= 0f)
            {
                _profile = DefaultProfile;
            }

            _restPosition = transform.position;
            _restRotation = transform.rotation;
            EnsureVisuals();
            EnsureAudio();
            base.Awake();
        }

        protected override void TickState(DetectionState state, PlayerController player, float distanceToPlayer, float deltaTime)
        {
            switch (state)
            {
                case DetectionState.Unaware:
                    ReturnToRest(deltaTime);
                    break;
                case DetectionState.Suspicious:
                    ReactToNoise(player.transform.position, deltaTime);
                    break;
                case DetectionState.Detected:
                    MoveTowards(player.transform.position, _chargeSpeed, deltaTime);
                    break;
            }
        }

        private void ReturnToRest(float deltaTime)
        {
            transform.position = Vector3.MoveTowards(transform.position, _restPosition, _returnSpeed * deltaTime);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, _restRotation, _rotationDegreesPerSecond * deltaTime);

            if (_headPivot != null)
            {
                _headPivot.localRotation = Quaternion.RotateTowards(_headPivot.localRotation, Quaternion.identity, 220f * deltaTime);
            }
        }

        private void ReactToNoise(Vector3 playerPosition, float deltaTime)
        {
            FaceTowards(playerPosition, deltaTime);
            if (_headPivot == null)
            {
                return;
            }

            var twitch = Mathf.Sin(Time.time * 10f) * 16f;
            var tilt = Mathf.Sin(Time.time * 3.5f) * 10f;
            _headPivot.localRotation = Quaternion.Euler(tilt, twitch, -tilt * 0.4f);
        }

        private void EnsureAudio()
        {
            var audioSources = GetComponents<AudioSource>();
            _idleAudioSource = audioSources.Length > 0 ? audioSources[0] : gameObject.AddComponent<AudioSource>();
            _alertAudioSource = audioSources.Length > 1 ? audioSources[1] : gameObject.AddComponent<AudioSource>();

            if (_idleAudioSource.clip == null)
            {
                _idleAudioSource.clip = ThreatAudioFactory.CreateShuffleLoop($"{name}_Shuffle");
                _idleAudioSource.volume = 0.14f;
            }

            if (_alertAudioSource.clip == null)
            {
                _alertAudioSource.clip = ThreatAudioFactory.CreateChargeCue($"{name}_Charge");
                _alertAudioSource.volume = 0.95f;
            }
        }

        private void EnsureVisuals()
        {
            if (_headPivot != null)
            {
                return;
            }

            var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.name = "Body";
            body.transform.SetParent(transform, false);
            body.transform.localScale = new Vector3(0.9f, 0.75f, 0.9f);
            body.transform.localPosition = new Vector3(0f, 0.75f, 0f);
            var bodyCollider = body.GetComponent<Collider>();
            if (bodyCollider != null)
            {
                DestroyImmediate(bodyCollider);
            }

            var bodyRenderer = body.GetComponent<MeshRenderer>();
            if (bodyRenderer != null)
            {
                bodyRenderer.sharedMaterial = CreateCreatureMaterial(new Color(0.25f, 0.27f, 0.22f, 1f));
            }

            var head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            head.name = "Head";
            head.transform.SetParent(body.transform, false);
            head.transform.localScale = Vector3.one * 0.65f;
            head.transform.localPosition = new Vector3(0f, 0.6f, 0.35f);
            var headCollider = head.GetComponent<Collider>();
            if (headCollider != null)
            {
                DestroyImmediate(headCollider);
            }

            var headRenderer = head.GetComponent<MeshRenderer>();
            if (headRenderer != null)
            {
                headRenderer.sharedMaterial = CreateCreatureMaterial(new Color(0.5f, 0.48f, 0.4f, 1f));
            }

            _headPivot = head.transform;
        }

        private static Material CreateCreatureMaterial(Color color)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            return new Material(shader)
            {
                color = color,
            };
        }
    }
}
