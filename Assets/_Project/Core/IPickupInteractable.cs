#nullable enable
using UnityEngine;

namespace ExtractionWeight.Core
{
    public interface IPickupInteractable
    {
        bool IsAvailable { get; }

        Vector3 WorldPosition { get; }

        float GetRequiredHoldDuration(IPlayerCarryInteractor player);

        bool TryCompletePickup(IPlayerCarryInteractor player, out string failureMessage);
    }
}
