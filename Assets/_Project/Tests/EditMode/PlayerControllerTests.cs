#nullable enable
using ExtractionWeight.Core;
using ExtractionWeight.Weight;
using NUnit.Framework;

namespace ExtractionWeight.Tests.EditMode
{
    public class PlayerControllerTests
    {
        [Test]
        public void CalculateSpeed_UsesExpectedMobilityMultiplier()
        {
            Assert.That(
                PlayerController.CalculateSpeed(4f, 7f, 1f, isSprinting: false, isCrouched: false, crouchSpeedMultiplier: 0.65f),
                Is.EqualTo(4f).Within(0.0001f));

            Assert.That(
                PlayerController.CalculateSpeed(4f, 7f, 0.8f, isSprinting: true, isCrouched: false, crouchSpeedMultiplier: 0.65f),
                Is.EqualTo(5.6f).Within(0.0001f));

            Assert.That(
                PlayerController.CalculateSpeed(4f, 7f, 0.6f, isSprinting: false, isCrouched: true, crouchSpeedMultiplier: 0.65f),
                Is.EqualTo(1.56f).Within(0.0001f));
        }

        [Test]
        public void StaminaDrainAndRegen_FollowsConfiguredRates()
        {
            var drainRate = PlayerController.GetSprintDrainPerSecond(18f, 0.75f, 1f);
            var depleted = PlayerController.UpdateStamina(100f, 2f, isSprinting: true, drainRate, regenPerSecond: 16f, maxStamina: 100f);
            var regenerated = PlayerController.UpdateStamina(depleted, 1.5f, isSprinting: false, drainRate, regenPerSecond: 16f, maxStamina: 100f);

            Assert.That(drainRate, Is.EqualTo(31.5f).Within(0.0001f));
            Assert.That(depleted, Is.EqualTo(37f).Within(0.0001f));
            Assert.That(regenerated, Is.EqualTo(61f).Within(0.0001f));
        }

        [TestCase(CarryBreakpoint.Light, false, 10f, true)]
        [TestCase(CarryBreakpoint.Loaded, false, 10f, true)]
        [TestCase(CarryBreakpoint.Overburdened, false, 10f, false)]
        [TestCase(CarryBreakpoint.SoftCeiling, false, 10f, false)]
        [TestCase(CarryBreakpoint.Light, true, 10f, false)]
        [TestCase(CarryBreakpoint.Light, false, 0f, false)]
        public void CanSprint_RespectsBreakpointAndState(CarryBreakpoint breakpoint, bool isCrouched, float stamina, bool expected)
        {
            Assert.That(PlayerController.CanSprint(breakpoint, isCrouched, stamina), Is.EqualTo(expected));
        }
    }
}
