#nullable enable
using System.Collections.Generic;
using ExtractionWeight.Core;
using ExtractionWeight.Loot;
using ExtractionWeight.Weight;

namespace ExtractionWeight.Extraction
{
    public static class Phase1RunResultStore
    {
        public static Phase1RunResult? LastRunResult { get; private set; }

        public static IReadOnlyList<string> PlayerStashItemIds => s_playerStashItemIds;

        private static readonly List<string> s_playerStashItemIds = new();

        public static Phase1RunResult CompleteSuccessfulExtraction(string zoneId, string pointId, string extractionType, CarryState carryState)
        {
            var runResult = new Phase1RunResult
            {
                WasSuccessful = true,
                ZoneId = zoneId,
                ExtractionPointId = pointId,
                ExtractionType = extractionType,
            };

            for (var i = 0; i < carryState.Items.Count; i++)
            {
                var item = carryState.Items[i];
                runResult.BankedItemIds.Add(item.ItemId);
                runResult.TotalBankedValue += item.Value;
                s_playerStashItemIds.Add(item.ItemId);
            }

            LastRunResult = runResult;
            carryState.Clear();
            return runResult;
        }

        public static Phase1RunResult CompleteFailedRun(string zoneId, CarryState carryState)
        {
            var runResult = new Phase1RunResult
            {
                WasSuccessful = false,
                ZoneId = zoneId,
            };

            LastRunResult = runResult;
            carryState.Clear();
            return runResult;
        }

        public static void Reset()
        {
            LastRunResult = null;
            s_playerStashItemIds.Clear();
        }
    }
}
