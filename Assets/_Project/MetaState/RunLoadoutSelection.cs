#nullable enable

namespace ExtractionWeight.MetaState
{
    public sealed class RunLoadoutSelection
    {
        public RunLoadoutSelection(string loadoutId, string displayName, float startingCapacity)
        {
            LoadoutId = loadoutId;
            DisplayName = displayName;
            StartingCapacity = startingCapacity;
        }

        public string LoadoutId { get; }

        public string DisplayName { get; }

        public float StartingCapacity { get; }
    }
}
