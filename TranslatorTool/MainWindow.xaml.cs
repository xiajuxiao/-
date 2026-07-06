using System.Windows;
using System.Windows.Input;
using Forms = System.Windows.Forms;
using Drawing = System.Drawing;
using Point = System.Windows.Point;
using TranslatorTool.Data;
using TranslatorTool.Models;
using TranslatorTool.Services;
using TranslatorTool.ViewModels;
using TranslatorTool.Views;

namespace TranslatorTool;

public partial class MainWindow : Window, IDisposable
{
    private readonly SettingsService _settingsService;
    private readonly IOcrService _ocrService;
    private readonly PaddleOcrService _paddleOcrService;
    private readonly AiVisionOcrService _aiVisionOcrService;
    private readonly OcrCoordinator _ocrCoordinator;
    private readonly ArgosTranslateClient _argosTranslateClient;
    private readonly WordLookupService _wordLookupService;
    private readonly AiTranslationService _aiTranslationService;
    private readonly ChineseInputTranslationService _chineseInputTranslationService;
    private readonly TranslationCoordinator _translationCoordinator;
    private readonly HistoryService _historyService;
    private readonly ISpeechService _speechService;
    private readonly HotkeyService _hotkeyService = new();
    private readonly Forms.NotifyIcon _notifyIcon;
    private bool _isDisposed;
    private bool _isExiting;

