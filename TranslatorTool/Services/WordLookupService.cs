using System.Text.RegularExpressions;
using TranslatorTool.Models;

namespace TranslatorTool.Services;

public sealed partial class WordLookupService(
    IPhoneticService phoneticService,
    ArgosTranslateClient argosTranslateClient,
    AiWordLookupService? aiWordLookupService = null)
{
    public async Task<WordLookupResult> LookupAsync(string selectedText, CancellationToken cancellationToken)
    {
        var lookupText = ExtractLookupText(selectedText);
        if (string.IsNullOrWhiteSpace(lookupText))
        {
            return new WordLookupResult(string.Empty, string.Empty, string.Empty);
        }

        var isPhrase = lookupText.Any(char.IsWhiteSpace);
        var phoneticsTask = phoneticService.GetAmericanPhoneticsAsync(lookupText, cancellationToken);
        var argosTask = argosTranslateClient.TranslateAsync(lookupText, cancellationToken);

        var phonetics = await phoneticsTask;
        var phonetic = phonetics
            .FirstOrDefault(item => string.Equals(item.Word, lookupText, StringComparison.OrdinalIgnoreCase))
            ?.AmericanPhonetic ?? string.Empty;

        var argos = await argosTask;
        var translation = argos.Ok && TranslationTextQualityEvaluator.LooksReadable(argos.TranslatedText)
            ? argos.TranslatedText
            : string.Empty;

        if ((isPhrase || string.IsNullOrWhiteSpace(phonetic)) && aiWordLookupService is not null)
        {
            try
            {
                using var aiTimeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                aiTimeout.CancelAfter(TimeSpan.FromSeconds(6));
                var ai = await aiWordLookupService.LookupAsync(lookupText, aiTimeout.Token);
                if (ai is not null)
                {
                    return new WordLookupResult(
                        string.IsNullOrWhiteSpace(ai.Word) ? lookupText : ai.Word,
                        ChooseText(ai.Phonetic, phonetic, "\u6682\u65E0\u97F3\u6807"),
                        ChooseText(ai.Translation, translation, "\u6682\u672A\u83B7\u5F97\u7FFB\u8BD1"));
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
            {
                DiagnosticsLog.Write($"AI word lookup failed: {ex.Message}");
            }
        }

        return new WordLookupResult(
            lookupText,
            string.IsNullOrWhiteSpace(phonetic) ? "\u6682\u65E0\u97F3\u6807" : phonetic,
            string.IsNullOrWhiteSpace(translation) ? "\u6682\u672A\u83B7\u5F97\u7FFB\u8BD1" : translation);
    }

    private static string ChooseText(string first, string second, string fallback)
    {
        if (!string.IsNullOrWhiteSpace(first))
        {
            return first;
        }

        return string.IsNullOrWhiteSpace(second) ? fallback : second;
    }

    private static string ExtractLookupText(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        var normalized = Regex.Replace(text.Trim(), @"\s+", " ");
        if (normalized.Length > 160)
        {
            return string.Empty;
        }

        var matches = EnglishWordRegex().Matches(normalized);
        if (matches.Count == 0 || matches.Count > 16)
        {
            return string.Empty;
        }

        var first = matches[0].Index;
        var lastMatch = matches[^1];
        var last = lastMatch.Index + lastMatch.Length;
        return normalized[first..last].Trim();
    }

    [GeneratedRegex("[A-Za-z][A-Za-z-]*")]
    private static partial Regex EnglishWordRegex();
}
