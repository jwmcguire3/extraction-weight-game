#nullable enable
using System;

namespace ExtractionWeight.MetaState
{
    [Serializable]
    public sealed class StoredLootItem
    {
        public string ItemId = string.Empty;
        public int Count;
    }
}
