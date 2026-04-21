#nullable enable
using ExtractionWeight.Core;
using NUnit.Framework;
using UnityEngine;

namespace ExtractionWeight.Tests.EditMode
{
    public class HealthSystemTests
    {
        [Test]
        public void Damage_ReducesHealthCorrectly()
        {
            var gameObject = new GameObject("HealthTestPlayer");
            var health = gameObject.AddComponent<PlayerHealth>();

            health.TakeDamage(25f, new TestThreat("warden"));

            Assert.That(health.CurrentHealth, Is.EqualTo(75f).Within(0.0001f));
            Object.DestroyImmediate(gameObject);
        }

        [Test]
        public void Regen_TriggersAfterFiveSeconds_AndCapsAtEightyPercent()
        {
            var gameObject = new GameObject("HealthTestPlayer");
            var health = gameObject.AddComponent<PlayerHealth>();

            health.TakeDamage(70f, new TestThreat("warden"));
            health.Tick(4.9f);
            Assert.That(health.CurrentHealth, Is.EqualTo(30f).Within(0.0001f));

            health.Tick(1f);
            Assert.That(health.CurrentHealth, Is.EqualTo(40f).Within(0.0001f));

            health.Tick(10f);
            Assert.That(health.CurrentHealth, Is.EqualTo(80f).Within(0.0001f));
            Object.DestroyImmediate(gameObject);
        }

        [Test]
        public void Death_FiresOnPlayerDeathOnce()
        {
            var gameObject = new GameObject("HealthTestPlayer");
            var health = gameObject.AddComponent<PlayerHealth>();
            var deathCount = 0;
            health.OnPlayerDeath += _ => deathCount++;

            health.TakeDamage(120f, new TestThreat("warden"));
            health.TakeDamage(10f, new TestThreat("warden"));

            Assert.That(health.IsDead, Is.True);
            Assert.That(deathCount, Is.EqualTo(1));
            Object.DestroyImmediate(gameObject);
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
