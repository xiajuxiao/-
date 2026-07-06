using TranslatorTool.Models;

namespace TranslatorTool.Services;

public class TranslationCoordinator(
    SettingsService settingsService,
    ITranslationService offlineTranslationService,
    ITranslationService aiTranslationService)
{
    public async Task TranslateWithUpdatesAsync(
        string sourceText,
        Action<TranslationResult> onResult,
        CancellationToken cancellationToken)
    {
        var settings = settingsService.Load();
        if (string.IsNullOrWhiteSpace(sourceText))
        {
            throw new ArgumentException("Translation text cannot be empty.", nameof(sourceText));
        }

        if (!settings.EnableOfflineTranslation && !settings.EnableAiTranslation)
        {
            throw new InvalidOperationException("Both offline and AI translation are disabled.");
        }

        Task<TranslationResult>? offlineTask = settings.EnableOfflineTranslation
            ? offlineTranslationService.TranslateAsync(sourceText, settings.TargetLanguage, cancellationToken)
            : null;

        Task<TranslationResult>? aiTask = settings.EnableAiTranslation
            ? aiTranslationService.TranslateAsync(sourceText, settings.TargetLanguage, cancellationToken)
            : null;

        if (aiTask is null)
        {
            onResult(await RequireOfflineAsync(offlineTask, cancellationToken));
            return;
        }

        if (offlineTask is not null)
        {
            var offline = await offlineTask;
            offline.IsFinal = false;
            offline.StatusMessage = "\u5DF2\u663E\u793A\u79BB\u7EBF\u7FFB\u8BD1\uFF0C\u6B63\u5728\u7B49\u5F85\u5728\u7EBF\u7ED3\u679C...";
            onResult(offline);
        }
        else if (!aiTask.IsCompleted)
        {
            onResult(new TranslationResult
            {
                SourceText = sourceText,
                TranslatedText = "\u6B63\u5728\u7B49\u5F85\u5728\u7EBF\u7FFB\u8BD1",
                Notes = "\u4ECD\u5728\u7B49\u5F85\u5728\u7EBF\u7FFB\u8BD1\u8FD4\u56DE\u3002",
                Engine = "\u5904\u7406\u4E2D",
                IsFinal = false,
                StatusMessage = "\u6B63\u5728\u7B49\u5F85\u5728\u7EBF\u7FFB\u8BD1\u7ED3\u679C..."
            });
        }

        try
        {
            var result = await aiTask;
            result.StatusMessage = "\u5728\u7EBF\u7FFB\u8BD1\u6210\u529F\u3002";
            onResult(result);
        }
        catch
        {
            if (offlineTask is null)
            {
                onResult(new TranslationResult
                {
                    SourceText = sourceText,
                    TranslatedText = "\u5728\u7EBF\u7FFB\u8BD1\u5931\u8D25",
                    Notes = "\u8BF7\u68C0\u67E5\u7F51\u7EDC\u3001API Key \u6216\u6A21\u578B\u914D\u7F6E\u3002",
                    Engine = "AI",
                    IsFinal = true,
                    StatusMessage = "\u5728\u7EBF\u7FFB\u8BD1\u5931\u8D25\u3002"
                });
                return;
            }

            var fallback = await offlineTask;
            fallback.IsFinal = true;
            fallback.StatusMessage = "\u5728\u7EBF\u7FFB\u8BD1\u5931\u8D25\uFF0C\u5F53\u524D\u4E3A\u79BB\u7EBF\u7ED3\u679C\u3002";
            onResult(fallback);
        }
    }

    public async Task<TranslationResult> TranslateAiOnlyAsync(string sourceText, CancellationToken cancellationToken)
    {
        var settings = settingsService.Load();
        if (string.IsNullOrWhiteSpace(sourceText))
        {
            throw new ArgumentException("Translation text cannot be empty.", nameof(sourceText));
        }

        if (!settings.EnableAiTranslation)
        {
            throw new InvalidOperationException("AI translation is disabled.");
        }

        var result = await aiTranslationService.TranslateAsync(sourceText, settings.TargetLanguage, cancellationToken);
        result.StatusMessage = "\u5728\u7EBF\u7FFB\u8BD1\u6210\u529F\u3002";
        return result;
    }

    private static async Task<TranslationResult> RequireOfflineAsync(Task<TranslationResult>? offlineTask, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (offlineTask is null)
        {
            throw new InvalidOperationException("Offline translation is disabled.");
        }

        var result = await offlineTask;
        result.IsFinal = true;
        result.StatusMessage = "\u79BB\u7EBF\u7FFB\u8BD1\u6210\u529F\u3002";
        return result;
    }
}
