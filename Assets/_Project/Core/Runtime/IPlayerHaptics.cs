#nullable enable
using ExtractionWeight.Weight;

namespace ExtractionWeight.Core
{
    public interface IPlayerHaptics
    {
        void PlayFootstepTap();

        void PlayBreakpointPulse(CarryBreakpoint breakpoint);
    }
}
