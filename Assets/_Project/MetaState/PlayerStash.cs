#nullable enable
using System;
using System.Collections.Generic;
using ExtractionWeight.Loot;
using UnityEngine;

namespace ExtractionWeight.MetaState
{
    public sealed class PlayerStash : ScriptableObject
    {
        private const string PlayerPrefsKey = "ExtractionWeight.PlayerStash.Phase1";

        private static PlayerStash? s_instance;
        private static Func<string, float>? s_itemValueResolver;

        [SerializeField]
        private List<StoredLootItem> _items = new();

        public IReadOnlyList<StoredLootItem> Items => _items;

        public float TotalValue
        {
            get
            {
                var total = 0f;
                for (var i = 0; i < _items.Count; i++)
                {
                    var item = _items[i];
                    total += ResolveItemValue(item.ItemId) * item.Count;
                }

                return total;
            }
        }

        public static PlayerStash Instance
        {
            get
            {
                s_instance ??= CreateAndLoad();
                return s_instance;
            }
        }

        public static void SetItemValueResolver(Func<string, float>? resolver)
        {
            s_itemValueResolver = resolver;
        }

        public void BankItems(List<LootItem> items)
        {
            if (items == null)
            {
                throw new ArgumentNullException(nameof(items));
            }

            var countsById = new Dictionary<string, int>(StringComparer.Ordinal);
            for (var i = 0; i < _items.Count; i++)
            {
                countsById[_items[i].ItemId] = _items[i].Count;
            }

            for (var i = 0; i < items.Count; i++)
            {
                var itemId = items[i].ItemId;
                countsById.TryGetValue(itemId, out var currentCount);
                countsById[itemId] = currentCount + 1;
            }

            _items.Clear();
            foreach (var pair in countsById)
            {
                _items.Add(new StoredLootItem
                {
                    ItemId = pair.Key,
                    Count = pair.Value,
                });
            }

            _items.Sort((left, right) => string.Compare(left.ItemId, right.ItemId, StringComparison.Ordinal));
            Save();
        }

        public void ClearItems()
        {
            _items.Clear();
            Save();
        }

        public string ToJson()
        {
            return JsonUtility.ToJson(new PlayerStashSaveData
            {
                Items = _items,
            });
        }

        public void LoadFromJson(string json)
        {
            _items.Clear();
            if (string.IsNullOrWhiteSpace(json))
            {
                return;
            }

            var saveData = JsonUtility.FromJson<PlayerStashSaveData>(json);
            if (saveData?.Items == null)
            {
                return;
            }

            for (var i = 0; i < saveData.Items.Count; i++)
            {
                var item = saveData.Items[i];
                if (item == null || string.IsNullOrWhiteSpace(item.ItemId) || item.Count <= 0)
                {
                    continue;
                }

                _items.Add(new StoredLootItem
                {
                    ItemId = item.ItemId,
                    Count = item.Count,
                });
            }
        }

        public void Save()
        {
            PlayerPrefs.SetString(PlayerPrefsKey, ToJson());
            PlayerPrefs.Save();
        }

        public void Reload()
        {
            LoadFromJson(PlayerPrefs.GetString(PlayerPrefsKey, string.Empty));
        }

        public static PlayerStash CreateTransient()
        {
            var stash = CreateInstance<PlayerStash>();
            stash.hideFlags = HideFlags.HideAndDontSave;
            return stash;
        }

        public static void ResetSingletonForTests()
        {
            if (s_instance != null)
            {
                DestroyImmediate(s_instance);
                s_instance = null;
            }

            PlayerPrefs.DeleteKey(PlayerPrefsKey);
            PlayerPrefs.Save();
            s_itemValueResolver = null;
        }

        private static PlayerStash CreateAndLoad()
        {
            var stash = CreateTransient();
            stash.Reload();
            return stash;
        }

        private static float ResolveItemValue(string itemId)
        {
            return s_itemValueResolver?.Invoke(itemId) ?? 0f;
        }

        [Serializable]
        private sealed class PlayerStashSaveData
        {
            public List<StoredLootItem> Items = new();
        }
    }
}
