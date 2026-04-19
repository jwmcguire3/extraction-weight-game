#nullable enable
using System.Collections;
using ExtractionWeight.Core;
using ExtractionWeight.Weight;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace ExtractionWeight.Tests.PlayMode
{
    public class PlayerMovementFeelTests
    {
        private const float DistanceToTravel = 10f;
        private const float TolerancePercent = 0.05f;

        [UnityTest]
        public IEnumerator TravelTimeOverTenMeters_MatchesExpectedSpeedAcrossWeightStates()
        {
            yield return RunTravelCase(0.2f, expectSprint: true);
            yield return RunTravelCase(0.6f, expectSprint: true);
            yield return RunTravelCase(0.9f, expectSprint: false);
        }

        [UnityTest]
        public IEnumerator BreakpointCrossing_FiresExpectedFeedbackEvents()
        {
            CreateGround();
            var player = CreatePlayer(new Vector3(0f, 0.05f, 0f), includeFeedbackController: true, out _, out var feedbackController);
            var audioDriver = new TestAudioDriver();
            var haptics = new TestHaptics();
            Assert.That(feedbackController, Is.Not.Null);
            feedbackController!.AudioDriverOverride = audioDriver;
            feedbackController.HapticsOverride = haptics;

            yield return null;

            player.DebugApplyMobilityLoad(0.2f);
            yield return null;
            player.DebugApplyMobilityLoad(0.6f);
            yield return null;
            player.DebugApplyMobilityLoad(0.9f);
            yield return null;

            player.SetUiMoveInput(Vector2.up);
            player.SetUiSprintHeld(true);

            yield return new WaitForSeconds(1.2f);

            Assert.That(audioDriver.BreakpointCrossCount, Is.EqualTo(2));
            Assert.That(haptics.BreakpointPulseCount, Is.EqualTo(2));
            Assert.That(audioDriver.FootstepCount, Is.GreaterThanOrEqualTo(1));
            Assert.That(haptics.FootstepTapCount, Is.GreaterThanOrEqualTo(1));
        }

        private static IEnumerator RunTravelCase(float capacityFraction, bool expectSprint)
        {
            CreateGround();
            var player = CreatePlayer(new Vector3(0f, 0.05f, 0f), includeFeedbackController: false, out var characterController, out _);

            player.DebugApplyMobilityLoad(capacityFraction);
            player.SetUiMoveInput(Vector2.up);
            player.SetUiSprintHeld(true);

            yield return null;
            yield return new WaitUntil(() => characterController.isGrounded);

            var startPosition = player.transform.position;
            var startTime = Time.fixedTime;
            var expectedSpeed = PlayerController.CalculateSpeed(
                player.WalkSpeed,
                player.SprintSpeed,
                player.CurrentPenalty.MobilityMultiplier,
                isSprinting: expectSprint,
                isCrouched: false,
                crouchSpeedMultiplier: 0.65f);

            yield return new WaitUntil(() => Vector3.Distance(startPosition, player.transform.position) >= DistanceToTravel);

            var actualTravelTime = Time.fixedTime - startTime;
            var expectedTravelTime = DistanceToTravel / expectedSpeed;

            Assert.That(
                actualTravelTime,
                Is.EqualTo(expectedTravelTime).Within(expectedTravelTime * TolerancePercent),
                $"Expected travel time near {expectedTravelTime:F3}s for load {capacityFraction:F2}, but observed {actualTravelTime:F3}s.");
        }

        private static void CreateGround()
        {
            var existingGround = GameObject.Find("TestGround");
            if (existingGround != null)
            {
                Object.Destroy(existingGround);
            }

            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "TestGround";
            ground.transform.position = Vector3.zero;
            ground.transform.localScale = new Vector3(5f, 1f, 5f);
        }

        private static PlayerController CreatePlayer(
            Vector3 position,
            bool includeFeedbackController,
            out CharacterController characterController,
            out CarryFeedbackController? feedbackController)
        {
            var existingPlayer = GameObject.Find("TestPlayer");
            if (existingPlayer != null)
            {
                Object.Destroy(existingPlayer);
            }

            var playerObject = new GameObject("TestPlayer");
            playerObject.transform.position = position;

            characterController = playerObject.AddComponent<CharacterController>();
            characterController.height = 1.8f;
            characterController.center = new Vector3(0f, 0.9f, 0f);
            characterController.radius = 0.35f;

            var playerController = playerObject.AddComponent<PlayerController>();
            feedbackController = includeFeedbackController ? playerObject.AddComponent<CarryFeedbackController>() : null;
            return playerController;
        }

        private sealed class TestAudioDriver : ICarryFeedbackAudioDriver
        {
            public int FootstepCount { get; private set; }

            public int BreakpointCrossCount { get; private set; }

            public void PlayFootstep(CarryBreakpoint breakpoint, float capacityFraction)
            {
                FootstepCount++;
            }

            public void HandleBreakpointCrossed(CarryBreakpoint previous, CarryBreakpoint current)
            {
                BreakpointCrossCount++;
            }

            public void UpdateBreath(float volume, float pitch)
            {
            }

            public void UpdateAmbient(AudioClip? clip)
            {
            }
        }

        private sealed class TestHaptics : IPlayerHaptics
        {
            public int FootstepTapCount { get; private set; }

            public int BreakpointPulseCount { get; private set; }

            public void PlayFootstepTap()
            {
                FootstepTapCount++;
            }

            public void PlayBreakpointPulse(CarryBreakpoint breakpoint)
            {
                BreakpointPulseCount++;
            }
        }
    }
}
