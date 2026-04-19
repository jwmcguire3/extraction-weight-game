#nullable enable
using System;
using ExtractionWeight.Core;

namespace ExtractionWeight.Loot
{
    public sealed class LootItem : ILoadoutItem
    {
        public LootItem(LootDefinition definition, LootVolatilityState? volatilityState = null)
        {
            Definition = definition ?? throw new ArgumentNullException(nameof(definition));
            VolatilityState = volatilityState ?? (definition.IsVolatile ? new LootVolatilityState() : null);
        }

        public LootDefinition Definition { get; }

        public LootVolatilityState? VolatilityState { get; }

        public string ItemId => Definition.ItemId;

        public CostSignature BaseCost => Definition.BaseCost;

        public float Value => Definition.Value;

        public bool IsVolatile => Definition.IsVolatile;

        public AmbientAxisEffect AmbientEffect => Definition.AmbientEffect;
    }
}
