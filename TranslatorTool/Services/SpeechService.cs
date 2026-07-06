using System.Globalization;
using System.Speech.Synthesis;

namespace TranslatorTool.Services;

public class SpeechService : ISpeechService, IDisposable
{
    private readonly SpeechSynthesizer _synthesizer = new();
    private readonly SemaphoreSlim _speakLock = new(1, 1);

    public async Task SpeakAsync(string text, string language, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            throw new ArgumentException("朗读文本不能为空。", nameof(text));
        }

        await _speakLock.WaitAsync(cancellationToken);
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            SelectVoice(language);

            var completion = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
            EventHandler<SpeakCompletedEventArgs>? handler = null;
            handler = (_, args) =>
            {
                _synthesizer.SpeakCompleted -= handler;
                if (args.Error is not null)
                {
                    completion.TrySetException(args.Error);
                }
                else if (args.Cancelled)
                {
                    completion.TrySetCanceled(cancellationToken);
                }
                else
                {
                    completion.TrySetResult(null);
                }
            };

            using var registration = cancellationToken.Register(() =>
            {
                _synthesizer.SpeakAsyncCancelAll();
                completion.TrySetCanceled(cancellationToken);
            });

            _synthesizer.SpeakCompleted += handler;
            _synthesizer.SpeakAsync(text);
            await completion.Task;
        }
        finally
        {
            _speakLock.Release();
        }
    }

    public void Dispose()
    {
        _synthesizer.Dispose();
        _speakLock.Dispose();
    }

    private void SelectVoice(string language)
    {
        var cultureName = language.StartsWith("zh", StringComparison.OrdinalIgnoreCase)
            ? "zh-CN"
            : "en-US";

        var culture = CultureInfo.GetCultureInfo(cultureName);
        var voice = _synthesizer
            .GetInstalledVoices(culture)
            .FirstOrDefault(voice => voice.Enabled);

        if (voice is not null)
        {
            _synthesizer.SelectVoice(voice.VoiceInfo.Name);
        }
    }
}
