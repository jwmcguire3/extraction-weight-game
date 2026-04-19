#nullable enable

namespace ExtractionWeight.Weight
{
    public readonly struct AppliedPenalty
    {
        public AppliedPenalty(float noiseMultiplier, float silhouetteMultiplier, float handlingMultiplier, float mobilityMultiplier)
        {
            NoiseMultiplier = noiseMultiplier;
            SilhouetteMultiplier = silhouetteMultiplier;
            HandlingMultiplier = handlingMultiplier;
            MobilityMultiplier = mobilityMultiplier;
        }

        public float NoiseMultiplier { get; }
        public float SilhouetteMultiplier { get; }
        public float HandlingMultiplier { get; }
        public float MobilityMultiplier { get; }
    }
}
