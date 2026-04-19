#nullable enable
using UnityEngine;

namespace ExtractionWeight.Core
{
    public interface IAmbientLoadoutItem : ILoadoutItem
    {
        AudioClip? AmbientSound { get; }
    }
}
