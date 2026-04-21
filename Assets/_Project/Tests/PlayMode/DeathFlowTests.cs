#nullable enable
using System.Collections;
using System.Collections.Generic;
using ExtractionWeight.Core;
using ExtractionWeight.Loot;
using ExtractionWeight.MetaState;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using ExtractionWeight.Zone;

namespace ExtractionWeight.Tests.PlayMode
{
    public class DeathFlowTests
    {
        private Sprite _icon = null!;
        private List<LootDefinition> _originalLootDefinitions = null!;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            PlayerStash.ResetSingletonForTests();
            if (GameFlowManager.Instance == null || !SceneManager.GetSceneByName("Base").isLoaded)
            {
                SceneManager.LoadScene("Boot", LoadSceneMode.Single);
                yield return null;
            }

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
                if (definition != null && definition.name.StartsWith("death-flow-", System.StringComparison.Ordinal))
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
        public IEnumerator LethalDamage_TransitionsPlayerIntoDeathFlow()
        {
            yield return EnterZone();
            var playerHealth = Object.FindAnyObjectByType<PlayerHealth>();
            Assert.That(playerHealth, Is.Not.Null);

            playerHealth!.TakeDamage(200f, new TestThreat("test-warden"));
            yield return null;

            Assert.That(playerHealth.IsDead, Is.True);
            Assert.That(GameFlowManager.Instance!.State, Is.EqualTo(GameFlowState.ReturningToBase));
            yield return new WaitUntil(() => GameFlowManager.Instance!.State == GameFlowState.AtBase);
        }

        [UnityTest]
        public IEnumerator LethalRun_ReturnsFailureWithLostLootValue()
        {
            yield return EnterZone();
            var player = Object.FindAnyObjectByType<PlayerController>();
            var health = Object.FindAnyObjectByType<PlayerHealth>();
            Assert.That(player, Is.Not.Null);
            Assert.That(health, Is.Not.Null);

            var pickup = CreateLootPickupNearPlayer(player!, "death-flow-chip", 35f);
            Assert.That(pickup.TryCompletePickup(player!, out var failureMessage), Is.True, failureMessage);

            health!.TakeDamage(200f, new TestThreat("test-listener"));
            yield return new WaitUntil(() => GameFlowManager.Instance!.State == GameFlowState.AtBase);

            Assert.That(GameFlowManager.Instance!.LastRunSummary, Is.Not.Null);
            Assert.That(GameFlowManager.Instance!.LastRunSummary!.WasSuccessful, Is.False);
            Assert.That(GameFlowManager.Instance!.LastRunSummary!.LostLootValue, Is.EqualTo(35f).Within(0.0001f));
        }

        [UnityTest]
        public IEnumerator ReturningToBaseAfterDeath_DoesNotPersistCarriedLoot()
        {
            yield return EnterZone();
            var player = Object.FindAnyObjectByType<PlayerController>();
            var health = Object.FindAnyObjectByType<PlayerHealth>();
            Assert.That(player, Is.Not.Null);
            Assert.That(health, Is.Not.Null);

            var pickup = CreateLootPickupNearPlayer(player!, "death-flow-relic", 45f);
            Assert.That(pickup.TryCompletePickup(player!, out var failureMessage), Is.True, failureMessage);

            health!.TakeDamage(200f, new TestThreat("test-warden"));
            yield return new WaitUntil(() => GameFlowManager.Instance!.State == GameFlowState.AtBase);

            Assert.That(PlayerStash.Instance.TotalValue, Is.EqualTo(0f).Within(0.0001f));
            Assert.That(PlayerStash.Instance.Items, Is.Empty);
        }

        private IEnumerator EnterZone()
        {
            GameFlowManager.Instance!.EnterZone("drydock");
            yield return new WaitUntil(() => GameFlowManager.Instance!.State == GameFlowState.InZone);
        }

        private void ConfigureFastDrydockDefinition()
        {
            var loader = Object.FindAnyObjectByType<ZoneLoader>();
            Assert.That(loader, Is.Not.Null);

            var definition = ScriptableObject.CreateInstance<ZoneDefinition>();
            definition.name = "death-flow-drydock";
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
