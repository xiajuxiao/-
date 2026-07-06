using System.Drawing;

namespace TranslatorTool.Services;

public sealed class OcrCoordinator(
    PaddleOcrService paddleOcrService,
    IOcrService windowsOcrService,
    AiVisionOcrService aiVisionOcrService)
{
    private static readonly TimeSpan WindowsOcrBudget = TimeSpan.FromSeconds(3);
    private static readonly TimeSpan PaddleOcrBudget = TimeSpan.FromSeconds(7);
    private static readonly TimeSpan AiOcrBudget = TimeSpan.FromSeconds(5);

    public async Task<OcrResult> RecognizeScreenshotAsync(Bitmap image, CancellationToken cancellationToken)
    {
        var windows = await TryRecognizeWithBudgetAsync(
            "Windows OCR",
            token => windowsOcrService.RecognizeTextAsync(image, token),
            WindowsOcrBudget,
            cancellationToken);
        if (IsGoodEnough(windows))
        {
            return windows;
        }

        var paddle = await TryRecognizeWithBudgetAsync(
            "PaddleOCR",
            token => paddleOcrService.RecognizeTextAsync(image, token),
            PaddleOcrBudget,
            cancellationToken);
        var betterLocal = ChooseBetter(windows, paddle);
        if (IsGoodEnough(betterLocal))
        {
            return betterLocal;
        }

        var ai = await TryRecognizeWithBudgetAsync(
            "AI OCR",
            token => aiVisionOcrService.TryRecognizeTextAsync(image, token),
            AiOcrBudget,
            cancellationToken);
        return ChooseBetter(betterLocal, ai);
    }

    private static async Task<OcrResult> TryRecognizeWithBudgetAsync(
        string engine,
        Func<CancellationToken, Task<string>> recognize,
        TimeSpan budget,
        CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(budget);
        try
        {
            return await TryRecognizeAsync(engine, recognize, timeout.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            DiagnosticsLog.Write($"{engine} timed out after {budget.TotalSeconds:0.#}s.");
            return OcrResult.Empty(engine);
        }
    }

    private static async Task<OcrResult> TryRecognizeAsync(
        string engine,
        Func<CancellationToken, Task<string>> recognize,
        CancellationToken cancellationToken)
    {
        try
        {
            var text = OcrTextPostProcessor.Normalize(await recognize(cancellationToken));
            DiagnosticsLog.Write($"{engine} text length: {text.Length}.");
            return new OcrResult(text, engine, !OcrQualityEvaluator.IsSuspicious(text));
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            DiagnosticsLog.Write($"{engine} failed: {ex.Message}");
            return OcrResult.Empty(engine);
        }
    }

    private static bool IsGoodEnough(OcrResult result)
    {
        return !string.IsNullOrWhiteSpace(result.Text) && result.IsReliable;
    }

    private static OcrResult ChooseBetter(OcrResult first, OcrResult second)
    {
        if (string.IsNullOrWhiteSpace(first.Text))
        {
            return second;
        }

        if (string.IsNullOrWhiteSpace(second.Text))
        {
            return first;
        }

        var betterText = OcrQualityEvaluator.ChooseBetter(first.Text, second.Text);
        return string.Equals(betterText, second.Text, StringComparison.Ordinal)
            ? second
            : first;
    }
}
