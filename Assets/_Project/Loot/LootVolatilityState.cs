#nullable enable
using System;

namespace ExtractionWeight.Loot
{
    [Serializable]
    public sealed class LootVolatilityState
    {
        public LootVolatilityState(float elapsedCarryTimeSeconds = 0f)
        {
            ElapsedCarryTimeSeconds = Math.Max(0f, elapsedCarryTimeSeconds);
        }

        public float ElapsedCarryTimeSeconds { get; private set; }

        public void Advance(float deltaTimeSeconds)
        {
            if (deltaTimeSeconds <= 0f)
            {
                return;
            }

            ElapsedCarryTimeSeconds += deltaTimeSeconds;
        }
    }
}
