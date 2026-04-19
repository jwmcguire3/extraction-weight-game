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
        private Sprite _icon = null!;
        private List<LootDefinition> _originalLootDefinitions = null!;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            PlayerStash.ResetSingletonForTests();
            SceneManager.LoadScene("Boot", LoadSceneMode.Single);
            yield return null;
            yield return new WaitUntil(() => GameFlowManager.Instance != null);
            yield return new WaitUntil(() => SceneManager.GetSceneByName("Base").isLoaded);
            GameFlowManager.Instance!.ResetProgressForTests();
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
            if (LootDatabase.Instance != null)
            {
                LootDatabase.Instance.EditorSetDefinitions(_originalLootDefinitions);
            }

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
            yield return new WaitUntil(() => GameFlowManager.Instance!.State == GameFlowState.InZone);
            var player = Object.FindAnyObjectByType<PlayerController>();
            Assert.That(player, Is.Not.Null);
            CreateLootPickupNearPlayer(player!, "run-loop-lost", 40f).TryCompletePickup(player!, out _);

            GameFlowManager.Instance!.FailCurrentRun();
            yield return new WaitUntil(() => GameFlowManager.Instance!.State == GameFlowState.AtBase);

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
            yield return new WaitUntil(() => GameFlowManager.Instance!.State == GameFlowState.InZone);

            var player = Object.FindAnyObjectByType<PlayerController>();
            Assert.That(player, Is.Not.Null);

            var pickup = CreateLootPickupNearPlayer(player!, itemId, value);
            Assert.That(pickup.TryCompletePickup(player!, out var failureMessage), Is.True, failureMessage);

            var extractionRoot = GameObject.Find("ExtractionPoint_A");
            var extractionController = extractionRoot != null ? extractionRoot.GetComponent<ExtractionWeight.Extraction.ExtractionController>() : null;
            Assert.That(extractionController, Is.Not.Null);
            extractionController!.TriggerExtraction(player!);

            yield return new WaitUntil(() => GameFlowManager.Instance!.State == GameFlowState.AtBase);
            yield return null;
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
