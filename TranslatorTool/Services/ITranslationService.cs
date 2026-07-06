using TranslatorTool.Models;

namespace TranslatorTool.Services;

public interface ITranslationService
{
    Task<TranslationResult> TranslateAsync(string sourceText, string targetLanguage, CancellationToken cancellationToken);
}
