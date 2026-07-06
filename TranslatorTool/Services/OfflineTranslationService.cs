using TranslatorTool.Models;

namespace TranslatorTool.Services;

public class OfflineTranslationService(
    IPhoneticService phoneticService,
    ArgosTranslateClient? argosTranslateClient = null) : ITranslationService
{
    private static readonly Dictionary<string, (string Translation, string Notes)> PhraseDictionary = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Current loop bandwidth"] = ("\u7535\u6D41\u73AF\u5E26\u5BBD", "\u6307\u7535\u673A\u63A7\u5236\u4E2D\u7535\u6D41\u73AF\u80FD\u591F\u54CD\u5E94\u7535\u6D41\u53D8\u5316\u7684\u9891\u7387\u8303\u56F4\u3002"),
        ["hello"] = ("\u4F60\u597D", "\u5E38\u89C1\u95EE\u5019\u8BED\u3002"),
        ["world"] = ("\u4E16\u754C", "\u8868\u793A\u5730\u7403\u3001\u4EBA\u7C7B\u793E\u4F1A\u6216\u67D0\u4E2A\u9886\u57DF\u3002")
    };

    private readonly ArgosTranslateClient? _argosTranslateClient = argosTranslateClient;

    public async Task<TranslationResult> TranslateAsync(string sourceText, string targetLanguage, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var cleanText = sourceText.Trim();
        var phoneticsTask = phoneticService.GetAmericanPhoneticsAsync(cleanText, cancellationToken);

        if (_argosTranslateClient is not null)
        {
            var argosResult = await _argosTranslateClient.TranslateAsync(cleanText, cancellationToken);
            if (argosResult.Ok
                && !string.IsNullOrWhiteSpace(argosResult.TranslatedText)
                && TranslationTextQualityEvaluator.LooksReadable(argosResult.TranslatedText))
            {
                return new TranslationResult
                {
                    SourceText = cleanText,
                    TranslatedText = argosResult.TranslatedText,
                    Notes = "\u0041\u0072\u0067\u006F\u0073\u0020\u0054\u0072\u0061\u006E\u0073\u006C\u0061\u0074\u0065\u0020\u672C\u5730\u82F1\u4E2D\u6A21\u578B\u7ED3\u679C\u3002",
                    Phonetics = await phoneticsTask,
                    SourceLanguage = "en",
                    TargetLanguage = targetLanguage,
                    Engine = "Argos Offline",
                    IsFinal = false,
                    StatusMessage = "\u79BB\u7EBF\u7FFB\u8BD1\u6210\u529F\uFF0C\u0041\u0049\u0020\u66F4\u65B0\u4E2D..."
                };
            }

            var reason = argosResult.Ok
                ? "Argos returned unreadable text."
                : argosResult.Error;
            DiagnosticsLog.Write($"Argos offline translation unavailable: {reason}");
        }

        var hasDictionaryResult = PhraseDictionary.TryGetValue(cleanText, out var value);
        var (translation, notes) = hasDictionaryResult
            ? value
            : ("\u6682\u672A\u83B7\u5F97\u53EF\u9760\u79BB\u7EBF\u7FFB\u8BD1", "\u0041\u0072\u0067\u006F\u0073\u0020\u79BB\u7EBF\u6A21\u578B\u4E0D\u53EF\u7528\u6216\u8FD4\u56DE\u4E86\u4E0D\u53EF\u8BFB\u7ED3\u679C\uFF0C\u4E14\u5F53\u524D\u672C\u5730\u8BCD\u5178\u672A\u8986\u76D6\u8BE5\u6587\u672C\u3002");

        return new TranslationResult
        {
            SourceText = cleanText,
            TranslatedText = translation,
            Notes = notes,
            Phonetics = await phoneticsTask,
            SourceLanguage = "en",
            TargetLanguage = targetLanguage,
            Engine = "Offline Dictionary",
            IsFinal = false,
            StatusMessage = hasDictionaryResult ? "\u8BCD\u5178\u79BB\u7EBF\u7FFB\u8BD1\uFF0C\u0041\u0049\u0020\u66F4\u65B0\u4E2D..." : "\u79BB\u7EBF\u515C\u5E95\u7ED3\u679C\uFF0C\u0041\u0049\u0020\u66F4\u65B0\u4E2D..."
        };
    }
}
