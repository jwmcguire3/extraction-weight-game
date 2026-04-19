#nullable enable

namespace ExtractionWeight.Core
{
    public interface IThreat
    {
        string ThreatId { get; }

        DetectionProfile Profile { get; }
    }
}
