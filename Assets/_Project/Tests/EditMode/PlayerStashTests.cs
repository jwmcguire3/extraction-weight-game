#nullable enable
using System.Collections.Generic;
using ExtractionWeight.Core;
using ExtractionWeight.Loot;
using ExtractionWeight.MetaState;
using NUnit.Framework;
using UnityEngine;

namespace ExtractionWeight.Tests.EditMode
{
    public class PlayerStashTests
    {
        private Sprite _icon = null!;

        [SetUp]
        public void SetUp()
        {
            PlayerStash.ResetSingletonForTests();
            PlayerStash.SetItemValueResolver(itemId => itemId switch
            {
                "cash-roll" => 20f,
                "data-chip" => 35f,
                _ => 0f,
            });

            var texture = new Texture2D(4, 4, TextureFormat.RGBA32, false);
            texture.SetPixels(new[]
            {
                Color.white, Color.white, Color.white, Color.white,
                Color.white, Color.white, Color.white, Color.white,
                Color.white, Color.white, Color.white, Color.white,
                Color.white, Color.white, Color.white, Color.white,
            });
            texture.Apply();
            _icon = Sprite.Create(texture, new Rect(0f, 0f, 4f, 4f), new Vector2(0.5f, 0.5f));
        }

        [TearDown]
        public void TearDown()
        {
            if (_icon != null)
            {
                Object.DestroyImmediate(_icon.texture);
                Object.DestroyImmediate(_icon);
            }

            PlayerStash.ResetSingletonForTests();
        }

        [Test]
        public void BankingItems_AddsCountsAndPreservesTotalValue()
        {
            var stash = PlayerStash.CreateTransient();
            stash.BankItems(new List<LootItem>
            {
                CreateLootItem("cash-roll", 20f),
                CreateLootItem("cash-roll", 20f),
                CreateLootItem("data-chip", 35f),
            });

            Assert.That(stash.Items, Has.Count.EqualTo(2));
            Assert.That(stash.TotalValue, Is.EqualTo(75f).Within(0.0001f));
            Assert.That(stash.Items[0].ItemId, Is.EqualTo("cash-roll"));
            Assert.That(stash.Items[0].Count, Is.EqualTo(2));
        }

        [Test]
        public void Clearing_RemovesAllItems()
        {
            var stash = PlayerStash.CreateTransient();
            stash.BankItems(new List<LootItem> { CreateLootItem("cash-roll", 20f) });

            stash.ClearItems();

            Assert.That(stash.Items, Is.Empty);
            Assert.That(stash.TotalValue, Is.EqualTo(0f));
        }

        [Test]
        public void PersistenceRoundTrip_ViaJsonRestoresContents()
        {
            var original = PlayerStash.CreateTransient();
            original.BankItems(new List<LootItem>
            {
                CreateLootItem("cash-roll", 20f),
                CreateLootItem("data-chip", 35f),
                CreateLootItem("data-chip", 35f),
            });

            var json = original.ToJson();
            var reloaded = PlayerStash.CreateTransient();
            reloaded.LoadFromJson(json);

            Assert.That(reloaded.Items, Has.Count.EqualTo(2));
            Assert.That(reloaded.TotalValue, Is.EqualTo(90f).Within(0.0001f));
        }

        private LootItem CreateLootItem(string itemId, float value)
        {
            var definition = ScriptableObject.CreateInstance<LootDefinition>();
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

            return new LootItem(definition);
        }
    }
}
