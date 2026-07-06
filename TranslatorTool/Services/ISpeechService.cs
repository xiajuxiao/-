namespace TranslatorTool.Services;

public interface ISpeechService
{
    Task SpeakAsync(string text, string language, CancellationToken cancellationToken);
}
