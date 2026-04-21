#nullable enable
using ExtractionWeight.Core;
using UnityEngine;

namespace ExtractionWeight.Threat
{
    [DisallowMultipleComponent]
    public sealed class Warden : ThreatBehaviourBase
    {
        private static readonly DetectionProfile DefaultProfile = new(0.1f, 0.9f, 25f, 25f, 38f);

        [Header("Warden")]
        [SerializeField]
        private Transform[] _waypoints = System.Array.Empty<Transform>();

        [SerializeField]
        private float _patrolSpeed = 2.5f;

        [SerializeField]
        private float _pursuitSpeed = 5f;

        [SerializeField]
        private MeshRenderer? _sightConeRenderer;

        [SerializeField]
        private Transform? _visualRoot;

        private int _currentWaypointIndex;

        public bool IsReturningToPatrol => !_isPursuing && CurrentState == DetectionState.Unaware;

        protected override float GiveUpDelaySeconds => 5f;

        protected override float ContactDamagePerSecond => 15f;

        protected override float ContactDamageRange => 3f;

        protected override void Reset()
        {
            base.Reset();
            _profile = DefaultProfile;
            _rotationDegreesPerSecond = 240f;
        }

        protected override void Awake()
        {
            if (_profile.BaseDetectionRange <= 0f)
            {
                _profile = DefaultProfile;
            }

            EnsureVisuals();
            EnsureAudio();
            base.Awake();
        }

        protected override void TickState(DetectionState state, PlayerController player, float distanceToPlayer, float deltaTime)
        {
            switch (state)
            {
                case DetectionState.Unaware:
                    Patrol(deltaTime);
                    break;
                case DetectionState.Suspicious:
                    FaceTowards(player.transform.position, deltaTime);
                    break;
                case DetectionState.Detected:
                    MoveTowards(player.transform.position, _pursuitSpeed, deltaTime);
                    break;
            }

            UpdateSightCone(state);
            BobVisuals(deltaTime);
        }

        private void Patrol(float deltaTime)
        {
            if (_waypoints.Length == 0)
            {
                return;
            }

            var targetWaypoint = _waypoints[_currentWaypointIndex];
            if (targetWaypoint == null)
            {
                return;
            }

            MoveTowards(targetWaypoint.position, _patrolSpeed, deltaTime);
            if (Vector3.Distance(transform.position, targetWaypoint.position) <= 0.25f)
            {
                _currentWaypointIndex = (_currentWaypointIndex + 1) % _waypoints.Length;
            }
        }

        private void UpdateSightCone(DetectionState state)
        {
            if (_sightConeRenderer == null)
            {
                return;
            }

            var material = _sightConeRenderer.material;
            var color = state switch
            {
                DetectionState.Detected => new Color(1f, 0.3f, 0.25f, 0.22f),
                DetectionState.Suspicious => new Color(1f, 0.8f, 0.25f, 0.16f),
                _ => new Color(0.4f, 0.9f, 1f, 0.1f),
            };
            material.color = color;
        }

        private void BobVisuals(float deltaTime)
        {
            if (_visualRoot == null)
            {
                return;
            }

            var localPosition = _visualRoot.localPosition;
            localPosition.y = 1.4f + (Mathf.Sin(Time.time * 2f) * 0.08f);
            _visualRoot.localPosition = localPosition;
            _visualRoot.Rotate(Vector3.up, 45f * deltaTime, Space.Self);
        }

        private void EnsureAudio()
        {
            var audioSources = GetComponents<AudioSource>();
            _idleAudioSource = audioSources.Length > 0 ? audioSources[0] : gameObject.AddComponent<AudioSource>();
            _alertAudioSource = audioSources.Length > 1 ? audioSources[1] : gameObject.AddComponent<AudioSource>();

            if (_idleAudioSource.clip == null)
            {
                _idleAudioSource.clip = ThreatAudioFactory.CreateMechanicalHum($"{name}_Hum");
                _idleAudioSource.volume = 0.18f;
            }

            if (_alertAudioSource.clip == null)
            {
                _alertAudioSource.clip = ThreatAudioFactory.CreateMechanicalAlert($"{name}_Alert");
                _alertAudioSource.volume = 0.85f;
            }
        }

        private void EnsureVisuals()
        {
            if (_visualRoot == null)
            {
                var visual = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                visual.name = "OrbVisual";
                visual.transform.SetParent(transform, false);
                visual.transform.localScale = Vector3.one * 1.2f;
                visual.transform.localPosition = new Vector3(0f, 1.4f, 0f);
                var collider = visual.GetComponent<Collider>();
                if (collider != null)
                {
                    DestroyImmediate(collider);
                }

                var renderer = visual.GetComponent<MeshRenderer>();
                if (renderer != null)
                {
                    renderer.sharedMaterial = CreateTransparentMaterial(new Color(0.45f, 0.9f, 1f, 0.9f));
                }

                _visualRoot = visual.transform;
            }

            if (_sightConeRenderer != null)
            {
                return;
            }

            var coneObject = new GameObject("SightCone", typeof(MeshFilter), typeof(MeshRenderer));
            coneObject.transform.SetParent(transform, false);
            coneObject.transform.localPosition = new Vector3(0f, 0.05f, 0f);
            coneObject.transform.localRotation = Quaternion.identity;

            var meshFilter = coneObject.GetComponent<MeshFilter>();
            meshFilter.sharedMesh = BuildConeMesh(40f, 8f, 18);

            _sightConeRenderer = coneObject.GetComponent<MeshRenderer>();
            _sightConeRenderer.sharedMaterial = CreateTransparentMaterial(new Color(0.4f, 0.9f, 1f, 0.1f));
        }

        private static Mesh BuildConeMesh(float angleDegrees, float length, int segments)
        {
            var mesh = new Mesh
            {
                name = "WardenSightCone",
            };

            var vertices = new Vector3[segments + 2];
            var triangles = new int[segments * 3];
            vertices[0] = Vector3.zero;

            var halfAngle = angleDegrees * 0.5f;
            for (var i = 0; i <= segments; i++)
            {
                var t = i / (float)segments;
                var angle = Mathf.Lerp(-halfAngle, halfAngle, t) * Mathf.Deg2Rad;
                vertices[i + 1] = new Vector3(Mathf.Sin(angle) * length, 0f, Mathf.Cos(angle) * length);
            }

            for (var i = 0; i < segments; i++)
            {
                var triangleIndex = i * 3;
                triangles[triangleIndex] = 0;
                triangles[triangleIndex + 1] = i + 1;
                triangles[triangleIndex + 2] = i + 2;
            }

            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.RecalculateBounds();
            mesh.RecalculateNormals();
            return mesh;
        }

        private static Material CreateTransparentMaterial(Color color)
        {
            var shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Standard");
            var material = new Material(shader)
            {
                color = color,
            };

            material.SetFloat("_Surface", 1f);
            material.SetFloat("_Blend", 0f);
            material.renderQueue = 3000;
            return material;
        }

#if UNITY_EDITOR
        public void EditorSetWaypoints(Transform[] waypoints)
        {
            _waypoints = waypoints;
        }
#endif
    }
}
