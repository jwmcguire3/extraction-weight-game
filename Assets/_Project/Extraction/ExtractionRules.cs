#nullable enable
using ExtractionWeight.Core;
using ExtractionWeight.Loot;
using ExtractionWeight.Weight;
using ExtractionWeight.Zone;

namespace ExtractionWeight.Extraction
{
    public static class ExtractionRules
    {
        public static bool IsCarryCompatible(CarryState carryState, ItemSizeFilter itemSizeFilter)
        {
            for (var i = 0; i < carryState.Items.Count; i++)
            {
                if (!IsItemCompatible(carryState.Items[i], itemSizeFilter))
                {
                    return false;
                }
            }

            return true;
        }

        public static bool IsCarryWithinCapacity(CarryState carryState, float maxCapacityFraction)
        {
            return carryState.CapacityFraction <= maxCapacityFraction;
        }

        public static bool IsItemCompatible(ILoadoutItem item, ItemSizeFilter itemSizeFilter)
        {
            if (item is not LootItem lootItem)
            {
                return true;
            }

            var size = lootItem.Definition.GetSizeClass();
            return itemSizeFilter switch
            {
                ItemSizeFilter.AcceptsAll => true,
                ItemSizeFilter.SmallOnly => size == LootItemSize.Small,
                ItemSizeFilter.MediumAndSmaller => size != LootItemSize.Large,
                _ => true,
            };
        }

        public static string GetCompatibilityFailureMessage(ExtractionPointData pointData)
        {
            return pointData.ItemSizeFilter == ItemSizeFilter.SmallOnly
                ? "Large item doesn't fit"
                : "Extraction incompatible";
        }

        public static string GetCapacityFailureMessage(ExtractionType extractionType)
        {
            return extractionType == ExtractionType.Drone
                ? "Too heavy for drone pickup"
                : "Too heavy for extraction";
        }
    }
}
