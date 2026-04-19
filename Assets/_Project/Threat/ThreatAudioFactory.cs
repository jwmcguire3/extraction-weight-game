#nullable enable
using UnityEngine;

namespace ExtractionWeight.Threat
{
    internal static class ThreatAudioFactory
    {
        private const int SampleRate = 22050;

        public static AudioClip CreateMechanicalHum(string clipName)
        {
            return CreateClip(clipName, 1.2f, sampleIndex =>
            {
                var t = sampleIndex / (float)SampleRate;
                var baseWave = Mathf.Sin(2f * Mathf.PI * 110f * t) * 0.35f;
                var overtone = Mathf.Sin(2f * Mathf.PI * 220f * t) * 0.1f;
                var pulse = Mathf.Sin(2f * Mathf.PI * 3f * t) * 0.05f;
                return baseWave + overtone + pulse;
            });
        }

        public static AudioClip CreateMechanicalAlert(string clipName)
        {
            return CreateClip(clipName, 0.55f, sampleIndex =>
            {
                var t = sampleIndex / (float)SampleRate;
                var tone = Mathf.Sin(2f * Mathf.PI * Mathf.Lerp(540f, 720f, t / 0.55f) * t) * 0.45f;
                var pulse = Mathf.Sign(Mathf.Sin(2f * Mathf.PI * 8f * t)) * 0.1f;
                return tone + pulse;
            });
        }

        public static AudioClip CreateShuffleLoop(string clipName)
        {
            return CreateClip(clipName, 1f, sampleIndex =>
            {
                var t = sampleIndex / (float)SampleRate;
                var noise = Mathf.PerlinNoise(t * 13f, 0.5f) - 0.5f;
                var rhythm = Mathf.Max(0f, Mathf.Sin(2f * Mathf.PI * 2f * t)) * 0.18f;
                return (noise * 0.2f) + rhythm;
            });
        }

        public static AudioClip CreateChargeCue(string clipName)
        {
            return CreateClip(clipName, 0.4f, sampleIndex =>
            {
                var t = sampleIndex / (float)SampleRate;
                var sweep = Mathf.Sin(2f * Mathf.PI * Mathf.Lerp(180f, 360f, t / 0.4f) * t) * 0.5f;
                var grit = (Mathf.PerlinNoise(t * 27f, 0.3f) - 0.5f) * 0.3f;
                return sweep + grit;
            });
        }

        private static AudioClip CreateClip(string clipName, float durationSeconds, System.Func<int, float> sampleFactory)
        {
            var sampleCount = Mathf.CeilToInt(durationSeconds * SampleRate);
            var samples = new float[sampleCount];
            for (var i = 0; i < sampleCount; i++)
            {
                samples[i] = Mathf.Clamp(sampleFactory(i), -1f, 1f);
            }

            var clip = AudioClip.Create(clipName, sampleCount, 1, SampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }
    }
}
