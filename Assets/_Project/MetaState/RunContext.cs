#nullable enable

namespace ExtractionWeight.MetaState
{
    public sealed class RunContext
    {
        public RunContext(string zoneId, string zoneDisplayName, RunLoadoutSelection loadout)
        {
            ZoneId = zoneId;
            ZoneDisplayName = zoneDisplayName;
            Loadout = loadout;
        }

        public string ZoneId { get; }

        public string ZoneDisplayName { get; }

        public RunLoadoutSelection Loadout { get; }
    }
}
