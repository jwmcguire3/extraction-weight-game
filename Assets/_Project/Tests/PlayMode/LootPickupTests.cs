#nullable enable
using System.Collections;
using ExtractionWeight.Core;
using ExtractionWeight.Loot;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace ExtractionWeight.Tests.PlayMode
{
    public class LootPickupTests
    {
        private Sprite _icon = null!;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
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
            ground.name = "LootPickupGround";
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            foreach (var root in Object.FindObjectsByType<GameObject>())
            {
                Object.Destroy(root);
            }

            yield return null;

            if (_icon != null)
            {
                Object.DestroyImmediate(_icon.texture);
                Object.DestroyImmediate(_icon);
            }
        }

        [UnityTest]
        public IEnumerator WalkingIntoPickup_TriggersEligibility()
        {
            var player = CreatePlayer();
            var pickup = CreatePickup(CreateDefinition("small", new CostSignature(0.05f, 0.02f, 0.02f, 0.02f), new Vector3(0.2f, 0.2f, 0.1f)), new Vector3(0f, 0f, 0f));

            player.transform.position = new Vector3(0f, 0.05f, 0f);
            yield return new WaitForFixedUpdate();
            yield return null;

            Assert.That(player.CurrentContextActionKind, Is.EqualTo(ContextActionKind.Pickup));
            Assert.That(player.CurrentContextActionLabel, Does.StartWith("Pickup"));
            Object.Destroy(pickup.gameObject);
            Object.Destroy(player.gameObject);
        }

        [UnityTest]
        public IEnumerator HoldingActionForRequiredDuration_CompletesPickup()
        {
            var player = CreatePlayer();
            var definition = CreateDefinition("small", new CostSignature(0.05f, 0.02f, 0.02f, 0.02f), new Vector3(0.2f, 0.2f, 0.1f));
            CreatePickup(definition, Vector3.zero);

            player.transform.position = new Vector3(0f, 0.05f, 0f);
            yield return new WaitForFixedUpdate();

            player.SetUiContextActionHeld(true);
            yield return new WaitForSeconds(0.35f);
            player.SetUiContextActionHeld(false);
            yield return null;

            Assert.That(player.CarryState.Items.Count, Is.EqualTo(1));
            Assert.That(player.CarryState.Items[0].ItemId, Is.EqualTo(definition.ItemId));
        }

        [UnityTest]
        public IEnumerator ReleasingEarly_CancelsWithoutAddingToCarry()
        {
            var player = CreatePlayer();
            CreatePickup(CreateDefinition("medium", new CostSignature(0.06f, 0.03f, 0.04f, 0.03f), new Vector3(0.5f, 0.4f, 0.2f)), Vector3.zero);

            player.transform.position = new Vector3(0f, 0.05f, 0f);
            yield return new WaitForFixedUpdate();

            player.SetUiContextActionHeld(true);
            yield return new WaitForSeconds(0.3f);
            player.SetUiContextActionHeld(false);
            yield return new WaitForSeconds(0.2f);

            Assert.That(player.CarryState.Items, Is.Empty);
        }

        [UnityTest]
        public IEnumerator PickupFailsAtSoftCeiling()
        {
            var player = CreatePlayer();
            var pickup = CreatePickup(CreateDefinition("large", new CostSignature(0.2f, 0f, 0f, 0f), new Vector3(0.9f, 0.7f, 0.6f)), Vector3.zero);
            player.CarryState.TryAdd(new SyntheticLoadoutItem("preload", new CostSignature(0.9f, 0.7f, 0f, 0f)));

            player.transform.position = new Vector3(0f, 0.05f, 0f);
            yield return new WaitForFixedUpdate();

            player.SetUiContextActionHeld(true);
            yield return new WaitForSeconds(1.6f);
            player.SetUiContextActionHeld(false);
            yield return null;

            Assert.That(player.CarryState.Items.Count, Is.EqualTo(1));
            Assert.That(player.CurrentHudMessage, Is.EqualTo("Pack full."));
            Assert.That(pickup.gameObject.activeSelf, Is.True);
        }

        [UnityTest]
        public IEnumerator VolatilePickup_AttachesAmbientEffectThatChangesPenalty()
        {
            var player = CreatePlayer();
            var definition = CreateDefinition(
                "volatile-leaking-battery",
                new CostSignature(0.1f, 0f, 0f, 0f),
                new Vector3(0.2f, 0.2f, 0.1f),
                isVolatile: true,
                ambientEffect: new AmbientAxisEffect(CostAxis.Noise, 0.05f),
                category: LootCategory.Volatile);
            CreatePickup(definition, Vector3.zero);

            player.transform.position = new Vector3(0f, 0.05f, 0f);
            yield return new WaitForFixedUpdate();

            player.SetUiContextActionHeld(true);
            yield return new WaitForSeconds(0.35f);
            player.SetUiContextActionHeld(false);
            yield return null;
            yield return null;

            Assert.That(player.CarryState.TotalCost.Noise, Is.EqualTo(0.15f).Within(0.0001f));
            Assert.That(player.CurrentPenalty.NoiseMultiplier, Is.GreaterThan(1f));
        }

        private PlayerController CreatePlayer()
        {
            var playerObject = new GameObject("LootTestPlayer");
            playerObject.transform.position = new Vector3(0f, 0.05f, -2f);

            var characterController = playerObject.AddComponent<CharacterController>();
            characterController.height = 1.8f;
            characterController.center = new Vector3(0f, 0.9f, 0f);
            characterController.radius = 0.35f;

            var physicsProbe = new GameObject("PhysicsProbe");
            physicsProbe.transform.SetParent(playerObject.transform, false);
            physicsProbe.transform.localPosition = new Vector3(0f, 0.9f, 0f);
            var rigidbody = physicsProbe.AddComponent<Rigidbody>();
            rigidbody.isKinematic = true;
            var collider = physicsProbe.AddComponent<CapsuleCollider>();
            collider.height = 1.8f;
            collider.radius = 0.35f;

            return playerObject.AddComponent<PlayerController>();
        }

        private LootPickup CreatePickup(LootDefinition definition, Vector3 position)
        {
            var pickupObject = new GameObject($"Pickup_{definition.ItemId}");
            pickupObject.transform.position = position;
            pickupObject.AddComponent<SphereCollider>().radius = 0.9f;
            var pickup = pickupObject.AddComponent<LootPickup>();
            pickup.Configure(definition);
            return pickup;
        }

        private LootDefinition CreateDefinition(
            string itemId,
            CostSignature baseCost,
            Vector3 size,
            bool isVolatile = false,
            AmbientAxisEffect ambientEffect = default,
            LootCategory category = LootCategory.Currency)
        {
            var definition = ScriptableObject.CreateInstance<LootDefinition>();
            definition.EditorSetData(
                itemId,
                itemId,
                _icon,
                category,
                baseCost,
                20f,
                isVolatile,
                size,
                null,
                null,
                ambientEffect);
            return definition;
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
    }
}
