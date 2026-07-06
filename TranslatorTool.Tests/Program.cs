using TranslatorTool.Models;
using TranslatorTool.Services;
using TranslatorTool.ViewModels;
using Drawing = System.Drawing;

await TranslationCoordinatorTests.RunAllAsync();
await OcrSmokeTests.RunAsync();
await PaddleOcrSmokeTests.RunAsync();
await OfflineTranslationServiceTests.RunAsync();
await ArgosTranslateClientSmokeTests.RunAsync();
await ChineseInputTranslationServiceTests.RunAsync();
await WordLookupServiceTests.RunAsync();
OcrTextPostProcessorTests.Run();
OcrQualityEvaluatorTests.Run();
TranslationTextQualityEvaluatorTests.Run();
AiCorrectionNoteApplierTests.Run();
SelectedTextQualityEvaluatorTests.Run();
FloatingTranslationViewModelTests.Run();
Console.WriteLine("TranslationCoordinator tests passed.");
Console.WriteLine("OCR smoke test passed.");
Console.WriteLine("PaddleOCR smoke test passed.");
Console.WriteLine("Offline translation service tests passed.");
Console.WriteLine("Argos translate client smoke test passed.");
Console.WriteLine("Chinese input translation service tests passed.");
Console.WriteLine("Word lookup service tests passed.");
Console.WriteLine("OCR post-processor tests passed.");
Console.WriteLine("OCR quality evaluator tests passed.");
Console.WriteLine("Translation text quality evaluator tests passed.");
Console.WriteLine("AI correction note applier tests passed.");
Console.WriteLine("Selected text quality evaluator tests passed.");
Console.WriteLine("FloatingTranslationViewModel tests passed.");

internal static class TranslationCoordinatorTests
{
    public static async Task RunAllAsync()
    {
        await AiFastReturnsOnlyAiResultAsync();
        await AiWaitsForOfflinePriorityThenReplacesAsync();
        await AiSlowShowsTimeoutOfflineThenAiAsync();
        await AiFailureKeepsOfflineResultAsync();
        await AiFinalResultCanCorrectOcrSourceTextAsync();
    }

    private static async Task AiFastReturnsOnlyAiResultAsync()
    {
        SaveSettings(timeoutMilliseconds: 1000, enableAi: true, enableOffline: true);
        var updates = await RunCoordinatorAsync(TimeSpan.FromMilliseconds(10), TimeSpan.FromMilliseconds(20), false);
        Assert(updates.Count == 2, "Fast AI should still replace an initial offline result.");
        Assert(updates[0].Engine == "Offline" && !updates[0].IsFinal, "Offline result should be shown first.");
        Assert(updates[1].Engine == "AI" && updates[1].IsFinal, "Fast AI result should replace offline result.");
        Assert(updates[1].StatusMessage.Contains('\u6210'), "Fast AI result should show success.");
    }

    private static async Task AiSlowShowsTimeoutOfflineThenAiAsync()
    {
        SaveSettings(timeoutMilliseconds: 30, enableAi: true, enableOffline: true);
        var updates = await RunCoordinatorAsync(TimeSpan.FromMilliseconds(5), TimeSpan.FromMilliseconds(80), false);
        Assert(updates.Count == 2, "Slow AI should show interim offline result then AI result.");
        Assert(updates[0].StatusMessage.Contains('\u79BB'), "Interim result should show offline status.");
        Assert(updates[1].StatusMessage.Contains('\u6210'), "Final AI result should show success.");
    }

    private static async Task AiWaitsForOfflinePriorityThenReplacesAsync()
    {
        SaveSettings(timeoutMilliseconds: 1000, enableAi: true, enableOffline: true);
        var updates = await RunCoordinatorAsync(TimeSpan.FromMilliseconds(80), TimeSpan.FromMilliseconds(5), false);
        Assert(updates.Count == 2, "AI completed first should still replace after initial offline result.");
        Assert(updates[0].Engine == "Offline", "Offline result should be displayed first by policy.");
        Assert(updates[1].Engine == "AI" && updates[1].IsFinal, "AI result should replace offline immediately after offline is shown.");
    }

