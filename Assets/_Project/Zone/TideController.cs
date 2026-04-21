#nullable enable
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ExtractionWeight.Zone
{
    [DisallowMultipleComponent]
    public sealed class TideController : MonoBehaviour
    {
        private const float MinimumEdgeThickness = 0.6f;
        private const float MinimumSegmentSize = 0.01f;
        private static readonly int BaseColorProperty = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorProperty = Shader.PropertyToID("_Color");

        [SerializeField]
        private ZoneRuntime? _zoneRuntime;

        [SerializeField]
        private Transform? _coreTransform;

        [SerializeField]
        private Vector3 _corePosition = Vector3.zero;

        [SerializeField]
        private bool _hasExplicitCorePosition;

        [SerializeField]
        private bool _useAutoBounds = true;

        [SerializeField]
        private Bounds _tideBounds = new(Vector3.zero, new Vector3(196f, 8f, 196f));

        [SerializeField]
        private float _maxFogHeight = 2f;

        [SerializeField]
        private Color _fogColor = new(0.76f, 0.82f, 0.87f, 0.72f);

        [SerializeField]
        private Color _edgeColor = new(0.9f, 0.96f, 1f, 0.95f);

        [SerializeField]
        private float _maxRumbleVolume = 0.18f;

        [SerializeField]
        private float _rumblePitch = 0.92f;

        private readonly List<MeshRenderer> _fogRenderers = new(4);
        private readonly List<Transform> _edgeSegments = new(4);
        private readonly List<Transform> _fogSegments = new(4);

        private ZoneDefinition? _zoneDefinition;
        private AudioSource? _rumbleSource;
        private Material? _fogMaterial;
        private Material? _edgeMaterial;
        private float _elapsedSeconds;
        private bool _started;

        public float DurationSeconds => _zoneDefinition?.TideDurationSeconds ?? 0f;

        public float ElapsedSeconds => _elapsedSeconds;

        public float Progress { get; private set; }

        public float CurrentFogHeight => Mathf.Lerp(0f, _maxFogHeight, Progress);

        public bool IsInitialized => _zoneDefinition != null;

        public IReadOnlyList<MeshRenderer> FogRenderers => _fogRenderers;

        public Vector2 CurrentSafeMin { get; private set; }

        public Vector2 CurrentSafeMax { get; private set; }

        private void Awake()
        {
            _zoneRuntime ??= FindAnyObjectByType<ZoneRuntime>();
            EnsureVisuals();
            EnsureAudio();
        }

        private void Update()
        {
            _zoneRuntime ??= FindAnyObjectByType<ZoneRuntime>();
            if (_zoneDefinition == null && _zoneRuntime?.CurrentZoneDefinition != null)
            {
                Initialize(_zoneRuntime.CurrentZoneDefinition);
            }

            if (!_started || _zoneDefinition == null)
            {
                return;
            }

            Advance(Time.deltaTime);
        }

        public void Initialize(ZoneDefinition zoneDefinition)
        {
            _zoneDefinition = zoneDefinition;
            _started = true;
            _elapsedSeconds = 0f;
            Progress = 0f;
            ResolveBounds();
            UpdatePresentation();
            _zoneRuntime?.SetElapsedRunSeconds(0f, force: true);
        }

        public void Advance(float deltaTime)
        {
            if (_zoneDefinition == null)
            {
                return;
            }

            var duration = Mathf.Max(_zoneDefinition.TideDurationSeconds, 0.0001f);
            _elapsedSeconds = Mathf.Clamp(_elapsedSeconds + Mathf.Max(0f, deltaTime), 0f, duration);
            Progress = _zoneDefinition.TideDurationSeconds <= 0f
                ? 1f
                : Mathf.Clamp01(_elapsedSeconds / duration);
            UpdatePresentation();
            _zoneRuntime?.SetElapsedRunSeconds(_elapsedSeconds);
        }

        public bool IsPointInsideTide(Vector3 worldPosition)
        {
            return worldPosition.x < CurrentSafeMin.x ||
                   worldPosition.x > CurrentSafeMax.x ||
                   worldPosition.z < CurrentSafeMin.y ||
                   worldPosition.z > CurrentSafeMax.y;
        }

        public float GetClosePercent(ExtractionPointData pointData)
        {
            if (_zoneDefinition == null || _zoneDefinition.TideDurationSeconds <= 0f)
            {
                return 1f;
            }

            return Mathf.Clamp01(pointData.TideCloseTime / _zoneDefinition.TideDurationSeconds);
        }

        public bool IsExtractionClosed(ExtractionPointData pointData)
        {
            return Progress >= GetClosePercent(pointData);
        }

#if UNITY_EDITOR
        public void EditorConfigure(ZoneRuntime zoneRuntime, Bounds tideBounds, Vector3 corePosition)
        {
            _zoneRuntime = zoneRuntime;
            _useAutoBounds = false;
            _tideBounds = tideBounds;
            _corePosition = corePosition;
            _hasExplicitCorePosition = true;
        }
#endif

        private void ResolveBounds()
        {
            if (_useAutoBounds && TryBuildBoundsFromScene(out var sceneBounds))
            {
                _tideBounds = sceneBounds;
            }

            if (_coreTransform != null)
            {
                _corePosition = _coreTransform.position;
            }
            else if (_hasExplicitCorePosition)
            {
                return;
            }
            else if (_zoneDefinition != null && _zoneDefinition.ExtractionPoints.Count > 0)
            {
                var coreAccumulator = Vector3.zero;
                for (var i = 0; i < _zoneDefinition.ExtractionPoints.Count; i++)
                {
                    coreAccumulator += _zoneDefinition.ExtractionPoints[i].Position;
                }

                _corePosition = coreAccumulator / _zoneDefinition.ExtractionPoints.Count;
            }
            else
            {
                _corePosition = _tideBounds.center;
            }
        }

        private void UpdatePresentation()
        {
            EnsureVisuals();
            EnsureAudio();

            var boundsMin = new Vector2(_tideBounds.min.x, _tideBounds.min.z);
            var boundsMax = new Vector2(_tideBounds.max.x, _tideBounds.max.z);
            var target = new Vector2(_corePosition.x, _corePosition.z);
            CurrentSafeMin = Vector2.Lerp(boundsMin, target, Progress);
            CurrentSafeMax = Vector2.Lerp(boundsMax, target, Progress);

            var height = CurrentFogHeight;
            UpdateFogSegments(boundsMin, boundsMax, GetPhase1FogHeight(height));
            UpdateRumble();
        }

        private void UpdateFogSegments(Vector2 boundsMin, Vector2 boundsMax, float height)
        {
            if (_fogSegments.Count < 4 || _edgeSegments.Count < 4)
            {
                return;
            }

            var safeWidth = Mathf.Max(0f, CurrentSafeMax.x - CurrentSafeMin.x);
            var safeDepth = Mathf.Max(0f, CurrentSafeMax.y - CurrentSafeMin.y);

            SetSegment(_fogSegments[0], _edgeSegments[0], new Vector3((boundsMin.x + CurrentSafeMin.x) * 0.5f, height * 0.5f, (boundsMin.y + boundsMax.y) * 0.5f), new Vector3(Mathf.Max(0f, CurrentSafeMin.x - boundsMin.x), height, boundsMax.y - boundsMin.y), Axis.X);
            SetSegment(_fogSegments[1], _edgeSegments[1], new Vector3((CurrentSafeMax.x + boundsMax.x) * 0.5f, height * 0.5f, (boundsMin.y + boundsMax.y) * 0.5f), new Vector3(Mathf.Max(0f, boundsMax.x - CurrentSafeMax.x), height, boundsMax.y - boundsMin.y), Axis.X);
            SetSegment(_fogSegments[2], _edgeSegments[2], new Vector3((CurrentSafeMin.x + CurrentSafeMax.x) * 0.5f, height * 0.5f, (boundsMin.y + CurrentSafeMin.y) * 0.5f), new Vector3(safeWidth, height, Mathf.Max(0f, CurrentSafeMin.y - boundsMin.y)), Axis.Z);
            SetSegment(_fogSegments[3], _edgeSegments[3], new Vector3((CurrentSafeMin.x + CurrentSafeMax.x) * 0.5f, height * 0.5f, (CurrentSafeMax.y + boundsMax.y) * 0.5f), new Vector3(safeWidth, height, Mathf.Max(0f, boundsMax.y - CurrentSafeMax.y)), Axis.Z);
        }

        private void SetSegment(Transform fogSegment, Transform edgeSegment, Vector3 center, Vector3 size, Axis edgeAxis)
        {
            var hasFog = size.x > MinimumSegmentSize && size.z > MinimumSegmentSize && size.y > MinimumSegmentSize;
            fogSegment.gameObject.SetActive(hasFog);
            edgeSegment.gameObject.SetActive(hasFog);
            if (!hasFog)
            {
                return;
            }

            fogSegment.position = center;
            fogSegment.localScale = size;

            var edgeSize = size;
            if (edgeAxis == Axis.X)
            {
                edgeSize.x = MinimumEdgeThickness;
                edgeSegment.position = new Vector3(center.x + (size.x * 0.5f * Mathf.Sign(_corePosition.x - center.x)), center.y, center.z);
            }
            else
            {
                edgeSize.z = MinimumEdgeThickness;
                edgeSegment.position = new Vector3(center.x, center.y, center.z + (size.z * 0.5f * Mathf.Sign(_corePosition.z - center.z)));
            }

            edgeSize.x = Mathf.Max(edgeSize.x, MinimumSegmentSize);
            edgeSize.z = Mathf.Max(edgeSize.z, MinimumSegmentSize);
            edgeSize.y = Mathf.Max(edgeSize.y, MinimumSegmentSize);
            edgeSegment.localScale = edgeSize;
        }

        private bool TryBuildBoundsFromScene(out Bounds bounds)
        {
            var activeScene = gameObject.scene.IsValid() ? gameObject.scene : SceneManager.GetActiveScene();
            var roots = activeScene.GetRootGameObjects();
            var found = false;
            bounds = default;

            for (var i = 0; i < roots.Length; i++)
            {
                if (roots[i] == gameObject)
                {
                    continue;
                }

                var renderers = roots[i].GetComponentsInChildren<Renderer>(true);
                for (var rendererIndex = 0; rendererIndex < renderers.Length; rendererIndex++)
                {
                    var candidate = renderers[rendererIndex];
                    if (!(candidate is MeshRenderer) || candidate.transform.IsChildOf(transform))
                    {
                        continue;
                    }

                    if (!found)
                    {
                        bounds = candidate.bounds;
                        found = true;
                    }
                    else
                    {
                        bounds.Encapsulate(candidate.bounds);
                    }
                }
            }

            if (!found)
            {
                bounds = _tideBounds;
            }

            var min = bounds.min;
            var max = bounds.max;
            min.y = 0f;
            max.y = Mathf.Max(max.y, _maxFogHeight);
            bounds.SetMinMax(min, max);
            return found;
        }

        private void EnsureVisuals()
        {
            if (_fogSegments.Count > 0)
            {
                return;
            }

            _fogMaterial = CreateMaterial("TideFog", _fogColor);
            _edgeMaterial = CreateMaterial("TideEdge", _edgeColor);

            for (var i = 0; i < 4; i++)
            {
                var fogSegment = CreateSegment($"FogSegment_{i}", _fogMaterial, _fogSegments);
                _fogRenderers.Add(fogSegment.GetComponent<MeshRenderer>());
                CreateSegment($"FogEdge_{i}", _edgeMaterial, _edgeSegments);
            }
        }

        private void EnsureAudio()
        {
            _rumbleSource ??= GetComponent<AudioSource>();
            if (_rumbleSource == null)
            {
                _rumbleSource = gameObject.AddComponent<AudioSource>();
            }

            _rumbleSource.loop = true;
            _rumbleSource.playOnAwake = false;
            _rumbleSource.spatialBlend = 0f;
            _rumbleSource.volume = 0f;
            _rumbleSource.pitch = _rumblePitch;
            _rumbleSource.clip ??= CreateRumbleClip($"{name}_TideRumble");
        }

        private void UpdateRumble()
        {
            if (_rumbleSource == null)
            {
                return;
            }

            var targetVolume = Mathf.Lerp(0.02f, _maxRumbleVolume, Mathf.SmoothStep(0f, 1f, Progress));
            _rumbleSource.volume = Mathf.MoveTowards(_rumbleSource.volume, targetVolume, Time.deltaTime * 0.12f);

            if (_rumbleSource.clip == null)
            {
                return;
            }

            if (!_rumbleSource.isPlaying && targetVolume > 0.001f)
            {
                _rumbleSource.Play();
            }
        }

        private Transform CreateSegment(string name, Material material, List<Transform> targetList)
        {
            var segment = GameObject.CreatePrimitive(PrimitiveType.Cube);
            segment.name = name;
            segment.transform.SetParent(transform, false);
            segment.layer = gameObject.layer;

            if (segment.TryGetComponent<Collider>(out var collider))
            {
                if (Application.isPlaying)
                {
                    Destroy(collider);
                }
                else
                {
                    DestroyImmediate(collider);
                }
            }

            var renderer = segment.GetComponent<MeshRenderer>();
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.sharedMaterial = material;
            targetList.Add(segment.transform);
            return segment.transform;
        }

        private static Material CreateMaterial(string materialName, Color color)
        {
            var shader =
#if PHASE_1
                Shader.Find("Unlit/Color") ??
#else
                Shader.Find("Universal Render Pipeline/Unlit") ??
#endif
                Shader.Find("Universal Render Pipeline/Unlit") ??
                Shader.Find("Unlit/Color") ??
                Shader.Find("Standard");
            var material = new Material(shader)
            {
                name = materialName,
            };

            if (material.HasProperty(BaseColorProperty))
            {
                material.SetColor(BaseColorProperty, color);
            }

            if (material.HasProperty(ColorProperty))
            {
                material.SetColor(ColorProperty, color);
            }

            return material;
        }

        private static float GetPhase1FogHeight(float height)
        {
#if PHASE_1
            return Mathf.Max(0.2f, height * 0.35f);
#else
            return height;
#endif
        }

        private static AudioClip CreateRumbleClip(string clipName)
        {
            const int sampleRate = 22050;
            const float duration = 2.8f;
            var sampleCount = Mathf.CeilToInt(sampleRate * duration);
            var samples = new float[sampleCount];

            for (var i = 0; i < sampleCount; i++)
            {
                var t = i / (float)sampleRate;
                var lowBand = Mathf.Sin(2f * Mathf.PI * 34f * t) * 0.28f;
                var midBand = Mathf.Sin(2f * Mathf.PI * 52f * t) * 0.12f;
                var noise = (Mathf.PerlinNoise(t * 4.3f, 0.37f) - 0.5f) * 0.22f;
                var pulse = Mathf.Sin(2f * Mathf.PI * 0.21f * t) * 0.05f;
                samples[i] = Mathf.Clamp(lowBand + midBand + noise + pulse, -1f, 1f);
            }

            var clip = AudioClip.Create(clipName, sampleCount, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private enum Axis
        {
            X,
            Z,
        }
    }
}
