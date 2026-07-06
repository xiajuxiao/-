using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using TranslatorTool.Models;

namespace TranslatorTool.Services;

public sealed class AiWordLookupService(SettingsService settingsService)
{
    private static readonly HttpClient HttpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(8)
    };

    public async Task<WordLookupResult?> LookupAsync(string text, CancellationToken cancellationToken)
    {
        var settings = settingsService.Load();
        if (string.IsNullOrWhiteSpace(settings.AiApiKey) || string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

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
                    You are a compact dictionary popup for electronics datasheets.
                    Return JSON only. No markdown.
                    Fields:
                    text: normalized selected English word or short phrase.
                    phonetic: American IPA. For a phrase, provide a compact pronunciation hint or the key word IPA; empty string is allowed only if impossible.
                    translation: concise Chinese translation suitable for a popup.
                    """
                },
                new
                {
                    role = "user",
                    content = text
                }
            },
            temperature = 0,
            max_tokens = 220
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

        if (string.IsNullOrWhiteSpace(content))
        {
            return null;
        }

        using var payload = JsonDocument.Parse(CleanupJson(content));
        var root = payload.RootElement;
        var normalizedText = root.TryGetProperty("text", out var textElement)
            ? textElement.GetString() ?? text
            : text;
        var phonetic = root.TryGetProperty("phonetic", out var phoneticElement)
            ? phoneticElement.GetString() ?? string.Empty
            : string.Empty;
        var translation = root.TryGetProperty("translation", out var translationElement)
            ? translationElement.GetString() ?? string.Empty
            : string.Empty;

        if (string.IsNullOrWhiteSpace(translation))
        {
            return null;
        }

        return new WordLookupResult(normalizedText.Trim(), phonetic.Trim(), translation.Trim());
    }

    private static string BuildChatCompletionsUrl(string baseUrl)
    {
        var normalized = string.IsNullOrWhiteSpace(baseUrl)
            ? "https://api.openai.com/v1"
            : baseUrl.Trim().TrimEnd('/');
        return $"{normalized}/chat/completions";
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
}