    private static async Task AiFailureKeepsOfflineResultAsync()
    {
        SaveSettings(timeoutMilliseconds: 30, enableAi: true, enableOffline: true);
        var updates = await RunCoordinatorAsync(TimeSpan.FromMilliseconds(5), TimeSpan.FromMilliseconds(80), true);
        Assert(updates.Count == 2, "AI failure should show interim offline result then final failure fallback.");
        Assert(updates[1].Engine == "Offline" && updates[1].IsFinal, "AI failure should keep final offline result.");
        Assert(updates[1].StatusMessage.Contains('\u5931'), "Failure status should be explicit.");
    }

    private static async Task AiFinalResultCanCorrectOcrSourceTextAsync()
    {
        SaveSettings(timeoutMilliseconds: 20, enableAi: true, enableOffline: true);
        var coordinator = new TranslationCoordinator(
            new SettingsService(),
            new FakeTranslationService("Offline", TimeSpan.FromMilliseconds(5), false, false),
            new FakeTranslationService("AI", TimeSpan.FromMilliseconds(60), false, true, "The SGM721/2/3/4 are a family of amplifiers."));

        var updates = new List<TranslationResult>();
        await coordinator.TranslateWithUpdatesAsync("The SGM721/2/3/4 are a familiy of amplifiers.", updates.Add, CancellationToken.None);
        Assert(updates.Count == 2, "Slow AI should first show offline result, then final AI result.");
        Assert(updates[0].SourceText.Contains("familiy", StringComparison.Ordinal), "Interim result should keep raw OCR text.");
        Assert(updates[1].SourceText.Contains("family", StringComparison.Ordinal), "Final AI result should refresh corrected source text.");
    }

    private static async Task<List<TranslationResult>> RunCoordinatorAsync(TimeSpan offlineDelay, TimeSpan aiDelay, bool aiFails)
    {
        var coordinator = new TranslationCoordinator(
            new SettingsService(),
            new FakeTranslationService("Offline", offlineDelay, false, false),
            new FakeTranslationService("AI", aiDelay, aiFails, true));

        var updates = new List<TranslationResult>();
        await coordinator.TranslateWithUpdatesAsync("Current loop bandwidth", updates.Add, CancellationToken.None);
        return updates;
    }

    private static void SaveSettings(int timeoutMilliseconds, bool enableAi, bool enableOffline)
    {
        new SettingsService().Save(new AppSettings
        {
            AiApiKey = "test-key",
            TargetLanguage = "\u4E2D\u6587",
            EnableAiTranslation = enableAi,
            EnableOfflineTranslation = enableOffline,
            AiTimeoutMilliseconds = timeoutMilliseconds
        });
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}

internal sealed class FakeTranslationService(
    string engine,
    TimeSpan delay,
    bool fails,
    bool isFinal,
    string? sourceTextOverride = null) : ITranslationService
{
    public async Task<TranslationResult> TranslateAsync(string sourceText, string targetLanguage, CancellationToken cancellationToken)
    {
        await Task.Delay(delay, cancellationToken);
        if (fails)
        {
            throw new InvalidOperationException($"{engine} failed");
        }

        return new TranslationResult
        {
            SourceText = sourceTextOverride ?? sourceText,
            TranslatedText = $"{engine} result",
            Notes = $"{engine} notes",
            SourceLanguage = "en",
            TargetLanguage = targetLanguage,
            Engine = engine,
            IsFinal = isFinal,
            StatusMessage = engine == "AI" ? "\u5728\u7EBF\u7FFB\u8BD1\u6210\u529F\u3002" : "\u79BB\u7EBF\u7FFB\u8BD1"
        };
    }
}

internal static class ChineseInputTranslationServiceTests
{
    public static async Task RunAsync()
    {
        await ChineseToEnglishUsesAiWhenAvailableAsync();
        await ChineseToEnglishFallsBackToOfflineOnTimeoutAsync();
        await ChineseToJapaneseUsesAiWhenAvailableAsync();
        await ChineseToJapaneseDoesNotFallbackToEnglishAsync();
        await EmptyInputDoesNotCallServicesAsync();
    }

