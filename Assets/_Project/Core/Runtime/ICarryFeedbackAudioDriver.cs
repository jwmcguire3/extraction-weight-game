#nullable enable
using ExtractionWeight.Weight;
using UnityEngine;

namespace ExtractionWeight.Core
{
    public interface ICarryFeedbackAudioDriver
    {
        void PlayFootstep(CarryBreakpoint breakpoint, float capacityFraction);

        void HandleBreakpointCrossed(CarryBreakpoint previous, CarryBreakpoint current);

        void UpdateBreath(float volume, float pitch);

        void UpdateAmbient(AudioClip? clip);
    }
}
