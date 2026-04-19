#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceProviders;
using UnityEngine.SceneManagement;

namespace ExtractionWeight.Zone
{
    public sealed class ZoneLoader : MonoBehaviour
    {
        [SerializeField] private List<ZoneDefinition> _availableZones = new();
        [SerializeField] private GameObject? _extractionPointMarkerPrefab;

        private readonly List<GameObject> _spawnedMarkers = new();
        private AsyncOperationHandle<SceneInstance>? _loadedSceneHandle;
        private Scene? _loadedScene;
        private GameObject? _markerRoot;
        private bool _loadedViaAddressables;

        public event Action<ZoneDefinition>? OnZoneLoaded;

        public ZoneDefinition? CurrentZoneDefinition { get; private set; }
        public Scene? LoadedZoneScene => _loadedScene;
        public IReadOnlyList<GameObject> SpawnedMarkers => _spawnedMarkers;

        public async Task<ZoneDefinition> LoadZoneByIdAsync(string zoneId)
        {
            if (string.IsNullOrWhiteSpace(zoneId))
            {
                throw new ArgumentException("Zone id is required.", nameof(zoneId));
            }

            var zoneDefinition = GetZoneDefinition(zoneId);
            if (zoneDefinition is null)
            {
                throw new InvalidOperationException($"No zone definition was found for id '{zoneId}'.");
            }

            if (CurrentZoneDefinition is not null)
            {
                await UnloadCurrentZoneAsync();
            }

            if (ShouldUseEditorSceneFallback(zoneDefinition))
            {
                await LoadEditorSceneFallbackAsync(zoneDefinition);
            }
            else
            {
                var sceneHandle = Addressables.LoadSceneAsync(
                    zoneDefinition.SceneAddressableKey,
                    LoadSceneMode.Additive,
                    activateOnLoad: true);

                _loadedSceneHandle = sceneHandle;
                await sceneHandle.Task;
                _loadedViaAddressables = true;
                _loadedScene = sceneHandle.Result.Scene;
            }

            CurrentZoneDefinition = zoneDefinition;
            SpawnExtractionPointMarkers(zoneDefinition);
            OnZoneLoaded?.Invoke(zoneDefinition);
            return zoneDefinition;
        }

        public async Task UnloadCurrentZoneAsync()
        {
            DestroySpawnedMarkers();

            if (_loadedViaAddressables && _loadedSceneHandle.HasValue && _loadedSceneHandle.Value.IsValid())
            {
                if (_loadedSceneHandle.Value.Status == AsyncOperationStatus.Succeeded)
                {
                    var unloadHandle = Addressables.UnloadSceneAsync(_loadedSceneHandle.Value, true);
                    await unloadHandle.Task;
                }
                else
                {
                    Addressables.Release(_loadedSceneHandle.Value);
                }

                _loadedSceneHandle = null;
            }
            else if (_loadedScene.HasValue && _loadedScene.Value.IsValid() && _loadedScene.Value.isLoaded)
            {
                var unloadOperation = SceneManager.UnloadSceneAsync(_loadedScene.Value);
                if (unloadOperation != null)
                {
                    await AwaitOperationAsync(unloadOperation);
                }
            }

            _loadedViaAddressables = false;
            _loadedScene = null;
            CurrentZoneDefinition = null;
        }

        public ZoneDefinition? GetZoneDefinition(string zoneId)
        {
            for (var i = 0; i < _availableZones.Count; i++)
            {
                var zone = _availableZones[i];
                if (zone != null && string.Equals(zone.ZoneId, zoneId, StringComparison.Ordinal))
                {
                    return zone;
                }
            }

            return null;
        }

#if UNITY_EDITOR
        public void EditorConfigure(List<ZoneDefinition> availableZones, GameObject extractionPointMarkerPrefab)
        {
            _availableZones = availableZones;
            _extractionPointMarkerPrefab = extractionPointMarkerPrefab;
        }
#endif

        private void SpawnExtractionPointMarkers(ZoneDefinition zoneDefinition)
        {
            var markerPrefab = ResolveMarkerPrefab();
            _markerRoot = new GameObject($"{zoneDefinition.ZoneId}_ExtractionMarkers");

            for (var i = 0; i < zoneDefinition.ExtractionPoints.Count; i++)
            {
                var pointData = zoneDefinition.ExtractionPoints[i];
                var marker = Instantiate(markerPrefab, pointData.Position, Quaternion.identity, _markerRoot.transform);
                var markerComponent = marker.GetComponent<ZoneExtractionPointMarker>();
                if (markerComponent == null)
                {
                    markerComponent = marker.AddComponent<ZoneExtractionPointMarker>();
                }

                markerComponent.Initialize(pointData);
                _spawnedMarkers.Add(marker);
            }
        }

        private GameObject ResolveMarkerPrefab()
        {
            if (_extractionPointMarkerPrefab != null)
            {
                return _extractionPointMarkerPrefab;
            }

            var fallback = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            fallback.name = "ExtractionPointMarker_Fallback";
            fallback.hideFlags = HideFlags.HideAndDontSave;

            var renderer = fallback.GetComponent<MeshRenderer>();
            if (renderer != null)
            {
                var material = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
                material.color = Color.red;
                renderer.sharedMaterial = material;
            }

            _extractionPointMarkerPrefab = fallback;
            return fallback;
        }

        private static async Task AwaitOperationAsync(AsyncOperation operation)
        {
            while (!operation.isDone)
            {
                await Task.Yield();
            }
        }

        private static bool ShouldUseEditorSceneFallback(ZoneDefinition zoneDefinition)
        {
            return !string.IsNullOrWhiteSpace(zoneDefinition.EditorScenePath) && !HasAddressablesRuntimeData();
        }

        private async Task LoadEditorSceneFallbackAsync(ZoneDefinition zoneDefinition)
        {
            if (string.IsNullOrWhiteSpace(zoneDefinition.EditorScenePath))
            {
                throw new InvalidOperationException($"Zone '{zoneDefinition.ZoneId}' does not have an editor scene fallback.");
            }

            if (!File.Exists(zoneDefinition.EditorScenePath))
            {
                throw new FileNotFoundException($"Zone '{zoneDefinition.ZoneId}' editor scene path was not found.", zoneDefinition.EditorScenePath);
            }

            var operation = SceneManager.LoadSceneAsync(zoneDefinition.EditorScenePath, LoadSceneMode.Additive);
            if (operation == null)
            {
                throw new InvalidOperationException($"Failed to start fallback load for scene '{zoneDefinition.EditorScenePath}'.");
            }

            _loadedSceneHandle = null;
            _loadedViaAddressables = false;
            await AwaitOperationAsync(operation);
            _loadedScene = SceneManager.GetSceneByPath(zoneDefinition.EditorScenePath);
        }

        private static bool HasAddressablesRuntimeData()
        {
            var projectRoot = Directory.GetCurrentDirectory();
            var settingsPath = Path.Combine(projectRoot, "Library", "com.unity.addressables", "aa", "Windows", "settings.json");
            return File.Exists(settingsPath);
        }

        private void DestroySpawnedMarkers()
        {
            for (var i = 0; i < _spawnedMarkers.Count; i++)
            {
                var marker = _spawnedMarkers[i];
                if (marker != null)
                {
                    Destroy(marker);
                }
            }

            _spawnedMarkers.Clear();

            if (_markerRoot != null)
            {
                Destroy(_markerRoot);
                _markerRoot = null;
            }
        }
    }
}