    private static async Task ChineseToEnglishUsesAiWhenAvailableAsync()
    {
        SaveSettings(timeoutMilliseconds: 200, enableAi: true);
        var service = CreateService(
            new FakeTranslationService("AI", TimeSpan.FromMilliseconds(5), false, true),
            (_, _, _, _) => Task.FromResult(ArgosTranslateResult.Success("offline")));

        var result = await service.TranslateAsync("\u4F60\u597D", English(), CancellationToken.None);
        Assert(result.Engine == "AI", "Chinese to English should use AI when AI succeeds.");
        Assert(result.TargetLanguage == "English", "Target language should be English.");
    }

    private static async Task ChineseToEnglishFallsBackToOfflineOnTimeoutAsync()
    {
        SaveSettings(timeoutMilliseconds: 1000, enableAi: true);
        var service = CreateService(
            new FakeTranslationService("AI", TimeSpan.FromMilliseconds(1200), false, true),
            (text, from, to, _) =>
            {
                Assert(text == "\u4F60\u597D", "Argos should receive Chinese source text.");
                Assert(from == "zh" && to == "en", "English fallback should use zh->en.");
                return Task.FromResult(ArgosTranslateResult.Success("Hello, this is an offline translation result."));
            });

        var result = await service.TranslateAsync("\u4F60\u597D", English(), CancellationToken.None);
        Assert(result.Engine == "Argos Offline", "English timeout should use Argos offline fallback.");
        Assert(result.TranslatedText.Contains("offline", StringComparison.Ordinal), "English timeout should show offline translation.");
        Assert(result.StatusMessage.Contains('\u82F1'), "English timeout status should mention English offline result.");
    }

    private static async Task ChineseToJapaneseUsesAiWhenAvailableAsync()
    {
        SaveSettings(timeoutMilliseconds: 200, enableAi: true);
        var service = CreateService(
            new FakeTranslationService("AI", TimeSpan.FromMilliseconds(5), false, true),
            (_, _, _, _) => Task.FromResult(ArgosTranslateResult.Success("offline")));

        var result = await service.TranslateAsync("\u4F60\u597D", Japanese(), CancellationToken.None);
        Assert(result.Engine == "AI", "Chinese to Japanese should use AI when AI succeeds.");
        Assert(result.TargetLanguage == "Japanese", "Target language should be Japanese.");
    }

    private static async Task ChineseToJapaneseDoesNotFallbackToEnglishAsync()
    {
        SaveSettings(timeoutMilliseconds: 1000, enableAi: true);
        var argosCalled = false;
        var service = CreateService(
            new FakeTranslationService("AI", TimeSpan.FromMilliseconds(1200), false, true),
            (_, _, _, _) =>
            {
                argosCalled = true;
                return Task.FromResult(ArgosTranslateResult.Success("hello"));
            });

        var result = await service.TranslateAsync("\u4F60\u597D", Japanese(), CancellationToken.None);
        Assert(!argosCalled, "Non-English timeout must not call English offline fallback.");
        Assert(result.TranslatedText == string.Empty, "Non-English timeout should not show English translation.");
        Assert(result.StatusMessage.Contains('\u6682'), "Non-English timeout should explain no offline package.");
    }

    private static async Task EmptyInputDoesNotCallServicesAsync()
    {
        SaveSettings(timeoutMilliseconds: 20, enableAi: true);
        var aiCalled = false;
        var argosCalled = false;
        var service = CreateService(
            new DelegateTranslationService((_, _, _) =>
            {
                aiCalled = true;
                return Task.FromResult(new TranslationResult());
            }),
            (_, _, _, _) =>
            {
                argosCalled = true;
                return Task.FromResult(ArgosTranslateResult.Success("offline"));
            });

        var result = await service.TranslateAsync("   ", English(), CancellationToken.None);
        Assert(!aiCalled && !argosCalled, "Empty input should not call translation services.");
        Assert(result.StatusMessage.Contains('\u8F93'), "Empty input should prompt for Chinese text.");
    }

