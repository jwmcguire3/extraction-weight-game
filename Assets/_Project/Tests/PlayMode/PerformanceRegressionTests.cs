#nullable enable
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ExtractionWeight.Core;
using ExtractionWeight.Extraction;
using ExtractionWeight.Loot;
using ExtractionWeight.MetaState;
using ExtractionWeight.Telemetry;
using ExtractionWeight.Threat;
using ExtractionWeight.Zone;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace ExtractionWeight.Tests.PlayMode
{
    public class PerformanceRegressionTests
    {
        private const float SetupTimeoutSeconds = 5f;
        private const float TransitionTimeoutSeconds = 10f;
        private const float PerformanceRunDurationSeconds = 60f;

        private string _telemetryDirectory = null!;
        private Sprite _icon = null!;
        private List<LootDefinition> _originalLootDefinitions = null!;
        private List<ZoneDefinition> _originalZones = null!;
        private GameObject? _originalMarkerPrefab;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            _telemetryDirectory = Path.Combine(Path.GetTempPath(), "ExtractionWeightTelemetryPlayMode", Path.GetRandomFileName());
            Directory.CreateDirectory(_telemetryDirectory);
            Phase1TelemetryService.EditorSetLogDirectoryOverride(_telemetryDirectory);
            Phase1TelemetryService.EditorResetForTests();

            PlayerStash.EditorSetPersistenceEnabled(false);
            PlayerStash.ResetSingletonForTests();
            if (GameFlowManager.Instance == null || !SceneManager.GetSceneByName("Base").isLoaded)
            {
                SceneManager.LoadScene("Boot", LoadSceneMode.Single);
                yield return null;
            }

            yield return WaitForCondition(() => GameFlowManager.Instance != null, SetupTimeoutSeconds, "GameFlowManager was not created after loading Boot.");
            yield return WaitForCondition(() => SceneManager.GetSceneByName("Base").isLoaded, SetupTimeoutSeconds, "Base scene did not finish loading during PlayMode setup.");
            GameFlowManager.Instance!.ResetProgressForTests();
            CaptureOriginalZoneLoaderState();
            ConfigureFastDrydockDefinition();
            _originalLootDefinitions = new List<LootDefinition>(LootDatabase.Instance!.Definitions);

            var texture = new Texture2D(4, 4, TextureFormat.RGBA32, false);
            var pixels = Enumerable.Repeat(Color.white, 16).ToArray();
            texture.SetPixels(pixels);
            texture.Apply();
            _icon = Sprite.Create(texture, new Rect(0f, 0f, 4f, 4f), new Vector2(0.5f, 0.5f));
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            PlayerStash.ResetSingletonForTests();
            PlayerStash.EditorSetPersistenceEnabled(true);
            Phase1TelemetryService.EditorResetForTests();
            Phase1TelemetryService.EditorSetLogDirectoryOverride(null);

            if (LootDatabase.Instance != null)
            {
                LootDatabase.Instance.EditorSetDefinitions(_originalLootDefinitions);
            }

            RestoreOriginalZoneLoaderState();

            foreach (var definition in Resources.FindObjectsOfTypeAll<ScriptableObject>())
            {
                if (definition != null && definition.name.StartsWith("performance-regression-", System.StringComparison.Ordinal))
                {
                    Object.DestroyImmediate(definition);
                }
            }

            if (_icon != null)
            {
                Object.DestroyImmediate(_icon.texture);
                Object.DestroyImmediate(_icon);
            }

            if (Directory.Exists(_telemetryDirectory))
            {
                Directory.Delete(_telemetryDirectory, recursive: true);
            }

            yield return null;
        }

        [UnityTest]
        public IEnumerator DrydockRun_AveragesAtLeast55Fps_AndCapturesExpectedTelemetry()
        {
            yield return EnterZone();

            var player = Object.FindAnyObjectByType<PlayerController>();
            var runtime = Object.FindAnyObjectByType<ZoneRuntime>();
            var extractionController = CreateExtractionController(runtime!, player!.transform.position);
            Assert.That(player, Is.Not.Null);
            Assert.That(runtime, Is.Not.Null);
            Assert.That(extractionController, Is.Not.Null);

            ReconfigureZoneRuntimeForFastExtraction(runtime!, extractionController!);
            var pickup = CreateLootPickupNearPlayer(player!, "performance-regression-battery", 55f, new CostSignature(0.45f, 0.1f, 0f, 0f));
            Assert.That(pickup.TryCompletePickup(player!, out var failureMessage), Is.True, failureMessage);
            yield return new WaitForSeconds(0.25f);

            var listener = CreateListenerNearPlayer(player!);
            yield return new WaitForSeconds(0.5f);
            Assert.That(listener.CurrentState, Is.EqualTo(DetectionState.Detected).Or.EqualTo(DetectionState.Suspicious));

            player.transform.position = extractionController!.transform.position + new Vector3(0f, 0f, -0.25f);
            extractionController.TriggerExtraction(player!);
            yield return WaitForCondition(
                () => extractionController.CurrentState == ExtractionPhaseState.Completed,
                TransitionTimeoutSeconds,
                "Extraction did not complete during the performance regression run.");

            var frames = 0;
            var startTime = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - startTime < PerformanceRunDurationSeconds)
            {
                var t = Time.realtimeSinceStartup - startTime;
                player!.SetUiMoveInput(new Vector2(Mathf.Sin(t * 0.8f), Mathf.Cos(t * 0.6f)));
                player.SetUiSprintHeld((frames % 120) < 45);
                frames++;
                yield return null;
            }

            player.SetUiMoveInput(Vector2.zero);
            player.SetUiSprintHeld(false);

            var elapsedSeconds = Time.realtimeSinceStartup - startTime;
            var averageFps = frames / Mathf.Max(elapsedSeconds, 0.0001f);
            Debug.Log($"PHASE1_DRYDOCK_AVG_FPS={averageFps:0.00}; PHASE1_DRYDOCK_AVG_FRAME_MS={1000f / averageFps:0.00}");
            Assert.That(averageFps, Is.GreaterThanOrEqualTo(55f), $"Average FPS was {averageFps:0.00} over {elapsedSeconds:0.00}s.");

            yield return FlushTelemetry();
            Phase1TelemetryService.EditorResetForTests();

            var logPath = Directory.EnumerateFiles(_telemetryDirectory, "*.jsonl").Single();
            var logText = File.ReadAllText(logPath);
            StringAssert.Contains($"\"eventName\":\"{TelemetryEventNames.RunStarted}\"", logText);
            StringAssert.Contains($"\"eventName\":\"{TelemetryEventNames.ItemPickedUp}\"", logText);
            StringAssert.Contains($"\"eventName\":\"{TelemetryEventNames.BreakpointCrossed}\"", logText);
            StringAssert.Contains($"\"eventName\":\"{TelemetryEventNames.ThreatDetected}\"", logText);
            StringAssert.Contains($"\"eventName\":\"{TelemetryEventNames.ExtractionInitiated}\"", logText);
            StringAssert.Contains($"\"eventName\":\"{TelemetryEventNames.ExtractionCompleted}\"", logText);
        }

        [UnityTest]
        public IEnumerator PlayerDeath_RecordsTelemetryEvent()
        {
            yield return EnterZone();

            var player = Object.FindAnyObjectByType<PlayerController>();
            var health = Object.FindAnyObjectByType<PlayerHealth>();
            Assert.That(player, Is.Not.Null);
            Assert.That(health, Is.Not.Null);

            CreateLootPickupNearPlayer(player!, "performance-regression-relic", 30f, new CostSignature(0.12f, 0f, 0f, 0f)).TryCompletePickup(player!, out _);
            GameFlowManager.Instance!.EditorSetRunStartRealtimeSeconds(Time.realtimeSinceStartup - 12f);
            health!.TakeDamage(200f, new TestThreat("performance-threat"));
            yield return WaitForCondition(
                () => GameFlowManager.Instance != null && GameFlowManager.Instance.State == GameFlowState.AtBase,
                TransitionTimeoutSeconds,
                "Death flow did not return to base.");

            yield return FlushTelemetry();
            Phase1TelemetryService.EditorResetForTests();

            var logPath = Directory.EnumerateFiles(_telemetryDirectory, "*.jsonl").Single();
            var logText = File.ReadAllText(logPath);
            StringAssert.Contains($"\"eventName\":\"{TelemetryEventNames.PlayerDied}\"", logText);
        }

        private static IEnumerator FlushTelemetry()
        {
            var flushTask = Phase1TelemetryService.Instance!.FlushAsync();
            while (!flushTask.IsCompleted)
            {
                yield return null;
            }
        }

        private IEnumerator EnterZone()
        {
            GameFlowManager.Instance!.EnterZone("drydock");
            yield return WaitForCondition(
                () => GameFlowManager.Instance != null && GameFlowManager.Instance.State == GameFlowState.InZone,
                TransitionTimeoutSeconds,
                "Drydock did not finish loading for the performance regression test.");
        }

        private static IEnumerator WaitForCondition(System.Func<bool> predicate, float timeoutSeconds, string failureMessage)
        {
            var startTime = Time.realtimeSinceStartup;
            while (!predicate())
            {
                if (Time.realtimeSinceStartup - startTime >= timeoutSeconds)
                {
                    Assert.Fail(failureMessage);
                }

                yield return null;
            }
        }

        private void ConfigureFastDrydockDefinition()
        {
            var loader = Object.FindAnyObjectByType<ZoneLoader>();
            Assert.That(loader, Is.Not.Null);

            var definition = ScriptableObject.CreateInstance<ZoneDefinition>();
            definition.name = "performance-regression-drydock";
            definition.EditorSetData(
                "drydock",
                "Drydock",
                "Zones/Drydock",
                Weight.ZoneAxisWeights.Uniform,
                180f,
                new List<ExtractionPointData>
                {
                    new("A", ExtractionType.Standard, new Vector3(-86f, 1.5f, -82f), 999f, ItemSizeFilter.AcceptsAll, 0.15f, holdRadius: 12f, initiationDurationSeconds: 0.05f, departureDurationSeconds: 0.05f),
                    new("B", ExtractionType.Standard, new Vector3(86f, 1.5f, -82f), 999f, ItemSizeFilter.AcceptsAll, 0.15f, holdRadius: 12f, initiationDurationSeconds: 0.05f, departureDurationSeconds: 0.05f),
                    new("C", ExtractionType.Drone, new Vector3(-86f, 1.5f, 82f), 999f, ItemSizeFilter.AcceptsAll, 0.15f, holdRadius: 12f, initiationDurationSeconds: 0.05f, departureDurationSeconds: 0.05f),
                    new("D", ExtractionType.Overland, new Vector3(86f, 1.5f, 82f), 999f, ItemSizeFilter.AcceptsAll, 0.15f, holdRadius: 12f, initiationDurationSeconds: 0.05f, departureDurationSeconds: 0.05f, approachDurationSeconds: 0.1f),
                },
                new List<LootSpawnRegion>(),
                new List<ThreatPatrolRoute>(),
                "Assets/_Project/Scenes/Zones/Drydock.unity");

            loader!.EditorConfigure(new List<ZoneDefinition> { definition }, null!);
        }

        private static void ReconfigureZoneRuntimeForFastExtraction(ZoneRuntime runtime, ExtractionController extractionController)
        {
            var definition = ScriptableObject.CreateInstance<ZoneDefinition>();
            definition.name = "performance-regression-runtime";
            definition.EditorSetData(
                "drydock",
                "Drydock",
                "Zones/Drydock",
                Weight.ZoneAxisWeights.Uniform,
                180f,
                new List<ExtractionPointData>
                {
                    new("PerfA", ExtractionType.Standard, extractionController.transform.position, 999f, ItemSizeFilter.AcceptsAll, 0.1f, holdRadius: 8f, initiationDurationSeconds: 0.05f, departureDurationSeconds: 0.05f),
                },
                new List<LootSpawnRegion>(),
                new List<ThreatPatrolRoute>(),
                "Assets/_Project/Scenes/Zones/Drydock.unity");
            runtime.Initialize(definition);
            extractionController.EditorAssignRuntime(runtime);
        }

        private void CaptureOriginalZoneLoaderState()
        {
            var loader = Object.FindAnyObjectByType<ZoneLoader>();
            Assert.That(loader, Is.Not.Null);
            _originalZones = loader!.EditorGetAvailableZones();
            _originalMarkerPrefab = loader.EditorGetExtractionPointMarkerPrefab();
        }

        private void RestoreOriginalZoneLoaderState()
        {
            var loader = Object.FindAnyObjectByType<ZoneLoader>();
            if (loader == null || _originalZones == null || _originalMarkerPrefab == null)
            {
                return;
            }

            loader.EditorConfigure(_originalZones, _originalMarkerPrefab);
        }

        private LootPickup CreateLootPickupNearPlayer(PlayerController player, string itemId, float value, CostSignature baseCost)
        {
            var definition = ScriptableObject.CreateInstance<LootDefinition>();
            definition.name = itemId;
            definition.EditorSetData(
                itemId,
                itemId,
                _icon,
                LootCategory.Currency,
                baseCost,
                value,
                false,
                Vector3.one * 0.2f,
                null,
                null,
                default);
            RegisterLootDefinition(definition);

            var pickupObject = new GameObject($"Pickup_{itemId}");
            pickupObject.transform.position = player.transform.position + new Vector3(0f, 0f, 1f);
            pickupObject.AddComponent<SphereCollider>().radius = 0.75f;
            var pickup = pickupObject.AddComponent<LootPickup>();
            pickup.Configure(definition);
            return pickup;
        }

        private static void RegisterLootDefinition(LootDefinition definition)
        {
            Assert.That(LootDatabase.Instance, Is.Not.Null);
            var updatedDefinitions = new List<LootDefinition>(LootDatabase.Instance!.Definitions);
            updatedDefinitions.Add(definition);
            LootDatabase.Instance.EditorSetDefinitions(updatedDefinitions);
        }

        private static Listener CreateListenerNearPlayer(PlayerController player)
        {
            var threatObject = new GameObject("performance-regression-listener");
            threatObject.transform.position = player.transform.position + new Vector3(0f, 0f, 4f);
            return threatObject.AddComponent<Listener>();
        }

        private static ExtractionController CreateExtractionController(ZoneRuntime runtime, Vector3 position)
        {
            var extractionRoot = new GameObject("PerformanceRegression_ExtractionPoint");
            extractionRoot.transform.position = position;
            extractionRoot.AddComponent<SphereCollider>().isTrigger = true;
            extractionRoot.AddComponent<PlayerContextActionTarget>();

            var controller = extractionRoot.AddComponent<ExtractionController>();
            controller.EditorAssignRuntime(runtime);
            controller.EditorConfigure("PerfA", extractionRoot.transform, null, null);
            return controller;
        }

        private sealed class TestThreat : IThreat
        {
            public TestThreat(string threatId)
            {
                ThreatId = threatId;
                Profile = new DetectionProfile(1f, 1f, 5f, 5f, 5f);
            }

            public string ThreatId { get; }

            public DetectionProfile Profile { get; }
        }
    }
}