    public MainWindow(SettingsService settingsService)
    {
        _settingsService = settingsService;
        var phoneticService = new PhoneticService();
        _ocrService = new OcrService();
        _paddleOcrService = new PaddleOcrService();
        _aiVisionOcrService = new AiVisionOcrService(settingsService);
        _ocrCoordinator = new OcrCoordinator(_paddleOcrService, _ocrService, _aiVisionOcrService);
        _argosTranslateClient = new ArgosTranslateClient(settingsService);
        _wordLookupService = new WordLookupService(phoneticService, _argosTranslateClient, new AiWordLookupService(settingsService));
        _aiTranslationService = new AiTranslationService(settingsService);
        _chineseInputTranslationService = new ChineseInputTranslationService(settingsService, _aiTranslationService, _argosTranslateClient);
        _speechService = new SpeechService();
        _historyService = new HistoryService(new AppDbContext());
        _translationCoordinator = new TranslationCoordinator(
            settingsService,
            new OfflineTranslationService(phoneticService, _argosTranslateClient),
            _aiTranslationService);
        InitializeComponent();

        _notifyIcon = CreateNotifyIcon();
        Closing += MainWindow_Closing;

        try
        {
            _hotkeyService.Register(this);
            _hotkeyService.HotkeyPressed += HotkeyService_HotkeyPressed;
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(ex.Message, "绐楀彛缈昏瘧宸ュ叿", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        if (!_isExiting)
        {
            e.Cancel = true;
            Hide();
        }
    }

    private Forms.NotifyIcon CreateNotifyIcon()
    {
        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add("截图翻译", null, (_, _) => Dispatcher.Invoke(StartScreenshotTranslation));
        menu.Items.Add("输入中文翻译", null, (_, _) => Dispatcher.Invoke(OpenChineseInputTranslationWindow));
        menu.Items.Add("历史记录", null, (_, _) => Dispatcher.Invoke(OpenHistoryWindow));
        menu.Items.Add("设置", null, (_, _) => Dispatcher.Invoke(OpenSettingsWindow));
        menu.Items.Add("显示测试悬浮窗", null, (_, _) => Dispatcher.Invoke(ShowSampleFloatingWindow));
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add("退出", null, (_, _) => Dispatcher.Invoke(ExitApplication));

        var notifyIcon = new Forms.NotifyIcon
        {
            Text = "窗口翻译工具",
            Icon = Drawing.SystemIcons.Application,
            ContextMenuStrip = menu,
            Visible = true
        };
        notifyIcon.DoubleClick += (_, _) => Dispatcher.Invoke(() =>
        {
            Show();
            Activate();
        });
        return notifyIcon;
    }

    private void HotkeyService_HotkeyPressed(object? sender, HotkeyPressedEventArgs e)
    {
        if (e.HotkeyName == HotkeyNames.ScreenshotTranslation)
        {
            StartScreenshotTranslation();
        }
    }

    private void StartScreenshotTranslation()
    {
        DiagnosticsLog.Write("Starting screenshot translation overlay.");
        var overlay = new ScreenshotOverlayWindow();
        overlay.ScreenshotCaptured += async (_, args) =>
        {
            await RunScreenshotTranslationAsync(args);
        };
        overlay.Show();
    }

    private async Task RunScreenshotTranslationAsync(ScreenshotCapturedEventArgs args)
    {
        using var image = args.Image;

        try
        {
            DiagnosticsLog.Write($"Run screenshot translation: {args.ScreenBounds.X},{args.ScreenBounds.Y},{args.ScreenBounds.Width},{args.ScreenBounds.Height}.");
            var floatingWindow = ShowFloatingWindow(CreateProgressResult(
                sourceText: string.Empty,
                translatedText: string.Empty,
                notes: string.Empty,
                statusMessage: "正在 OCR 识别，请稍等..."), args.ScreenBounds);

            OcrResult ocrResult;
            try
            {
                using var ocrTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                ocrResult = await _ocrCoordinator.RecognizeScreenshotAsync(image, ocrTimeout.Token);
            }
            catch (OperationCanceledException)
            {
                floatingWindow.UpdateResult(CreateProgressResult(
                    sourceText: string.Empty,
                    translatedText: "OCR 识别超时",
                    notes: "请重新框选更清晰或更小的区域后再试。",
                    statusMessage: "OCR 识别超时。"));
                return;
            }

            if (string.IsNullOrWhiteSpace(ocrResult.Text))
            {
                floatingWindow.UpdateResult(CreateProgressResult(
                    sourceText: string.Empty,
                    translatedText: "OCR 未识别到文字",
                    notes: "请重新框选更清晰的区域。",
                    statusMessage: "OCR 未识别到文字。"));
                return;
            }

            floatingWindow.UpdateResult(CreateProgressResult(
                ocrResult.Text,
                "正在翻译...",
                $"已完成 OCR（{ocrResult.Engine}），正在请求翻译。",
                $"OCR 识别成功（{ocrResult.Engine}），正在在线翻译，请稍等...",
                ocrResult.Engine));

            await TranslateIntoWindowAsync(
                ocrResult.Text,
                floatingWindow,
                $"Alt + Q 截图 OCR ({ocrResult.Engine})",
                fromOcr: true,
                textSource: ocrResult.Engine,
                CancellationToken.None);
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show($"截图翻译失败：{ex.Message}", "窗口翻译工具", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private static TranslationResult CreateProgressResult(
        string sourceText,
        string translatedText,
        string notes,
        string statusMessage,
        string textSource = "")
    {
        return new TranslationResult
        {
            SourceText = sourceText,
            TranslatedText = translatedText,
            Notes = notes,
            Engine = "处理中",
            TextSource = textSource,
            IsFinal = false,
            StatusMessage = statusMessage
        };
    }

    private async Task TranslateIntoWindowAsync(
        string sourceText,
        FloatingTranslationWindow floatingWindow,
        string triggerSource,
        bool fromOcr,
        string textSource,
        CancellationToken cancellationToken)
    {
        var historySaved = 0;
        await _translationCoordinator.TranslateWithUpdatesAsync(
            sourceText,
            result =>
            {
                result.TextSource = textSource;
                Dispatcher.Invoke(() => floatingWindow.UpdateResult(result));
                if (result.IsFinal && Interlocked.Exchange(ref historySaved, 1) == 0)
                {
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            await _historyService.AddAsync(result, triggerSource, fromOcr, audioPath: null, CancellationToken.None);
                        }
                        catch (Exception ex)
                        {
                            Dispatcher.Invoke(() => System.Windows.MessageBox.Show(
                                $"保存历史记录失败：{ex.Message}",
                                "窗口翻译工具",
                                MessageBoxButton.OK,
                                MessageBoxImage.Warning));
                        }
                    });
                }
            },
            cancellationToken);
    }

    private void ShowSampleFloatingWindow()
    {
        ShowFloatingWindow(new TranslationResult
        {
            SourceText = "Current loop bandwidth",
            TranslatedText = "电流环带宽",
            Notes = "指电机控制中电流环能够响应电流变化的频率范围。",
            SourceLanguage = "en",
            TargetLanguage = "zh-CN",
            Engine = "Preview",
            IsFinal = true,
            Phonetics =
            [
                new PhoneticItem { Word = "current", AmericanPhonetic = "/ˈkɜːrənt/", Meaning = "当前的；电流" },
                new PhoneticItem { Word = "loop", AmericanPhonetic = "/luːp/", Meaning = "环路" },
                new PhoneticItem { Word = "bandwidth", AmericanPhonetic = "/ˈbændwɪdθ/", Meaning = "带宽" }
            ]
        }, null);
    }

    private FloatingTranslationWindow ShowFloatingWindow(TranslationResult result, Int32Rect? anchor)
    {
        FloatingTranslationWindow? window = null;
        FloatingTranslationViewModel? viewModel = null;
        viewModel = new FloatingTranslationViewModel(
            result,
            _speechService,
            () => RetryOnlineTranslationAsync(window!, viewModel!.SourceText, viewModel.TextSource));
        window = new FloatingTranslationWindow(viewModel, _wordLookupService, _speechService);
        window.ShowNear(anchor);
        return window;
    }

    private async Task RetryOnlineTranslationAsync(FloatingTranslationWindow window, string sourceText, string textSource)
    {
        window.UpdateResult(new TranslationResult
        {
            SourceText = sourceText,
            TranslatedText = window.CurrentTranslatedText,
            Notes = window.CurrentNotes,
            Phonetics = window.CurrentPhonetics,
            Engine = window.CurrentEngine,
            TextSource = textSource,
            IsFinal = false,
            StatusMessage = "\u6B63\u5728\u91CD\u65B0\u8BF7\u6C42\u5728\u7EBF\u7FFB\u8BD1..."
        });

        try
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
            var result = await _translationCoordinator.TranslateAiOnlyAsync(sourceText, timeout.Token);
            result.TextSource = textSource;
            window.UpdateResult(result);
            await _historyService.AddAsync(result, "Refresh online translation", fromOcr: false, audioPath: null, CancellationToken.None);
        }
        catch (Exception ex)
        {
            window.UpdateResult(new TranslationResult
            {
                SourceText = sourceText,
                TranslatedText = window.CurrentTranslatedText,
                Notes = $"\u5728\u7EBF\u7FFB\u8BD1\u91CD\u8BD5\u5931\u8D25\uFF1A{ex.Message}",
                Phonetics = window.CurrentPhonetics,
                Engine = window.CurrentEngine,
                TextSource = textSource,
                IsFinal = true,
                StatusMessage = "\u5728\u7EBF\u7FFB\u8BD1\u4ECD\u7136\u5931\u8D25\uFF0C\u5F53\u524D\u4E3A\u79BB\u7EBF\u7ED3\u679C\u3002"
            });
        }
    }

    private void OpenHistoryWindow()
    {
        new HistoryWindow(new HistoryViewModel(_historyService)).Show();
    }

    private void OpenChineseInputTranslationWindow()
    {
        var viewModel = new ChineseInputTranslationViewModel(_chineseInputTranslationService);
        var window = new ChineseInputTranslationWindow(viewModel);
        window.Show();
        window.Activate();
    }

    private void OpenSettingsWindow()
    {
        var viewModel = new SettingsViewModel(_settingsService);
        new SettingsWindow(viewModel).ShowDialog();
    }

    private void HideButton_Click(object sender, RoutedEventArgs e)
    {
        Hide();
    }

    private void ExitApplication()
    {
        _isExiting = true;
        Close();
        System.Windows.Application.Current.Shutdown();
    }

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;
        _hotkeyService.HotkeyPressed -= HotkeyService_HotkeyPressed;
        _hotkeyService.Dispose();
        _paddleOcrService.Dispose();
        _argosTranslateClient.Dispose();
        if (_speechService is IDisposable disposableSpeechService)
        {
            disposableSpeechService.Dispose();
        }
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
    }
}