    private static ChineseInputTranslationService CreateService(
        ITranslationService aiService,
        Func<string, string, string, CancellationToken, Task<ArgosTranslateResult>> argosTranslateAsync)
    {
        return new ChineseInputTranslationService(new SettingsService(), aiService, argosTranslateAsync);
    }

    private static TargetLanguageOption English() => new()
    {
        DisplayName = "\u82F1\u6587",
        TargetLanguage = "English",
        Code = "en"
    };

    private static TargetLanguageOption Japanese() => new()
    {
        DisplayName = "\u65E5\u6587",
        TargetLanguage = "Japanese",
        Code = "ja"
    };

    private static void SaveSettings(int timeoutMilliseconds, bool enableAi)
    {
        new SettingsService().Save(new AppSettings
        {
            AiApiKey = "test-key",
            TargetLanguage = "\u4E2D\u6587",
            EnableAiTranslation = enableAi,
            EnableOfflineTranslation = true,
            AiTimeoutMilliseconds = timeoutMilliseconds
        });
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    private sealed class DelegateTranslationService(
        Func<string, string, CancellationToken, Task<TranslationResult>> translateAsync) : ITranslationService
    {
        public Task<TranslationResult> TranslateAsync(string sourceText, string targetLanguage, CancellationToken cancellationToken)
        {
            return translateAsync(sourceText, targetLanguage, cancellationToken);
        }
    }
}

internal static class OcrSmokeTests
{
    public static async Task RunAsync()
    {
        using var bitmap = new Drawing.Bitmap(640, 180);
        using (var graphics = Drawing.Graphics.FromImage(bitmap))
        using (var font = new Drawing.Font("Arial", 56, Drawing.FontStyle.Bold))
        {
            graphics.Clear(Drawing.Color.White);
            graphics.DrawString("HELLO WORLD", font, Drawing.Brushes.Black, new Drawing.PointF(24, 42));
        }

        var text = await new OcrService().RecognizeTextAsync(bitmap, CancellationToken.None);
        if (!text.Contains("HELLO", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"OCR did not recognize expected text. Actual: {text}");
        }
    }
}

internal static class PaddleOcrSmokeTests
{
    public static async Task RunAsync()
    {
        using var bitmap = new Drawing.Bitmap(520, 140);
        using (var graphics = Drawing.Graphics.FromImage(bitmap))
        using (var font = new Drawing.Font("Arial", 42, Drawing.FontStyle.Bold))
        {
            graphics.Clear(Drawing.Color.White);
            graphics.DrawString("offset voltage", font, Drawing.Brushes.Black, new Drawing.PointF(24, 36));
        }

        using var service = new PaddleOcrService();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var text = await service.RecognizeTextAsync(bitmap, timeout.Token);
        if (!text.Contains("offset", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"PaddleOCR did not recognize expected text. Actual: {text}");
        }
    }
}

internal static class OfflineTranslationServiceTests
{
    public static async Task RunAsync()
    {
        var service = new OfflineTranslationService(new FakePhoneticService(), argosTranslateClient: null);
        var result = await service.TranslateAsync("Current loop bandwidth", "\u4E2D\u6587", CancellationToken.None);
        if (result.Engine != "Offline Dictionary" || !result.TranslatedText.Contains('\u7535'))
        {
            throw new InvalidOperationException($"Dictionary fallback should remain available. Actual: {result.Engine} / {result.TranslatedText}");
        }

        var unknown = await service.TranslateAsync("uncovered technical sentence", "\u4E2D\u6587", CancellationToken.None);
        if (!unknown.TranslatedText.Contains('\u6682'))
        {
            throw new InvalidOperationException($"Unknown text should fail loudly as fallback. Actual: {unknown.TranslatedText}");
        }
    }
}

internal static class ArgosTranslateClientSmokeTests
{
    public static async Task RunAsync()
    {
        SaveSettingsForArgos();
        var client = new ArgosTranslateClient(new SettingsService());
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var result = await client.TranslateAsync("Related products\nSee TSV911 and TSV912", timeout.Token);
        if (!result.Ok)
        {
            throw new InvalidOperationException($"Argos client should translate with installed model. Error: {result.Error}");
        }

        if (result.TranslatedText.Split('\n').Length != 2)
        {
            throw new InvalidOperationException($"Argos client should preserve line count. Actual: {result.TranslatedText}");
        }

        if (result.TranslatedText.Contains('\uFFFD') || !result.TranslatedText.Any(IsCjk))
        {
            throw new InvalidOperationException($"Argos client should return readable UTF-8 Chinese. Actual: {result.TranslatedText}");
        }

        var second = await client.TranslateAsync("Portable devices", timeout.Token);
        if (!second.Ok || !second.TranslatedText.Any(IsCjk))
        {
            throw new InvalidOperationException($"Argos client should support repeated calls. Actual: {second.Error} / {second.TranslatedText}");
        }
    }

