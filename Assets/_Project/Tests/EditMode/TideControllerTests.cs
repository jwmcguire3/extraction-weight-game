#nullable enable
using System.Collections.Generic;
using ExtractionWeight.Weight;
using ExtractionWeight.Zone;
using NUnit.Framework;
using UnityEngine;

namespace ExtractionWeight.Tests.EditMode
{
    public sealed class TideControllerTests
    {
        private readonly List<Object> _createdObjects = new();

        [TearDown]
        public void TearDown()
        {
            for (var i = _createdObjects.Count - 1; i >= 0; i--)
            {
                if (_createdObjects[i] != null)
                {
                    Object.DestroyImmediate(_createdObjects[i]);
                }
            }

            _createdObjects.Clear();
        }

        [Test]
        public void TideProgressesLinearlyOverConfiguredDuration()
        {
            var zoneDefinition = CreateZoneDefinition(
                100f,
                new ExtractionPointData("A", ExtractionType.Standard, new Vector3(7f, 0f, 0f), 60f, ItemSizeFilter.AcceptsAll, 5f));
            var runtime = CreateRuntime(zoneDefinition);
            var controller = CreateController(runtime, zoneDefinition, new Bounds(Vector3.zero, new Vector3(20f, 4f, 20f)));

            controller.Advance(25f);
            Assert.That(controller.Progress, Is.EqualTo(0.25f).Within(0.0001f));

            controller.Advance(25f);
            Assert.That(controller.Progress, Is.EqualTo(0.5f).Within(0.0001f));
        }

        [Test]
        public void TideStateIsQueryableByPosition()
        {
            var zoneDefinition = CreateZoneDefinition(
                10f,
                new ExtractionPointData("A", ExtractionType.Standard, new Vector3(6f, 0f, 0f), 8f, ItemSizeFilter.AcceptsAll, 5f));
            var runtime = CreateRuntime(zoneDefinition);
            var controller = CreateController(runtime, zoneDefinition, new Bounds(Vector3.zero, new Vector3(20f, 4f, 20f)));

            controller.Advance(5f);

            Assert.That(controller.IsPointInsideTide(new Vector3(8f, 0f, 0f)), Is.True);
            Assert.That(controller.IsPointInsideTide(new Vector3(0f, 0f, 0f)), Is.False);
        }

        [Test]
        public void ExtractionPointsRegisterClosedWhenTideReachesThem()
        {
            var earlyPoint = new ExtractionPointData("A", ExtractionType.Standard, new Vector3(7f, 0f, 0f), 40f, ItemSizeFilter.AcceptsAll, 5f);
            var latePoint = new ExtractionPointData("B", ExtractionType.Standard, new Vector3(0f, 0f, 7f), 75f, ItemSizeFilter.AcceptsAll, 5f);
            var zoneDefinition = CreateZoneDefinition(100f, earlyPoint, latePoint);
            var runtime = CreateRuntime(zoneDefinition);
            var controller = CreateController(runtime, zoneDefinition, new Bounds(Vector3.zero, new Vector3(20f, 4f, 20f)));

            controller.Advance(41f);

            Assert.That(controller.IsExtractionClosed(earlyPoint), Is.True);
            Assert.That(runtime.IsExtractionOpen(earlyPoint.PointId), Is.False);
            Assert.That(runtime.IsExtractionOpen(latePoint.PointId), Is.True);
        }

        private ZoneDefinition CreateZoneDefinition(float tideDurationSeconds, params ExtractionPointData[] extractionPoints)
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
            _createdObjects.Add(zoneDefinition);
            return zoneDefinition;
        }

        private ZoneRuntime CreateRuntime(ZoneDefinition zoneDefinition)
        {
            var runtimeObject = new GameObject("ZoneRuntime");
            _createdObjects.Add(runtimeObject);
            var runtime = runtimeObject.AddComponent<ZoneRuntime>();
            runtime.EditorConfigure(zoneDefinition);
            return runtime;
        }

        private TideController CreateController(ZoneRuntime runtime, ZoneDefinition zoneDefinition, Bounds bounds)
        {
            var controllerObject = new GameObject("TideController");
            _createdObjects.Add(controllerObject);
            var controller = controllerObject.AddComponent<TideController>();
            controller.EditorConfigure(runtime, bounds, Vector3.zero);
            controller.Initialize(zoneDefinition);
            return controller;
        }
    }
}
