#nullable enable

namespace ExtractionWeight.Core
{
    public interface IAmbientEffect
    {
        CostAxis AffectedAxis { get; }

        float AxisIncreasePerSecond { get; }
    }
}
