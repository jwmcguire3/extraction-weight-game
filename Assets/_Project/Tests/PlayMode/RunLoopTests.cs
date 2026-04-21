#nullable enable
using System.Collections;
using System.Collections.Generic;
using ExtractionWeight.Core;
using ExtractionWeight.Loot;
using ExtractionWeight.MetaState;
using ExtractionWeight.UI;
using ExtractionWeight.Zone;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace ExtractionWeight.Tests.PlayMode
{
    public class RunLoopTests
    {
        private const float SetupTimeoutSeconds = 5f;
        private const float TransitionTimeoutSeconds = 10f;

        private Sprite _icon = null!;
        private List<LootDefinition> _originalLootDefinitions = null!;
        private List<ZoneDefinition> _originalZones = null!;
        private GameObject? _originalMarkerPrefab;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
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
            var pixels = new Color[16];
            for (var i = 0; i < pixels.Length; i++)
            {
                pixels[i] = Color.white;
            }

            texture.SetPixels(pixels);
            texture.Apply();
            _icon = Sprite.Create(texture, new Rect(0f, 0f, 4f, 4f), new Vector2(0.5f, 0.5f));
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            PlayerStash.ResetSingletonForTests();
            PlayerStash.EditorSetPersistenceEnabled(true);
            if (LootDatabase.Instance != null)
            {
                LootDatabase.Instance.EditorSetDefinitions(_originalLootDefinitions);
            }

            RestoreOriginalZoneLoaderState();

            foreach (var definition in Resources.FindObjectsOfTypeAll<ScriptableObject>())
            {
                if (definition != null && definition.name.StartsWith("run-loop-", System.StringComparison.Ordinal))
                {
                    Object.DestroyImmediate(definition);
                }
            }

            if (_icon != null)
            {
                Object.DestroyImmediate(_icon.texture);
                Object.DestroyImmediate(_icon);
            }

            yield return null;
        }

        [UnityTest]
        public IEnumerator FullLoop_BaseToDrydockPickupExtractToBase_UpdatesStash()
        {
            yield return RunSuccessfulExtraction("run-loop-cash", 25f);

            var stash = PlayerStash.Instance;
            Assert.That(stash.Items, Has.Count.EqualTo(1));
            Assert.That(stash.Items[0].ItemId, Is.EqualTo("run-loop-cash"));
            Assert.That(stash.Items[0].Count, Is.EqualTo(1));

            var baseScreen = Object.FindAnyObjectByType<BaseScreenController>();
            Assert.That(baseScreen, Is.Not.Null);
            Assert.That(baseScreen!.LastRunText, Does.StartWith("Success"));
        }

        [UnityTest]
        public IEnumerator FailedRun_BaseToDrydockDeathToBase_LeavesStashUnchangedAndShowsFailure()
        {
            yield return RunSuccessfulExtraction("run-loop-starter", 20f);
            var startingValue = PlayerStash.Instance.TotalValue;

            GameFlowManager.Instance!.EnterZone("drydock");
            yield return WaitForCondition(
                () => GameFlowManager.Instance != null && GameFlowManager.Instance.State == GameFlowState.InZone,
                TransitionTimeoutSeconds,
                "Failed-run setup never reached the in-zone state.");
            var player = Object.FindAnyObjectByType<PlayerController>();
            Assert.That(player, Is.Not.Null);
            CreateLootPickupNearPlayer(player!, "run-loop-lost", 40f).TryCompletePickup(player!, out _);

            GameFlowManager.Instance!.FailCurrentRun();
            yield return WaitForCondition(
                () => GameFlowManager.Instance != null && GameFlowManager.Instance.State == GameFlowState.AtBase,
                TransitionTimeoutSeconds,
                "Failed run never returned to base.");

            Assert.That(PlayerStash.Instance.TotalValue, Is.EqualTo(startingValue).Within(0.0001f));

            var baseScreen = Object.FindAnyObjectByType<BaseScreenController>();
            Assert.That(baseScreen, Is.Not.Null);
            Assert.That(baseScreen!.LastRunText, Does.StartWith("Failed"));
        }

        [UnityTest]
        public IEnumerator MultipleSequentialRuns_AccumulateCorrectly()
        {
            yield return RunSuccessfulExtraction("run-loop-chip", 30f);
            yield return RunSuccessfulExtraction("run-loop-chip", 30f);
            yield return RunSuccessfulExtraction("run-loop-relic", 45f);

            Assert.That(PlayerStash.Instance.Items, Has.Count.EqualTo(2));
            Assert.That(PlayerStash.Instance.TotalValue, Is.EqualTo(105f).Within(0.0001f));
            Assert.That(GameFlowManager.Instance!.SessionStats.RunsAttempted, Is.EqualTo(3));
            Assert.That(GameFlowManager.Instance!.SessionStats.RunsSuccessful, Is.EqualTo(3));
        }

        private IEnumerator RunSuccessfulExtraction(string itemId, float value)
        {
            GameFlowManager.Instance!.EnterZone("drydock");
            yield return WaitForCondition(
                () => GameFlowManager.Instance != null && GameFlowManager.Instance.State == GameFlowState.InZone,
                TransitionTimeoutSeconds,
                "Successful-run setup never reached the in-zone state.");

            var player = Object.FindAnyObjectByType<PlayerController>();
            Assert.That(player, Is.Not.Null);

            var pickup = CreateLootPickupNearPlayer(player!, itemId, value);
            Assert.That(pickup.TryCompletePickup(player!, out var failureMessage), Is.True, failureMessage);

            GameFlowManager.Instance!.CompleteSuccessfulRun("drydock", CollectCarriedLoot(player!.CarryState));

            yield return WaitForCondition(
                () => GameFlowManager.Instance != null && GameFlowManager.Instance.State == GameFlowState.AtBase,
                TransitionTimeoutSeconds,
                "Successful extraction never completed the return-to-base flow.");
            yield return null;
        }

        private static List<LootItem> CollectCarriedLoot(ExtractionWeight.Weight.CarryState carryState)
        {
            var items = new List<LootItem>(carryState.Items.Count);
            for (var i = 0; i < carryState.Items.Count; i++)
            {
                if (carryState.Items[i] is LootItem lootItem)
                {
                    items.Add(lootItem);
                }
            }

            return items;
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
            definition.name = "run-loop-drydock";
            definition.EditorSetData(
                "drydock",
                "Drydock",
                "Zones/Drydock",
                Weight.ZoneAxisWeights.Uniform,
                120f,
                new List<ExtractionPointData>
                {
                    new("A", ExtractionType.Standard, new Vector3(-86f, 1.5f, -82f), 999f, ItemSizeFilter.AcceptsAll, 0.05f, holdRadius: 12f, initiationDurationSeconds: 0.05f, departureDurationSeconds: 0.05f),
                },
                new List<LootSpawnRegion>(),
                new List<ThreatPatrolRoute>(),
                "Assets/_Project/Scenes/Zones/Drydock.unity");

            loader!.EditorConfigure(new List<ZoneDefinition> { definition }, null!);
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

        private LootPickup CreateLootPickupNearPlayer(PlayerController player, string itemId, float value)
        {
            var definition = ScriptableObject.CreateInstance<LootDefinition>();
            definition.name = itemId;
            definition.EditorSetData(
                itemId,
                itemId,
                _icon,
                LootCategory.Currency,
                new CostSignature(0.1f, 0f, 0f, 0f),
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
    }
}
