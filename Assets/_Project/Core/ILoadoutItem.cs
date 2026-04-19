#nullable enable

namespace ExtractionWeight.Core
{
    public interface ILoadoutItem
    {
        string ItemId { get; }

        CostSignature BaseCost { get; }

        float Value { get; }

        bool IsVolatile { get; }
    }
}
