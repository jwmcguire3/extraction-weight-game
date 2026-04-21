#nullable enable

namespace ExtractionWeight.Core
{
    public interface IPlayerDeathPresentation
    {
        float GetPresentationDurationSeconds();

        void Play(float lostLootValue);
    }
}
