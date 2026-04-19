#nullable enable

namespace ExtractionWeight.Core
{
    public interface IPlayerCarryInteractor
    {
        float CurrentHandlingMultiplier { get; }

        bool TryAddCarryItem(ILoadoutItem item);

        void AttachAmbientEffect(IAmbientEffect effect);
    }
}
