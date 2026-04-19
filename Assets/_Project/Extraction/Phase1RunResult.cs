#nullable enable
using System;
using System.Collections.Generic;

namespace ExtractionWeight.Extraction
{
    [Serializable]
    public sealed class Phase1RunResult
    {
        public bool WasSuccessful;
        public string ZoneId = string.Empty;
        public string ExtractionPointId = string.Empty;
        public string ExtractionType = string.Empty;
        public float TotalBankedValue;
        public List<string> BankedItemIds = new();
    }
}
