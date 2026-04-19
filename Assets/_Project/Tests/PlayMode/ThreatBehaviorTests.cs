#nullable enable
using System.Collections;
using ExtractionWeight.Core;
using ExtractionWeight.Threat;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace ExtractionWeight.Tests.PlayMode
{
    public class ThreatBehaviorTests
    {
        [UnitySetUp]
        public IEnumerator SetUp()
        {
            SceneManager.LoadScene("Boot", LoadSceneMode.Single);
            yield return null;

            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "TestGround";
            ground.transform.position = Vector3.zero;
            ground.transform.localScale = new Vector3(10f, 1f, 10f);

            var audioListenerObject = new GameObject("TestAudioListener");
            audioListenerObject.AddComponent<AudioListener>();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            var loader = Object.FindAnyObjectByType<Zone.ZoneLoader>();
            if (loader != null)
            {
                var unloadTask = loader.UnloadCurrentZoneAsync();
                while (!unloadTask.IsCompleted)
                {
                    yield return null;
                }
            }

            yield return null;
        }

        [UnityTest]
        public IEnumerator Warden_DetectsLoadedPlayerAtTwentyFiveMeters_ButNotLightPlayer()
        {
            var player = CreatePlayer(new Vector3(0f, 0.05f, 26f), loaded: false);
            var wardenObject = new GameObject("TestWarden");
            var warden = wardenObject.AddComponent<Warden>();
            warden.EditorAssignPlayer(player);
            yield return null;

            Assert.That(warden.CurrentState, Is.Not.EqualTo(DetectionState.Detected));

            AddSilhouetteLoad(player, 0.75f);
            yield return new WaitForSeconds(0.2f);

            Assert.That(warden.CurrentState, Is.EqualTo(DetectionState.Detected));

            Object.Destroy(wardenObject);
            Object.Destroy(player.gameObject);
        }

        [UnityTest]
        public IEnumerator Listener_WakesForLoudPlayer_ButIgnoresQuietOne()
        {
            var player = CreatePlayer(new Vector3(0f, 0.05f, 16f), loaded: false);
            var listenerObject = new GameObject("TestListener");
            var listener = listenerObject.AddComponent<Listener>();
            listener.EditorAssignPlayer(player);
            yield return null;

            Assert.That(listener.CurrentState, Is.Not.EqualTo(DetectionState.Detected));

            AddNoiseLoad(player, 0.8f);
            yield return new WaitForSeconds(0.2f);

            Assert.That(listener.CurrentState, Is.EqualTo(DetectionState.Detected));

            Object.Destroy(listenerObject);
            Object.Destroy(player.gameObject);
        }

        [UnityTest]
        public IEnumerator PursuitTiming_ThreatReachesPlayerWithinExpectedWindow()
        {
            var player = CreatePlayer(new Vector3(0f, 0.05f, 10f), loaded: true);
            AddNoiseLoad(player, 0.9f);
            var listenerObject = new GameObject("PursuitListener");
            var listener = listenerObject.AddComponent<Listener>();
            listener.EditorAssignPlayer(player);
            yield return null;

            var timeout = Time.time + 2f;
            while (Vector3.Distance(listener.transform.position, player.transform.position) > 0.6f && Time.time < timeout)
            {
                yield return null;
            }

            Assert.That(Vector3.Distance(listener.transform.position, player.transform.position), Is.LessThanOrEqualTo(0.6f));

            Object.Destroy(listenerObject);
            Object.Destroy(player.gameObject);
        }

        [UnityTest]
        public IEnumerator GiveUpBehavior_ReturnsToPatrolAndRest()
        {
            var player = CreatePlayer(new Vector3(0f, 0.05f, 10f), loaded: true);
            AddSilhouetteLoad(player, 0.8f);
            AddNoiseLoad(player, 0.8f);

            var wardenObject = new GameObject("GiveUpWarden");
            var routeRoot = new GameObject("Route");
            var waypointA = new GameObject("A").transform;
            waypointA.position = Vector3.zero;
            waypointA.SetParent(routeRoot.transform, false);
            var waypointB = new GameObject("B").transform;
            waypointB.position = new Vector3(5f, 0f, 0f);
            waypointB.SetParent(routeRoot.transform, false);

            var warden = wardenObject.AddComponent<Warden>();
            warden.EditorSetWaypoints(new[] { waypointA, waypointB });
            warden.EditorAssignPlayer(player);

            var listenerObject = new GameObject("GiveUpListener");
            listenerObject.transform.position = new Vector3(12f, 0f, 0f);
            var listener = listenerObject.AddComponent<Listener>();
            listener.EditorAssignPlayer(player);
            yield return null;

            Assert.That(warden.CurrentState, Is.EqualTo(DetectionState.Detected));
            Assert.That(listener.CurrentState, Is.EqualTo(DetectionState.Detected));

            var escapePosition = new Vector3(80f, 0.05f, 80f);
            player.transform.position = escapePosition;
            player.GetComponent<StaticPlayerAnchor>()?.SetAnchor(escapePosition);
            var timeout = Time.time + 8f;
            while ((warden.CurrentState == DetectionState.Detected || listener.CurrentState == DetectionState.Detected) &&
                   Time.time < timeout)
            {
                yield return null;
            }

            var patrolResumeTimeout = Time.time + 2f;
            while (!listener.IsResting && Time.time < patrolResumeTimeout)
            {
                yield return null;
            }

            var wardenResumePosition = warden.transform.position;
            yield return new WaitForSeconds(0.5f);

            Assert.That(warden.CurrentState, Is.EqualTo(DetectionState.Unaware));
            Assert.That(listener.CurrentState, Is.EqualTo(DetectionState.Unaware));
            Assert.That(listener.IsResting, Is.True);
            Assert.That(Vector3.Distance(wardenResumePosition, warden.transform.position), Is.GreaterThan(0.05f));

            Object.Destroy(routeRoot);
            Object.Destroy(wardenObject);
            Object.Destroy(listenerObject);
            Object.Destroy(player.gameObject);
        }

        private static PlayerController CreatePlayer(Vector3 position, bool loaded)
        {
            var playerObject = new GameObject("ThreatTestPlayer");
            playerObject.transform.position = position;
            var characterController = playerObject.AddComponent<CharacterController>();
            characterController.height = 1.8f;
            characterController.radius = 0.35f;
            characterController.center = new Vector3(0f, 0.9f, 0f);
            var playerController = playerObject.AddComponent<PlayerController>();
            var anchor = playerObject.AddComponent<StaticPlayerAnchor>();
            anchor.SetAnchor(position);
            if (loaded)
            {
                AddSilhouetteLoad(playerController, 0.7f);
            }

            return playerController;
        }

        private static void AddNoiseLoad(PlayerController player, float amount)
        {
            player.CarryState.TryAdd(new SyntheticLoadoutItem($"noise-{amount}", new CostSignature(amount, 0f, 0f, 0f)));
        }

        private static void AddSilhouetteLoad(PlayerController player, float amount)
        {
            player.CarryState.TryAdd(new SyntheticLoadoutItem($"silhouette-{amount}", new CostSignature(0f, amount, 0f, 0f)));
        }

        private sealed class SyntheticLoadoutItem : ILoadoutItem
        {
            public SyntheticLoadoutItem(string itemId, CostSignature baseCost)
            {
                ItemId = itemId;
                BaseCost = baseCost;
            }

            public string ItemId { get; }

            public CostSignature BaseCost { get; }

            public float Value => 0f;

            public bool IsVolatile => false;
        }

        private sealed class StaticPlayerAnchor : MonoBehaviour
        {
            private Vector3 _anchorPosition;

            public void SetAnchor(Vector3 anchorPosition)
            {
                _anchorPosition = anchorPosition;
                transform.position = anchorPosition;
            }

            private void LateUpdate()
            {
                transform.position = _anchorPosition;
            }
        }
    }
}
