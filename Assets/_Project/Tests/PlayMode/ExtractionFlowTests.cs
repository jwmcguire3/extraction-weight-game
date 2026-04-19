#nullable enable
using System.Collections;
using System.Collections.Generic;
using ExtractionWeight.Core;
using ExtractionWeight.Extraction;
using ExtractionWeight.Loot;
using ExtractionWeight.Threat;
using ExtractionWeight.Weight;
using ExtractionWeight.Zone;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace ExtractionWeight.Tests.PlayMode
{
    public class ExtractionFlowTests
    {
        private Sprite _icon = null!;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            Phase1RunResultStore.Reset();

            var texture = new Texture2D(4, 4, TextureFormat.RGBA32, false);
            var pixels = new Color[16];
            for (var i = 0; i < pixels.Length; i++)
            {
                pixels[i] = Color.white;
            }

            texture.SetPixels(pixels);
            texture.Apply();
            _icon = Sprite.Create(texture, new Rect(0f, 0f, 4f, 4f), new Vector2(0.5f, 0.5f));

            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "ExtractionFlowGround";
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            foreach (var gameObject in Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None))
            {
                Object.Destroy(gameObject);
            }

            yield return null;

            if (_icon != null)
            {
                Object.DestroyImmediate(_icon.texture);
                Object.DestroyImmediate(_icon);
            }

            Phase1RunResultStore.Reset();
        }

        [UnityTest]
        public IEnumerator FullFlow_BanksExpectedLoot()
        {
            var player = CreatePlayer(new Vector3(0f, 0.05f, 0f));
            var item = CreateLootItem("bank-me", Vector3.one * 0.2f);
            player.CarryState.TryAdd(item);
            var controller = CreateExtractionSetup(
                player,
                new ExtractionPointData("A", ExtractionType.Standard, Vector3.zero, 99f, ItemSizeFilter.AcceptsAll, 0.1f, initiationDurationSeconds: 0.05f, departureDurationSeconds: 0.05f),
                out _);

            controller.TriggerExtraction(player);
            yield return new WaitForSeconds(0.3f);

            Assert.That(controller.CurrentState, Is.EqualTo(ExtractionPhaseState.Completed));
            Assert.That(Phase1RunResultStore.LastRunResult, Is.Not.Null);
            Assert.That(Phase1RunResultStore.LastRunResult!.BankedItemIds, Does.Contain(item.ItemId));
            Assert.That(Phase1RunResultStore.PlayerStashItemIds, Does.Contain(item.ItemId));
        }

        [UnityTest]
        public IEnumerator HoldInterruption_AbortsAndAllowsRetry()
        {
            var player = CreatePlayer(new Vector3(0f, 0.05f, 0f));
            var controller = CreateExtractionSetup(
                player,
                new ExtractionPointData("A", ExtractionType.Standard, Vector3.zero, 99f, ItemSizeFilter.AcceptsAll, 0.25f, initiationDurationSeconds: 0.05f, departureDurationSeconds: 0.05f, holdRadius: 1f),
                out _);

            controller.TriggerExtraction(player);
            yield return new WaitForSeconds(0.12f);
            player.transform.position = new Vector3(5f, 0.05f, 0f);
            yield return null;

            Assert.That(controller.CurrentState, Is.EqualTo(ExtractionPhaseState.Idle));
            Assert.That(controller.LastFailureMessage, Is.EqualTo("Extraction aborted"));

            player.transform.position = Vector3.zero;
            controller.TriggerExtraction(player);
            yield return new WaitForSeconds(0.4f);

            Assert.That(controller.CurrentState, Is.EqualTo(ExtractionPhaseState.Completed));
        }

        [UnityTest]
        public IEnumerator ExtraThreatSpawnDuringStandardHold_ActivatesThreat()
        {
            var player = CreatePlayer(new Vector3(0f, 0.05f, 0f));
            var controller = CreateExtractionSetup(
                player,
                new ExtractionPointData("A", ExtractionType.Standard, Vector3.zero, 99f, ItemSizeFilter.AcceptsAll, 0.3f, initiationDurationSeconds: 0.05f, departureDurationSeconds: 0.05f),
                out var extraThreat);

            controller.TriggerExtraction(player);
            yield return new WaitForSeconds(0.25f);

            Assert.That(extraThreat, Is.Not.Null);
            Assert.That(extraThreat!.gameObject.activeSelf, Is.True);
            Assert.That(controller.HasSpawnedExtraThreat, Is.True);
        }

        [UnityTest]
        public IEnumerator DroneExtraction_CompletesWithoutThreatSpawn()
        {
            var player = CreatePlayer(new Vector3(0f, 0.05f, 0f));
            var item = CreateLootItem("small", Vector3.one * 0.2f);
            player.CarryState.TryAdd(item);
            var controller = CreateExtractionSetup(
                player,
                new ExtractionPointData("C", ExtractionType.Drone, Vector3.zero, 99f, ItemSizeFilter.SmallOnly, 0.1f, initiationDurationSeconds: 0.05f, departureDurationSeconds: 0.05f, holdRadius: 3f, maxCarryCapacityFraction: 0.45f),
                out var extraThreat,
                createThreat: true);

            controller.TriggerExtraction(player);
            yield return new WaitForSeconds(0.3f);

            Assert.That(controller.CurrentState, Is.EqualTo(ExtractionPhaseState.Completed));
            Assert.That(controller.HasSpawnedExtraThreat, Is.False);
            Assert.That(extraThreat!.gameObject.activeSelf, Is.False);
        }

        [UnityTest]
        public IEnumerator OverlandWalk_CompletesWhenPlayerReachesBoundaryMarker()
        {
            var player = CreatePlayer(new Vector3(0f, 0.05f, 0f));
            var controller = CreateExtractionSetup(
                player,
                new ExtractionPointData("D", ExtractionType.Overland, Vector3.zero, 99f, ItemSizeFilter.AcceptsAll, 0f, initiationDurationSeconds: 0.05f, departureDurationSeconds: 0.05f, approachDurationSeconds: 0.4f),
                out _,
                boundaryLocalPosition: new Vector3(4f, 0f, 0f));

            controller.TriggerExtraction(player);
            yield return new WaitForSeconds(0.12f);
            player.transform.position = new Vector3(4f, 0.05f, 0f);
            yield return new WaitForSeconds(0.1f);

            Assert.That(controller.CurrentState, Is.EqualTo(ExtractionPhaseState.Completed));
        }

        [UnityTest]
        public IEnumerator TideClosedExtraction_FailsWithCorrectMessage()
        {
            var player = CreatePlayer(new Vector3(0f, 0.05f, 0f));
            var controller = CreateExtractionSetup(
                player,
                new ExtractionPointData("A", ExtractionType.Standard, Vector3.zero, 0.01f, ItemSizeFilter.AcceptsAll, 0.1f, initiationDurationSeconds: 0.05f, departureDurationSeconds: 0.05f),
                out _);

            yield return new WaitForSeconds(0.05f);
            controller.TriggerExtraction(player);
            yield return null;

            Assert.That(controller.CurrentState, Is.EqualTo(ExtractionPhaseState.Idle));
            Assert.That(controller.LastFailureMessage, Is.EqualTo("Extraction closed"));
        }

        private ExtractionController CreateExtractionSetup(
            PlayerController player,
            ExtractionPointData pointData,
            out Warden? extraThreat,
            bool createThreat = true,
            Vector3? boundaryLocalPosition = null)
        {
            var zoneDefinition = ScriptableObject.CreateInstance<ZoneDefinition>();
            zoneDefinition.EditorSetData(
                "test-zone",
                "Test Zone",
                "unused",
                ZoneAxisWeights.Uniform,
                120f,
                new List<ExtractionPointData> { pointData },
                new List<LootSpawnRegion>(),
                new List<ThreatPatrolRoute>(),
                string.Empty);

            var runtimeObject = new GameObject("ZoneRuntime");
            var runtime = runtimeObject.AddComponent<ZoneRuntime>();
            runtime.Initialize(zoneDefinition);

            var extractionRoot = new GameObject($"ExtractionPoint_{pointData.PointId}");
            extractionRoot.transform.position = pointData.Position;
            extractionRoot.AddComponent<SphereCollider>().isTrigger = true;
            extractionRoot.AddComponent<PlayerContextActionTarget>();

            extraThreat = null;
            if (createThreat)
            {
                var threatObject = new GameObject($"ExtraThreat_{pointData.PointId}");
                threatObject.transform.SetParent(extractionRoot.transform, false);
                extraThreat = threatObject.AddComponent<Warden>();
                extraThreat.gameObject.SetActive(false);
            }

            Transform? boundaryMarker = null;
            if (boundaryLocalPosition.HasValue)
            {
                var marker = new GameObject("BoundaryMarker");
                marker.transform.SetParent(extractionRoot.transform, false);
                marker.transform.localPosition = boundaryLocalPosition.Value;
                boundaryMarker = marker.transform;
            }

            var controller = extractionRoot.AddComponent<ExtractionController>();
            controller.EditorAssignRuntime(runtime);
            controller.EditorConfigure(pointData.PointId, extractionRoot.transform, boundaryMarker, extraThreat);

            return controller;
        }

        private PlayerController CreatePlayer(Vector3 position)
        {
            var playerObject = new GameObject("ExtractionFlowPlayer");
            playerObject.transform.position = position;

            var characterController = playerObject.AddComponent<CharacterController>();
            characterController.height = 1.8f;
            characterController.center = new Vector3(0f, 0.9f, 0f);
            characterController.radius = 0.35f;

            playerObject.AddComponent<InteractionTracker>();
            return playerObject.AddComponent<PlayerController>();
        }

        private LootItem CreateLootItem(string itemId, Vector3 size)
        {
            var definition = ScriptableObject.CreateInstance<LootDefinition>();
            definition.EditorSetData(
                itemId,
                itemId,
                _icon,
                LootCategory.Currency,
                new CostSignature(0.1f, 0f, 0f, 0f),
                25f,
                false,
                size,
                null,
                null,
                default);
            return new LootItem(definition);
        }
    }
}
