#nullable enable
using System.Collections;
using System.Collections.Generic;
using ExtractionWeight.Core;
using ExtractionWeight.Extraction;
using ExtractionWeight.UI;
using ExtractionWeight.Weight;
using ExtractionWeight.Zone;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace ExtractionWeight.Tests.PlayMode
{
    public sealed class TideVisualTests
    {
        [UnityTearDown]
        public IEnumerator TearDown()
        {
            foreach (var gameObject in Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None))
            {
                Object.Destroy(gameObject);
            }

            yield return null;
        }

        [UnityTest]
        public IEnumerator FogPlaneRendersAndAdvancesDuringPlay()
        {
            var zoneDefinition = CreateZoneDefinition(1f, new ExtractionPointData("A", ExtractionType.Standard, new Vector3(7f, 0f, 0f), 0.8f, ItemSizeFilter.AcceptsAll, 0.1f));
            var runtime = CreateRuntime(zoneDefinition);
            var tide = CreateTideController(runtime, zoneDefinition, new Bounds(Vector3.zero, new Vector3(20f, 4f, 20f)));
            var initialHeight = tide.CurrentFogHeight;

            yield return new WaitForSeconds(0.2f);

            Assert.That(tide.Progress, Is.GreaterThan(0f));
            Assert.That(tide.CurrentFogHeight, Is.GreaterThan(initialHeight));
            Assert.That(tide.FogRenderers.Count, Is.EqualTo(4));
            Assert.That(HasActiveFogRenderer(tide), Is.True);
        }

        [UnityTest]
        public IEnumerator HudTideBarUpdatesInSyncWithControllerProgress()
        {
            var player = CreatePlayer();
            var zoneDefinition = CreateZoneDefinition(1.5f, new ExtractionPointData("A", ExtractionType.Standard, new Vector3(7f, 0f, 0f), 1.0f, ItemSizeFilter.AcceptsAll, 0.1f));
            var runtime = CreateRuntime(zoneDefinition);
            var tide = CreateTideController(runtime, zoneDefinition, new Bounds(Vector3.zero, new Vector3(20f, 4f, 20f)));
            var hud = CreateHud(player);

            yield return new WaitForSeconds(0.3f);

            Assert.That(hud.TideBarFillAmount, Is.EqualTo(tide.Progress).Within(0.05f));
        }

        [UnityTest]
        public IEnumerator ClosedExtractionEntersClosedStateAfterClosePercent()
        {
            var zoneDefinition = CreateZoneDefinition(1f, new ExtractionPointData("A", ExtractionType.Standard, Vector3.zero, 0.2f, ItemSizeFilter.AcceptsAll, 0.1f));
            var runtime = CreateRuntime(zoneDefinition);
            _ = CreateTideController(runtime, zoneDefinition, new Bounds(Vector3.zero, new Vector3(20f, 4f, 20f)));
            var extraction = CreateExtractionController(runtime, "A");

            yield return new WaitForSeconds(0.35f);

            Assert.That(extraction.CurrentState, Is.EqualTo(ExtractionPhaseState.Closed));
        }

        private static bool HasActiveFogRenderer(TideController tideController)
        {
            for (var i = 0; i < tideController.FogRenderers.Count; i++)
            {
                var renderer = tideController.FogRenderers[i];
                if (renderer != null && renderer.gameObject.activeInHierarchy)
                {
                    return true;
                }
            }

            return false;
        }

        private static ZoneDefinition CreateZoneDefinition(float tideDurationSeconds, params ExtractionPointData[] extractionPoints)
        {
            var zoneDefinition = ScriptableObject.CreateInstance<ZoneDefinition>();
            zoneDefinition.EditorSetData(
                "test-zone",
                "Test Zone",
                "unused",
                ZoneAxisWeights.Uniform,
                tideDurationSeconds,
                new List<ExtractionPointData>(extractionPoints),
                new List<LootSpawnRegion>(),
                new List<ThreatPatrolRoute>(),
                string.Empty);
            return zoneDefinition;
        }

        private static ZoneRuntime CreateRuntime(ZoneDefinition zoneDefinition)
        {
            var runtime = new GameObject("ZoneRuntime").AddComponent<ZoneRuntime>();
            runtime.EditorConfigure(zoneDefinition);
            return runtime;
        }

        private static TideController CreateTideController(ZoneRuntime runtime, ZoneDefinition zoneDefinition, Bounds bounds)
        {
            var controller = new GameObject("TideController").AddComponent<TideController>();
            controller.EditorConfigure(runtime, bounds, Vector3.zero);
            controller.Initialize(zoneDefinition);
            return controller;
        }

        private static PlayerController CreatePlayer()
        {
            var playerObject = new GameObject("Player");
            var characterController = playerObject.AddComponent<CharacterController>();
            characterController.height = 1.8f;
            characterController.center = new Vector3(0f, 0.9f, 0f);
            characterController.radius = 0.35f;
            playerObject.AddComponent<InteractionTracker>();
            return playerObject.AddComponent<PlayerController>();
        }

        private static MobileUIHUD CreateHud(PlayerController playerController)
        {
            var hudObject = new GameObject(
                "Hud",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster),
                typeof(MobileUIHUD));
            var hud = hudObject.GetComponent<MobileUIHUD>();
            hud.EditorConfigure(playerController);
            return hud;
        }

        private static ExtractionController CreateExtractionController(ZoneRuntime runtime, string pointId)
        {
            var extractionRoot = new GameObject($"ExtractionPoint_{pointId}");
            extractionRoot.AddComponent<SphereCollider>().isTrigger = true;
            extractionRoot.AddComponent<PlayerContextActionTarget>();
            var controller = extractionRoot.AddComponent<ExtractionController>();
            controller.EditorAssignRuntime(runtime);
            controller.EditorConfigure(pointId, extractionRoot.transform, null, null);
            return controller;
        }
    }
}
