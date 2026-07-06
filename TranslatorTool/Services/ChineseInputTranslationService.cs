using TranslatorTool.Models;

namespace TranslatorTool.Services;

public sealed class ChineseInputTranslationService
{
    private readonly SettingsService _settingsService;
    private readonly ITranslationService _aiTranslationService;
    private readonly Func<string, string, string, CancellationToken, Task<ArgosTranslateResult>> _argosTranslateAsync;

    public ChineseInputTranslationService(
        SettingsService settingsService,
        AiTranslationService aiTranslationService,
        ArgosTranslateClient argosTranslateClient)
        : this(settingsService, aiTranslationService, argosTranslateClient.TranslateAsync)
    {
    }

    public ChineseInputTranslationService(
        SettingsService settingsService,
        ITranslationService aiTranslationService,
        Func<string, string, string, CancellationToken, Task<ArgosTranslateResult>> argosTranslateAsync)
    {
        _settingsService = settingsService;
        _aiTranslationService = aiTranslationService;
        _argosTranslateAsync = argosTranslateAsync;
    }

    public async Task<TranslationResult> TranslateAsync(
        string sourceText,
        TargetLanguageOption targetLanguage,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(sourceText))
        {
            return new TranslationResult
            {
                SourceText = string.Empty,
                TranslatedText = string.Empty,
                Notes = "\u8BF7\u8F93\u5165\u8981\u7FFB\u8BD1\u7684\u4E2D\u6587\u3002",
                SourceLanguage = "zh",
                TargetLanguage = targetLanguage.TargetLanguage,
                Engine = "\u672A\u5F00\u59CB",
                IsFinal = true,
                StatusMessage = "\u8BF7\u8F93\u5165\u4E2D\u6587\u540E\u518D\u7FFB\u8BD1\u3002"
            };
        }

        var settings = _settingsService.Load();
        var timeout = TimeSpan.FromMilliseconds(Math.Max(1000, settings.AiTimeoutMilliseconds));
        if (settings.EnableAiTranslation)
        {
            try
            {
                using var aiTimeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                aiTimeout.CancelAfter(timeout);
                var aiResult = await _aiTranslationService.TranslateAsync(sourceText, targetLanguage.TargetLanguage, aiTimeout.Token);
                aiResult.SourceLanguage = "zh";
                aiResult.TargetLanguage = targetLanguage.TargetLanguage;
                aiResult.Engine = "AI";
                aiResult.IsFinal = true;
                aiResult.StatusMessage = "\u5728\u7EBF\u7FFB\u8BD1\u6210\u529F\u3002";
                return aiResult;
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                return await OfflineFallbackAsync(sourceText, targetLanguage, "\u5728\u7EBF\u7FFB\u8BD1\u8D85\u65F6", cancellationToken);
            }
            catch (Exception ex)
            {
                return await OfflineFallbackAsync(sourceText, targetLanguage, $"\u5728\u7EBF\u7FFB\u8BD1\u5931\u8D25\uFF1A{ex.Message}", cancellationToken);
            }
        }

        return await OfflineFallbackAsync(sourceText, targetLanguage, "\u5728\u7EBF\u7FFB\u8BD1\u672A\u542F\u7528", cancellationToken);
    }

    private async Task<TranslationResult> OfflineFallbackAsync(
        string sourceText,
        TargetLanguageOption targetLanguage,
        string reason,
        CancellationToken cancellationToken)
    {
        if (targetLanguage.Code != "en")
        {
            return new TranslationResult
            {
                SourceText = sourceText.Trim(),
                TranslatedText = string.Empty,
                Notes = $"{reason}\uFF1B\u8BE5\u76EE\u6807\u8BED\u8A00\u6682\u65E0\u79BB\u7EBF\u7FFB\u8BD1\u5305\u3002",
                SourceLanguage = "zh",
                TargetLanguage = targetLanguage.TargetLanguage,
                Engine = "AI",
                IsFinal = true,
                StatusMessage = "\u5728\u7EBF\u7FFB\u8BD1\u5931\u8D25\uFF0C\u8BE5\u76EE\u6807\u8BED\u8A00\u6682\u65E0\u79BB\u7EBF\u5305\u3002"
            };
        }

        var argosResult = await _argosTranslateAsync(sourceText.Trim(), "zh", "en", cancellationToken);
        if (argosResult.Ok
            && !string.IsNullOrWhiteSpace(argosResult.TranslatedText)
            && TranslationTextQualityEvaluator.LooksReadable(argosResult.TranslatedText))
        {
            return new TranslationResult
            {
                SourceText = sourceText.Trim(),
                TranslatedText = argosResult.TranslatedText,
                Notes = $"{reason}\uFF1B\u5DF2\u4F7F\u7528 Argos Translate \u4E2D\u82F1\u79BB\u7EBF\u6A21\u578B\u3002",
                SourceLanguage = "zh",
                TargetLanguage = targetLanguage.TargetLanguage,
                Engine = "Argos Offline",
                IsFinal = true,
                StatusMessage = "\u5728\u7EBF\u7FFB\u8BD1\u8D85\u65F6\uFF0C\u5F53\u524D\u4E3A\u82F1\u6587\u79BB\u7EBF\u7ED3\u679C\u3002"
            };
        }

        return new TranslationResult
        {
            SourceText = sourceText.Trim(),
            TranslatedText = string.Empty,
            Notes = $"{reason}\uFF1BArgos zh->en \u79BB\u7EBF\u7FFB\u8BD1\u4E0D\u53EF\u7528\uFF1A{argosResult.Error}",
            SourceLanguage = "zh",
            TargetLanguage = targetLanguage.TargetLanguage,
            Engine = "Argos Offline",
            IsFinal = true,
            StatusMessage = "\u5728\u7EBF\u7FFB\u8BD1\u5931\u8D25\uFF0C\u82F1\u6587\u79BB\u7EBF\u7FFB\u8BD1\u4E5F\u4E0D\u53EF\u7528\u3002"
        };
    }
}