    private static void SaveSettingsForArgos()
    {
        new SettingsService().Save(new AppSettings
        {
            ArgosPythonPath = "python",
            TargetLanguage = "\u4E2D\u6587",
            EnableOfflineTranslation = true,
            EnableAiTranslation = false
        });
    }

    private static bool IsCjk(char ch)
    {
        return ch is >= '\u4E00' and <= '\u9FFF';
    }
}

internal static class WordLookupServiceTests
{
    public static async Task RunAsync()
    {
        SaveSettingsForWordLookup();
        var argos = new ArgosTranslateClient(new SettingsService());
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var service = new WordLookupService(new FakePhoneticService(), argos, aiWordLookupService: null);

        var phrase = await service.LookupAsync("excellent speed", timeout.Token);
        if (phrase.Word != "excellent speed" || !phrase.Translation.Any(IsCjk))
        {
            throw new InvalidOperationException($"Short phrase lookup should use Argos fallback. Actual: {phrase}");
        }
    }

    private static void SaveSettingsForWordLookup()
    {
        new SettingsService().Save(new AppSettings
        {
            ArgosPythonPath = "python",
            TargetLanguage = "\u4E2D\u6587",
            EnableOfflineTranslation = true,
            EnableAiTranslation = false
        });
    }

    private static bool IsCjk(char ch)
    {
        return ch is >= '\u4E00' and <= '\u9FFF';
    }
}

internal static class OcrTextPostProcessorTests
{
    public static void Run()
    {
        var text = OcrTextPostProcessor.Normalize("from 2 . 1 V to 5 . 5V with 1 \u00B5A current");
        if (!text.Contains("2.1V", StringComparison.Ordinal) || !text.Contains("5.5V", StringComparison.Ordinal) || !text.Contains("1\u00B5A", StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Expected generic unit spacing repair. Actual: {text}");
        }

        var flattened = "Features LOW input offset voltage: 1 . 5mV max (A grade) Rail-to-rail input and output Wide bandwidth 20MHz Stable for gain 4or -3 LOW power consumption: 820pA typ High output current: 35mA Operating from 2 . 5V to 5 . 5V LOW input bias current,1pA typ ESD internal protection \u2265 5kV";
        var restored = OcrTextPostProcessor.Normalize(flattened);
        if (restored.Split('\n').Length < 8
            || !restored.Contains("\nRail-to-rail input and output", StringComparison.Ordinal)
            || !restored.Contains("\nESD internal protection", StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Expected datasheet list line breaks to be restored. Actual: {restored}");
        }
    }
}

internal static class OcrQualityEvaluatorTests
{
    public static void Run()
    {
        if (!OcrQualityEvaluator.IsSuspicious("optimized for VO \u300At e and IOW noise"))
        {
            throw new InvalidOperationException("Suspicious OCR text should trigger enhanced OCR.");
        }

        var better = OcrQualityEvaluator.ChooseBetter("VO \u300At e", "voltage");
        if (!better.Equals("voltage", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Quality evaluator should choose cleaner OCR text.");
        }
    }
}

internal static class TranslationTextQualityEvaluatorTests
{
    public static void Run()
    {
        if (!TranslationTextQualityEvaluator.LooksReadable("\u8F93\u5165\u5931\u8C03\u7535\u538B"))
        {
            throw new InvalidOperationException("Normal Chinese translation should be readable.");
        }

        if (TranslationTextQualityEvaluator.LooksReadable("\uFFFD\uFFFD\uFFFD\uFFFD\uFFFD"))
        {
            throw new InvalidOperationException("Replacement-character mojibake should be rejected.");
        }

        if (TranslationTextQualityEvaluator.LooksReadable("???? ?????"))
        {
            throw new InvalidOperationException("Question-mark mojibake should be rejected.");
        }
    }
}

internal static class AiCorrectionNoteApplierTests
{
    public static void Run()
    {
        var source = "The SGM721 / 2 / 3 / 4 are a fam ily Of single amplifiers optimized for IOW VO \u300At e.";
        var notes = "corrected 'fam ily' to 'family', 'IOW VO \u300At e' to 'low voltage', 'Of' to 'of'";
        var corrected = AiCorrectionNoteApplier.Apply(source, notes);

        if (!corrected.Contains("family", StringComparison.Ordinal)
            || !corrected.Contains("low voltage", StringComparison.Ordinal)
            || corrected.Contains("fam ily", StringComparison.Ordinal)
            || corrected.Contains("IOW", StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"AI correction notes should update source text. Actual: {corrected}");
        }
    }
}

internal static class SelectedTextQualityEvaluatorTests
{
    public static void Run()
    {
        var selectedParagraph = "The SGM721/2/3/4 are a family of single, dual and quad operational amplifiers.";
        if (!SelectedTextQualityEvaluator.LooksLikeUserSelection(selectedParagraph))
        {
            throw new InvalidOperationException("A normal selected paragraph should pass the quality gate.");
        }

        var uiPollutedText = """
            缈昏瘧缁撴灉
            SGMICRO
            GENERAL DESCRIPTION
            The SGM721/2/3/4 are a family of single amplifiers.
            FEATURES
            Input Offset Voltage
            Supply Voltage Range
            鏈楄
            澶嶅埗
            """;
        if (SelectedTextQualityEvaluator.LooksLikeUserSelection(uiPollutedText))
        {
            throw new InvalidOperationException("UI-polluted accessibility text should be rejected.");
        }

        var coordinateText = """
            242,28,35,479,90
            244,80,37,479,90
            244,132,37,479,90
            244,182,37,479,90
            244,234,37,479,90
            36,284,35,61,90
            """;
        if (SelectedTextQualityEvaluator.LooksLikeUserSelection(coordinateText))
        {
            throw new InvalidOperationException("Coordinate-list selection artifacts should be rejected.");
        }
    }
}

internal static class FloatingTranslationViewModelTests
{
    public static void Run()
    {
        var viewModel = new FloatingTranslationViewModel(new TranslationResult
        {
            SourceText = "source",
            TranslatedText = string.Empty,
            Notes = string.Empty,
            Engine = "\u5904\u7406\u4E2D",
            IsFinal = false,
            StatusMessage = string.Empty
        }, new FakeSpeechService());

        if (!viewModel.StatusMessage.Contains('\u6B63'))
        {
            throw new InvalidOperationException("Empty status should show a processing message.");
        }

        viewModel.Update(new TranslationResult
        {
            SourceText = "source",
            TranslatedText = "done",
            Notes = "ok",
            Engine = "AI",
            TextSource = "\u771F\u5B9E\u9009\u533A",
            IsFinal = true,
            StatusMessage = "\u5728\u7EBF\u7FFB\u8BD1\u6210\u529F\u3002"
        });

        if (!viewModel.StatusMessage.Contains('\u6210'))
        {
            throw new InvalidOperationException("Success status should be visible.");
        }

        if (!viewModel.SourceDisplay.Contains("AI", StringComparison.Ordinal)
            || !viewModel.SourceDisplay.Contains('\u771F'))
        {
            throw new InvalidOperationException($"Source display should include text source and engine. Actual: {viewModel.SourceDisplay}");
        }
    }
}

internal sealed class FakeSpeechService : ISpeechService
{
    public Task SpeakAsync(string text, string cultureName, CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}

internal sealed class FakePhoneticService : IPhoneticService
{
    public Task<List<PhoneticItem>> GetAmericanPhoneticsAsync(string text, CancellationToken cancellationToken)
    {
        return Task.FromResult(new List<PhoneticItem>());
    }
}

