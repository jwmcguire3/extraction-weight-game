#nullable enable
using System.Collections;
using System.Threading.Tasks;
using NUnit.Framework;
using ExtractionWeight.Zone;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace ExtractionWeight.Tests.PlayMode
{
    public class ZoneLoaderTests
    {
        private const string BootSceneName = "Boot";
        private const string DrydockZoneId = "drydock";

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            SceneManager.LoadScene(BootSceneName, LoadSceneMode.Single);
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            var loader = Object.FindFirstObjectByType<ZoneLoader>();
            if (loader != null)
            {
                var unloadTask = loader.UnloadCurrentZoneAsync();
                yield return WaitForTask(unloadTask);
                yield return null;
            }
        }

        [UnityTest]
        public IEnumerator DrydockDefinition_CanBeResolvedById()
        {
            var loader = GetLoader();

            Assert.That(loader.GetZoneDefinition(DrydockZoneId), Is.Not.Null);
            yield return null;
        }

        [UnityTest]
        public IEnumerator DrydockScene_LoadsAdditivelyWithinFiveSeconds()
        {
            var loader = GetLoader();
            var startTime = Time.realtimeSinceStartup;
            var loadTask = loader.LoadZoneByIdAsync(DrydockZoneId);

            yield return WaitForTask(loadTask);

            Assert.That(Time.realtimeSinceStartup - startTime, Is.LessThanOrEqualTo(5f));
            Assert.That(loader.LoadedZoneScene.HasValue, Is.True);
            Assert.That(loader.LoadedZoneScene!.Value.isLoaded, Is.True);
            Assert.That(loader.LoadedZoneScene!.Value.name, Is.EqualTo("Drydock"));
        }

        [UnityTest]
        public IEnumerator DrydockLoad_SpawnsFourExtractionPointMarkers()
        {
            var loader = GetLoader();
            var loadTask = loader.LoadZoneByIdAsync(DrydockZoneId);

            yield return WaitForTask(loadTask);
            yield return null;

            var markers = Object.FindObjectsByType<ZoneExtractionPointMarker>(FindObjectsSortMode.None);
            Assert.That(markers, Has.Length.EqualTo(4));
            Assert.That(loader.SpawnedMarkers, Has.Count.EqualTo(4));
        }

        [UnityTest]
        public IEnumerator Unload_CleansUpZoneSceneAndSpawnedMarkers()
        {
            var loader = GetLoader();
            var loadTask = loader.LoadZoneByIdAsync(DrydockZoneId);
            yield return WaitForTask(loadTask);

            var unloadTask = loader.UnloadCurrentZoneAsync();
            yield return WaitForTask(unloadTask);
            yield return null;

            var markers = Object.FindObjectsByType<ZoneExtractionPointMarker>(FindObjectsSortMode.None);
            Assert.That(markers, Is.Empty);
            Assert.That(loader.SpawnedMarkers, Is.Empty);
            Assert.That(loader.CurrentZoneDefinition, Is.Null);

            var drydockScene = SceneManager.GetSceneByName("Drydock");
            Assert.That(drydockScene.IsValid() && drydockScene.isLoaded, Is.False);
        }

        private static ZoneLoader GetLoader()
        {
            var loader = Object.FindFirstObjectByType<ZoneLoader>();
            Assert.That(loader, Is.Not.Null, "Boot scene should contain a ZoneLoader.");
            return loader!;
        }

        private static IEnumerator WaitForTask(Task task)
        {
            while (!task.IsCompleted)
            {
                yield return null;
            }

            if (task.IsFaulted)
            {
                Assert.Fail(task.Exception?.GetBaseException().Message ?? "Task failed.");
            }
        }

        private static IEnumerator WaitForTask<T>(Task<T> task)
        {
            while (!task.IsCompleted)
            {
                yield return null;
            }

            if (task.IsFaulted)
            {
                Assert.Fail(task.Exception?.GetBaseException().Message ?? "Task failed.");
            }
        }
    }
}
