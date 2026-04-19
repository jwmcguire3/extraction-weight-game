#nullable enable

namespace ExtractionWeight.Core
{
    public interface IAmbientEffect
    {
        CostAxis AffectedAxis { get; }

        CostSignature CurrentContribution { get; }

        void Tick(float deltaTimeSeconds);
    }
}
