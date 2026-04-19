#nullable enable
using System;
using System.Collections.Generic;
using ExtractionWeight.Core;
using UnityEngine;

namespace ExtractionWeight.Loot
{
    [CreateAssetMenu(fileName = "LootDatabase", menuName = "Extraction Weight/Loot/Loot Database")]
    public sealed class LootDatabase : ScriptableObject
    {
        public const string AssetPath = "Assets/_Project/Data/Loot/LootDatabase.asset";

        private static LootDatabase? _instance;

        [SerializeField]
        private List<LootDefinition> _definitions = new();

        private Dictionary<string, LootDefinition>? _definitionsById;
        private Dictionary<LootCategory, List<LootDefinition>>? _definitionsByCategory;

        public static LootDatabase? Instance => _instance;

        public IReadOnlyList<LootDefinition> Definitions => _definitions;

        public LootDefinition GetById(string itemId)
        {
            if (string.IsNullOrWhiteSpace(itemId))
            {
                throw new ArgumentException("Item id is required.", nameof(itemId));
            }

            RebuildLookupCaches();
            if (_definitionsById is null || !_definitionsById.TryGetValue(itemId.Trim(), out var definition))
            {
                throw new KeyNotFoundException($"No loot definition with id '{itemId}' exists in the database.");
            }

            return definition;
        }

        public IReadOnlyList<LootDefinition> GetByCategory(LootCategory category)
        {
            RebuildLookupCaches();
            if (_definitionsByCategory is null || !_definitionsByCategory.TryGetValue(category, out var definitions))
            {
                return Array.Empty<LootDefinition>();
            }

            return definitions;
        }

        public IReadOnlyList<string> Validate()
        {
            var errors = new List<string>();
            var seenIds = new HashSet<string>(StringComparer.Ordinal);

            for (var i = 0; i < _definitions.Count; i++)
            {
                var definition = _definitions[i];
                if (definition is null)
                {
                    errors.Add($"Definitions[{i}] is missing a LootDefinition reference.");
                    continue;
                }

                if (string.IsNullOrWhiteSpace(definition.ItemId))
                {
                    errors.Add($"{definition.name}: ItemId is required.");
                }
                else if (!seenIds.Add(definition.ItemId))
                {
                    errors.Add($"{definition.name}: duplicate ItemId '{definition.ItemId}'.");
                }

                if (string.IsNullOrWhiteSpace(definition.DisplayName))
                {
                    errors.Add($"{definition.name}: DisplayName is required.");
                }

                if (definition.Icon is null)
                {
                    errors.Add($"{definition.name}: Icon is required.");
                }

                if (definition.TotalBaseCost <= CostSignature.EqualityEpsilon)
                {
                    errors.Add($"{definition.name}: BaseCost must contribute non-zero load.");
                }

                if (definition.Value <= 0f)
                {
                    errors.Add($"{definition.name}: Value must be greater than zero.");
                }

                if (definition.PhysicalSize.x <= 0f || definition.PhysicalSize.y <= 0f || definition.PhysicalSize.z <= 0f)
                {
                    errors.Add($"{definition.name}: PhysicalSize must be greater than zero on all axes.");
                }

                if (definition.Category == LootCategory.Volatile && !definition.IsVolatile)
                {
                    errors.Add($"{definition.name}: Volatile category items must set IsVolatile.");
                }

                if (definition.IsVolatile && !definition.AmbientEffect.IsConfigured)
                {
                    errors.Add($"{definition.name}: Volatile items must define an ambient axis increase.");
                }

                if (!definition.IsVolatile && definition.AmbientEffect.IsConfigured)
                {
                    errors.Add($"{definition.name}: Non-volatile items must not define an ambient axis increase.");
                }
            }

            return errors;
        }

#if UNITY_EDITOR
        public void EditorSetDefinitions(IReadOnlyList<LootDefinition> definitions)
        {
            _definitions = new List<LootDefinition>(definitions.Count);
            for (var i = 0; i < definitions.Count; i++)
            {
                _definitions.Add(definitions[i]);
            }

            RebuildLookupCaches();
            UnityEditor.EditorUtility.SetDirty(this);
        }
#endif

        private void OnEnable()
        {
            _instance = this;
            RebuildLookupCaches();
        }

        private void OnValidate()
        {
            RebuildLookupCaches();
        }

        private void RebuildLookupCaches()
        {
            _definitionsById = new Dictionary<string, LootDefinition>(StringComparer.Ordinal);
            _definitionsByCategory = new Dictionary<LootCategory, List<LootDefinition>>();

            for (var i = 0; i < _definitions.Count; i++)
            {
                var definition = _definitions[i];
                if (definition is null || string.IsNullOrWhiteSpace(definition.ItemId))
                {
                    continue;
                }

                if (!_definitionsById.ContainsKey(definition.ItemId))
                {
                    _definitionsById.Add(definition.ItemId, definition);
                }

                if (!_definitionsByCategory.TryGetValue(definition.Category, out var bucket))
                {
                    bucket = new List<LootDefinition>();
                    _definitionsByCategory.Add(definition.Category, bucket);
                }

                bucket.Add(definition);
            }
        }
    }
}
