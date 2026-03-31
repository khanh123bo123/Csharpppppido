namespace TourGuideApi.Services;

public interface ITextToSpeechService
{
    Task<string> SynthesizeAsync(string text, string fileName);
}
