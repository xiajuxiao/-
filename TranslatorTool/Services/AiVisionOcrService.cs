using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace TranslatorTool.Services;

public class AiVisionOcrService(SettingsService settingsService)
{
    private static readonly HttpClient HttpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(12)
    };

    public async Task<string> TryRecognizeTextAsync(Bitmap image, CancellationToken cancellationToken)
    {
        var settings = settingsService.Load();
        if (string.IsNullOrWhiteSpace(settings.AiApiKey))
        {
            return string.Empty;
        }

        using var aiImage = PrepareImageForAiOcr(image);
        var dataUrl = CreatePngDataUrl(aiImage);
        foreach (var model in GetCandidateModels(settings.AiBaseUrl, settings.AiVisionOcrModel))
        {
            try
            {
                var text = await RecognizeWithModelAsync(settings.AiApiKey, settings.AiBaseUrl, model, dataUrl, cancellationToken);
                if (!string.IsNullOrWhiteSpace(text))
                {
                    var normalized = OcrTextPostProcessor.Normalize(CleanupOcrText(text));
                    if (!OcrQualityEvaluator.IsSuspicious(normalized))
                    {
                        return normalized;
                    }

                    using var enhancedImage = PrepareHighContrastImageForAiOcr(image);
                    var enhancedText = await RecognizeWithModelAsync(
                        settings.AiApiKey,
                        settings.AiBaseUrl,
                        model,
                        CreatePngDataUrl(enhancedImage),
                        cancellationToken);
                    var enhancedNormalized = OcrTextPostProcessor.Normalize(CleanupOcrText(enhancedText));
                    var better = OcrQualityEvaluator.ChooseBetter(normalized, enhancedNormalized);
                    var corrected = await CorrectSuspiciousOcrTextAsync(
                        settings.AiApiKey,
                        settings.AiBaseUrl,
                        settings.AiTranslationModel,
                        better,
                        cancellationToken);
                    DiagnosticsLog.Write($"Enhanced OCR used. first={normalized.Length}, enhanced={enhancedNormalized.Length}, corrected={corrected.Length}.");
                    return string.IsNullOrWhiteSpace(corrected) ? better : corrected;
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                DiagnosticsLog.Write($"AI OCR failed with {model}: {ex.Message}");
            }
        }

        return string.Empty;
    }

    private static async Task<string> RecognizeWithModelAsync(string apiKey, string baseUrl, string model, string dataUrl, CancellationToken cancellationToken)
    {
        var request = new
        {
            model,
            messages = new object[]
            {
                new
                {
                    role = "user",
                    content = new object[]
                    {
                        CreateImageContent(dataUrl, model),
                        new
                        {
                            type = "text",
                            text = "Recognize all readable text in this screenshot. Preserve reading order, line breaks, punctuation, symbols, units, and datasheet notation. Do not translate, summarize, correct, or add explanations. Return only the OCR text."
                        }
                    }
                }
            },
            max_tokens = 600
        };

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, BuildChatCompletionsUrl(baseUrl));
        httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        httpRequest.Content = new StringContent(JsonSerializer.Serialize(request), Encoding.UTF8, "application/json");

        using var response = await HttpClient.SendAsync(httpRequest, cancellationToken);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        using var document = JsonDocument.Parse(json);
        var content = document.RootElement
            .GetProperty("choices")[0]
            .GetProperty("message")
            .GetProperty("content");
        return ExtractContentText(content);
    }

    private static async Task<string> CorrectSuspiciousOcrTextAsync(
        string apiKey,
        string baseUrl,
        string configuredModel,
        string text,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        var model = string.IsNullOrWhiteSpace(configuredModel) ? "gpt-4.1-mini" : configuredModel.Trim();
        var request = new
        {
            model,
            messages = new[]
            {
                new
                {
                    role = "system",
                    content = """
                    You correct OCR text from electronics datasheets.
                    Fix obvious OCR mistakes using English grammar, units, and datasheet context.
                    Preserve line breaks and reading order.
                    Do not translate. Do not explain. Return only corrected English text.
                    If unsure, keep the original token.
                    """
                },
                new
                {
                    role = "user",
                    content = text
                }
            },
            temperature = 0,
            max_tokens = Math.Clamp(text.Length * 2, 300, 1200)
        };

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, BuildChatCompletionsUrl(baseUrl));
        httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
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
        return OcrTextPostProcessor.Normalize(CleanupOcrText(content ?? string.Empty));
    }

    private static object CreateImageContent(string dataUrl, string model)
    {
        if (model.Contains("ocr", StringComparison.OrdinalIgnoreCase))
        {
            return new
            {
                type = "image_url",
                image_url = new
                {
                    url = dataUrl
                },
                min_pixels = 3072,
                max_pixels = 8388608
            };
        }

        return new
        {
            type = "image_url",
            image_url = new
            {
                url = dataUrl,
                detail = "high"
            }
        };
    }

    private static IEnumerable<string> GetCandidateModels(string baseUrl, string configuredModel)
    {
        var model = string.IsNullOrWhiteSpace(configuredModel)
            ? "gpt-5.4-mini"
            : configuredModel.Trim();
        yield return model;

        if (baseUrl.Contains("openai.com", StringComparison.OrdinalIgnoreCase)
            && !model.Equals("gpt-4.1-mini", StringComparison.OrdinalIgnoreCase))
        {
            yield return "gpt-4.1-mini";
        }
    }

    private static string BuildChatCompletionsUrl(string baseUrl)
    {
        var normalized = string.IsNullOrWhiteSpace(baseUrl)
            ? "https://api.openai.com/v1"
            : baseUrl.Trim().TrimEnd('/');
        return $"{normalized}/chat/completions";
    }

    private static string CreatePngDataUrl(Bitmap image)
    {
        using var stream = new MemoryStream();
        image.Save(stream, ImageFormat.Png);
        return $"data:image/png;base64,{Convert.ToBase64String(stream.ToArray())}";
    }

    private static Bitmap PrepareImageForAiOcr(Bitmap image)
    {
        const int scale = 2;
        var scaled = new Bitmap(image.Width * scale, image.Height * scale, PixelFormat.Format32bppArgb);
        using var graphics = Graphics.FromImage(scaled);
        graphics.Clear(Color.White);
        graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
        graphics.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;
        graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
        graphics.DrawImage(image, new Rectangle(0, 0, scaled.Width, scaled.Height));
        return scaled;
    }

    private static Bitmap PrepareHighContrastImageForAiOcr(Bitmap image)
    {
        using var normalized = new Bitmap(image.Width, image.Height, PixelFormat.Format32bppArgb);
        for (var y = 0; y < image.Height; y++)
        {
            for (var x = 0; x < image.Width; x++)
            {
                var color = image.GetPixel(x, y);
                var luminance = (color.R * 0.299) + (color.G * 0.587) + (color.B * 0.114);
                var output = luminance < 165 && !IsSelectionBlue(color)
                    ? Color.Black
                    : Color.White;
                normalized.SetPixel(x, y, output);
            }
        }

        const int scale = 3;
        var scaled = new Bitmap(normalized.Width * scale, normalized.Height * scale, PixelFormat.Format32bppArgb);
        using var graphics = Graphics.FromImage(scaled);
        graphics.Clear(Color.White);
        graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
        graphics.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.Half;
        graphics.DrawImage(normalized, new Rectangle(0, 0, scaled.Width, scaled.Height));
        return scaled;
    }

    private static bool IsSelectionBlue(Color color)
    {
        return color.B > color.R + 25
               && color.B > color.G + 5
               && color.G > color.R + 10
               && color.B > 120
               && color.G > 90;
    }

    private static string ExtractContentText(JsonElement content)
    {
        if (content.ValueKind == JsonValueKind.String)
        {
            return content.GetString() ?? string.Empty;
        }

        if (content.ValueKind == JsonValueKind.Object)
        {
            if (content.TryGetProperty("text", out var textElement) && textElement.ValueKind == JsonValueKind.String)
            {
                return textElement.GetString() ?? string.Empty;
            }

            if (content.TryGetProperty("ocr_result", out var ocrResult)
                && ocrResult.TryGetProperty("words_info", out var wordsInfo)
                && wordsInfo.ValueKind == JsonValueKind.Array)
            {
                var lines = wordsInfo.EnumerateArray()
                    .Select(item => item.TryGetProperty("text", out var line) ? line.GetString() : null)
                    .Where(line => !string.IsNullOrWhiteSpace(line));
                return string.Join(Environment.NewLine, lines);
            }
        }

        if (content.ValueKind == JsonValueKind.Array)
        {
            var parts = content.EnumerateArray()
                .Select(ExtractContentText)
                .Where(part => !string.IsNullOrWhiteSpace(part));
            return string.Join(Environment.NewLine, parts);
        }

        return string.Empty;
    }

    private static string CleanupOcrText(string text)
    {
        var trimmed = text.Trim();
        if (trimmed.StartsWith("```", StringComparison.Ordinal))
        {
            trimmed = trimmed.Replace("```text", string.Empty, StringComparison.OrdinalIgnoreCase)
                .Replace("```", string.Empty, StringComparison.Ordinal)
                .Trim();
        }

        return OcrTextPostProcessor.Normalize(trimmed);
    }
}
