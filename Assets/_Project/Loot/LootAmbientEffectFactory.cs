#nullable enable
using System;
using ExtractionWeight.Core;
using UnityEngine;

namespace ExtractionWeight.Loot
{
    public static class LootAmbientEffectFactory
    {
        public static IAmbientEffect CreateFor(LootItem item)
        {
            if (item is null)
            {
                throw new ArgumentNullException(nameof(item));
            }

            return item.ItemId switch
            {
                "volatile-leaking-battery" => new ConstantAmbientEffect(new AmbientAxisEffect(CostAxis.Noise, 0.05f)),
                "volatile-caged-bird" => new CagedBirdAmbientEffect(item.ItemId),
                _ => new ConstantAmbientEffect(item.AmbientEffect),
            };
        }

        private sealed class ConstantAmbientEffect : IAmbientEffect
        {
            public ConstantAmbientEffect(AmbientAxisEffect effect)
            {
                AffectedAxis = effect.AffectedAxis;
                CurrentContribution = effect.ToContribution();
            }

            public CostAxis AffectedAxis { get; }

            public CostSignature CurrentContribution { get; private set; }

            public void Tick(float deltaTimeSeconds)
            {
            }
        }

        private sealed class CagedBirdAmbientEffect : IAmbientEffect
        {
            private const float BaseNoise = 0.03f;
            private const float SpikeNoise = 0.2f;
            private const float SpikeDurationSeconds = 1.5f;

            private readonly System.Random _random;
            private float _secondsUntilNextSpike;
            private float _spikeSecondsRemaining;

            public CagedBirdAmbientEffect(string seedKey)
            {
                AffectedAxis = CostAxis.Noise;
                _random = new System.Random(StringComparer.Ordinal.GetHashCode(seedKey));
                ScheduleNextSpike();
                CurrentContribution = new CostSignature(BaseNoise, 0f, 0f, 0f);
            }

            public CostAxis AffectedAxis { get; }

            public CostSignature CurrentContribution { get; private set; }

            public void Tick(float deltaTimeSeconds)
            {
                if (deltaTimeSeconds <= 0f)
                {
                    return;
                }

                if (_spikeSecondsRemaining > 0f)
                {
                    _spikeSecondsRemaining = Math.Max(0f, _spikeSecondsRemaining - deltaTimeSeconds);
                }
                else
                {
                    _secondsUntilNextSpike -= deltaTimeSeconds;
                    if (_secondsUntilNextSpike <= 0f)
                    {
                        _spikeSecondsRemaining = SpikeDurationSeconds;
                        ScheduleNextSpike();
                    }
                }

                var noise = _spikeSecondsRemaining > 0f ? BaseNoise + SpikeNoise : BaseNoise;
                CurrentContribution = new CostSignature(noise, 0f, 0f, 0f);
            }

            private void ScheduleNextSpike()
            {
                _secondsUntilNextSpike = Mathf.Lerp(15f, 40f, (float)_random.NextDouble());
            }
        }
    }
}
