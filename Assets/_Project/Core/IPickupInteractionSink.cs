#nullable enable

namespace ExtractionWeight.Core
{
    public interface IPickupInteractionSink
    {
        void RegisterPickupCandidate(IPickupInteractable pickup);

        void UnregisterPickupCandidate(IPickupInteractable pickup);
    }
}
