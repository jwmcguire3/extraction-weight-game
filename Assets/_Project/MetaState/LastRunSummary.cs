#nullable enable
using System.Collections.Generic;

namespace ExtractionWeight.MetaState
{
    public sealed class LastRunSummary
    {
        public bool WasSuccessful { get; set; }

        public string ZoneId { get; set; } = string.Empty;

        public string ZoneDisplayName { get; set; } = string.Empty;

        public float DurationSeconds { get; set; }

        public float TotalBankedValue { get; set; }

        public float LostLootValue { get; set; }

        public IReadOnlyList<StoredLootItem> BankedItems { get; set; } = System.Array.Empty<StoredLootItem>();
    }
}
