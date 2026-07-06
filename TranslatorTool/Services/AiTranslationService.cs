using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using TranslatorTool.Models;

namespace TranslatorTool.Services;

public class AiTranslationService(SettingsService settingsService) : ITranslationService
{
    private static readonly HttpClient HttpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(20)
    };

    public async Task<TranslationResult> TranslateAsync(string sourceText, string targetLanguage, CancellationToken cancellationToken)
    {
        var settings = settingsService.Load();
        if (string.IsNullOrWhiteSpace(settings.AiApiKey))
        {
            throw new InvalidOperationException("AI API Key is not configured.");
        }

        var cleanText = OcrTextPostProcessor.Normalize(sourceText);
        var request = new
        {
            model = string.IsNullOrWhiteSpace(settings.AiTranslationModel)
                ? "gpt-4.1-mini"
                : settings.AiTranslationModel.Trim(),
            messages = new[]
            {
                new
                {
                    role = "system",
                    content = """
                    You are a desktop translation engine.
                    Input may be OCR text from electronics datasheets or manually entered Chinese text.
                    If the input contains obvious OCR errors, correct them first.
                    Translate line by line into the requested target language.
                    Return JSON only. No markdown.
                    Fields:
                    sourceText: corrected source text. Preserve line breaks as much as possible.
                    translatedText: translation in the target language. Keep the same line count and line order as sourceText.
                    notes: short notes for important terms or OCR fixes. Empty string is allowed.
                    phonetics: return an empty array unless a term is truly important.
                    """
                },
                new
                {
                    role = "user",
                    content = $"Target language: {targetLanguage}\nTranslate line by line:\n{cleanText}"
                }
            },
            temperature = 0,
            max_tokens = EstimateMaxTokens(cleanText)
        };

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, BuildChatCompletionsUrl(settings.AiBaseUrl));
        httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", settings.AiApiKey);
        httpRequest.Content = new StringContent(JsonSerializer.Serialize(request), Encoding.UTF8, "application/json");

        using var response = await HttpClient.SendAsync(httpRequest, cancellationToken);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        using var document = JsonDocument.Parse(json);
        var content = document.RootElement
            .GetProperty("choices")[0]
            .GetProperty("message")
            .GetProperty("content")
            .GetString();

        var aiPayload = ParseAiPayload(content);
        var correctedSourceText = string.IsNullOrWhiteSpace(aiPayload.SourceText)
            ? cleanText
            : OcrTextPostProcessor.Normalize(aiPayload.SourceText);
        correctedSourceText = AiCorrectionNoteApplier.Apply(correctedSourceText, aiPayload.Notes);
        var translatedText = NormalizeLineEndings(aiPayload.TranslatedText.Trim());

        return new TranslationResult
        {
            SourceText = correctedSourceText,
            TranslatedText = translatedText,
            Notes = aiPayload.Notes,
            Phonetics = aiPayload.Phonetics.Take(2).ToList(),
            SourceLanguage = "en",
            TargetLanguage = targetLanguage,
            Engine = "AI",
            IsFinal = true,
            StatusMessage = "\u5728\u7EBF\u7FFB\u8BD1\u6210\u529F\u3002"
        };
    }

    private static int EstimateMaxTokens(string text)
    {
        var lineCount = Math.Max(1, text.Count(ch => ch == '\n') + 1);
        return Math.Clamp(text.Length * 2 + lineCount * 24, 360, 1200);
    }

    private static string BuildChatCompletionsUrl(string baseUrl)
    {
        var normalized = string.IsNullOrWhiteSpace(baseUrl)
            ? "https://api.openai.com/v1"
            : baseUrl.Trim().TrimEnd('/');
        return $"{normalized}/chat/completions";
    }

    private static AiTranslationPayload ParseAiPayload(string? content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            throw new InvalidOperationException("AI returned empty content.");
        }

        try
        {
            using var document = JsonDocument.Parse(CleanupJson(content));
            var root = document.RootElement;
            var payload = new AiTranslationPayload
            {
                SourceText = root.TryGetProperty("sourceText", out var sourceText)
                    ? sourceText.GetString() ?? string.Empty
                    : string.Empty,
                TranslatedText = root.GetProperty("translatedText").GetString() ?? string.Empty,
                Notes = root.TryGetProperty("notes", out var notes) ? notes.GetString() ?? string.Empty : string.Empty
            };

            if (root.TryGetProperty("phonetics", out var phoneticsElement)
                && phoneticsElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in phoneticsElement.EnumerateArray())
                {
                    payload.Phonetics.Add(new PhoneticItem
                    {
                        Word = item.TryGetProperty("word", out var word) ? word.GetString() ?? string.Empty : string.Empty,
                        AmericanPhonetic = item.TryGetProperty("americanPhonetic", out var phonetic) ? phonetic.GetString() ?? string.Empty : string.Empty,
                        Meaning = item.TryGetProperty("meaning", out var meaning) ? meaning.GetString() ?? string.Empty : string.Empty
                    });
                }
            }

            return payload;
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException("AI returned invalid JSON.", ex);
        }
    }

    private static string CleanupJson(string content)
    {
        var trimmed = content.Trim();
        if (!trimmed.StartsWith("```", StringComparison.Ordinal))
        {
            return trimmed;
        }

        return trimmed.Replace("```json", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("```", string.Empty, StringComparison.Ordinal)
            .Trim();
    }

    private static string NormalizeLineEndings(string text)
    {
        return text.Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n');
    }

    private static List<PhoneticItem> MergePhonetics(List<PhoneticItem> localItems, List<PhoneticItem> aiItems)
    {
        var merged = new List<PhoneticItem>(localItems);
        var existing = new HashSet<string>(localItems.Select(item => item.Word), StringComparer.OrdinalIgnoreCase);

        foreach (var item in aiItems.Where(item =>
                     !string.IsNullOrWhiteSpace(item.Word)
                     && !string.IsNullOrWhiteSpace(item.AmericanPhonetic)
                     && !existing.Contains(item.Word)))
        {
            merged.Add(item);
            existing.Add(item.Word);
        }

        return merged.Take(4).ToList();
    }

    private sealed class AiTranslationPayload
    {
        public string SourceText { get; set; } = string.Empty;
        public string TranslatedText { get; set; } = string.Empty;
        public string Notes { get; set; } = string.Empty;
        public List<PhoneticItem> Phonetics { get; } = [];
    }
}
