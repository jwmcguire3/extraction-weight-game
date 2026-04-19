#nullable enable
using System;

namespace ExtractionWeight.Extraction
{
    public sealed class ExtractionStateMachine
    {
        public ExtractionPhaseState State { get; private set; } = ExtractionPhaseState.Idle;

        public static bool CanTransition(ExtractionPhaseState from, ExtractionPhaseState to)
        {
            return from switch
            {
                ExtractionPhaseState.Idle => to is ExtractionPhaseState.Initiation,
                ExtractionPhaseState.Initiation => to is ExtractionPhaseState.Approach or ExtractionPhaseState.Hold or ExtractionPhaseState.Idle,
                ExtractionPhaseState.Approach => to is ExtractionPhaseState.Hold or ExtractionPhaseState.Departure or ExtractionPhaseState.Idle,
                ExtractionPhaseState.Hold => to is ExtractionPhaseState.Departure or ExtractionPhaseState.Idle,
                ExtractionPhaseState.Departure => to is ExtractionPhaseState.Completed,
                ExtractionPhaseState.Completed => false,
                _ => false,
            };
        }

        public void TransitionTo(ExtractionPhaseState nextState)
        {
            if (!CanTransition(State, nextState))
            {
                throw new InvalidOperationException($"Invalid extraction state transition from {State} to {nextState}.");
            }

            State = nextState;
        }

        public void ResetToIdle()
        {
            State = ExtractionPhaseState.Idle;
        }
    }
}
