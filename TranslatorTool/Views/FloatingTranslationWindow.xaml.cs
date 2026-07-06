using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Threading;
using TranslatorTool.Services;
using TranslatorTool.ViewModels;
using Point = System.Windows.Point;

namespace TranslatorTool.Views;

public partial class FloatingTranslationWindow : Window
{
    private readonly FloatingTranslationViewModel _viewModel;
    private readonly WordLookupService _wordLookupService;
    private readonly ISpeechService _speechService;
    private CancellationTokenSource? _wordLookupCancellation;
    private CancellationTokenSource? _selectionPopupCancellation;
    private string _popupWord = string.Empty;

    public FloatingTranslationWindow(
        FloatingTranslationViewModel viewModel,
        WordLookupService wordLookupService,
        ISpeechService speechService)
    {
        InitializeComponent();
        _viewModel = viewModel;
        _wordLookupService = wordLookupService;
        _speechService = speechService;
        DataContext = viewModel;
    }

    public void UpdateResult(Models.TranslationResult result)
    {
        _viewModel.Update(result);
        UpdateLayout();
    }

    public string CurrentTranslatedText => _viewModel.TranslatedText;
    public string CurrentNotes => _viewModel.Notes;
    public string CurrentEngine => _viewModel.Engine;
    public List<Models.PhoneticItem> CurrentPhonetics => _viewModel.Phonetics.ToList();

    public void ShowNear(Int32Rect? anchor)
    {
        Show();
        UpdateLayout();

        var point = anchor.HasValue
            ? new Point(anchor.Value.X + anchor.Value.Width + 12, anchor.Value.Y)
            : GetMousePosition();

        PlaceWithinScreen(point);
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);

        // 悬浮窗展示时不抢占当前输入焦点。
        var handle = new WindowInteropHelper(this).Handle;
        NativeMethods.SetWindowLong(handle, NativeMethods.GwlExStyle,
            NativeMethods.GetWindowLong(handle, NativeMethods.GwlExStyle) | NativeMethods.WsExNoActivate);
    }

    private void PlaceWithinScreen(Point desired)
    {
        var left = desired.X;
        var top = desired.Y;
        var screenLeft = SystemParameters.VirtualScreenLeft;
        var screenTop = SystemParameters.VirtualScreenTop;
        var screenRight = screenLeft + SystemParameters.VirtualScreenWidth;
        var screenBottom = screenTop + SystemParameters.VirtualScreenHeight;

        if (left + ActualWidth > screenRight)
        {
            left = screenRight - ActualWidth - 8;
        }

        if (top + ActualHeight > screenBottom)
        {
            top = screenBottom - ActualHeight - 8;
        }

        Left = Math.Max(screenLeft + 8, left);
        Top = Math.Max(screenTop + 8, top);
    }

    private static Point GetMousePosition()
    {
        var position = System.Windows.Forms.Cursor.Position;
        return new Point(position.X + 16, position.Y + 16);
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void TitleBar_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (e.ClickCount == 1 && e.ButtonState == System.Windows.Input.MouseButtonState.Pressed)
        {
            try
            {
                DragMove();
            }
            catch (InvalidOperationException)
            {
                // 鼠标状态变化时 DragMove 可能失败，忽略即可。
            }
        }
    }

    private async void SelectableTextBox_MouseLeftButtonUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (sender is not System.Windows.Controls.TextBox textBox)
        {
            return;
        }

        await ShowWordPopupForSelectionAsync(textBox, TimeSpan.FromMilliseconds(40));
    }

    private async void SelectableTextBox_SelectionChanged(object sender, RoutedEventArgs e)
    {
        if (sender is not System.Windows.Controls.TextBox textBox)
        {
            return;
        }

        _selectionPopupCancellation?.Cancel();
        _selectionPopupCancellation?.Dispose();
        _selectionPopupCancellation = new CancellationTokenSource();
        var token = _selectionPopupCancellation.Token;

        try
        {
            await Task.Delay(180, token);
            if (!token.IsCancellationRequested)
            {
                await ShowWordPopupForSelectionAsync(textBox, TimeSpan.Zero);
            }
        }
        catch (OperationCanceledException)
        {
            // A newer selection change will handle the popup.
        }
    }

    private async Task ShowWordPopupForSelectionAsync(System.Windows.Controls.TextBox textBox, TimeSpan delay)
    {
        if (delay > TimeSpan.Zero)
        {
            await Task.Delay(delay);
        }

        await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Background);
        var selectedText = textBox.SelectedText?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(selectedText))
        {
            WordPopup.IsOpen = false;
            return;
        }

        _wordLookupCancellation?.Cancel();
        _wordLookupCancellation?.Dispose();
        _wordLookupCancellation = new CancellationTokenSource(TimeSpan.FromSeconds(8));

        PopupWordText.Text = selectedText.Trim();
        PopupPhoneticText.Text = "\u6B63\u5728\u67E5\u8BE2...";
        PopupTranslationText.Text = string.Empty;
        WordPopup.IsOpen = true;

        try
        {
            var result = await _wordLookupService.LookupAsync(selectedText, _wordLookupCancellation.Token);
            if (string.IsNullOrWhiteSpace(result.Word))
            {
                WordPopup.IsOpen = false;
                return;
            }

            _popupWord = result.Word;
            PopupWordText.Text = result.Word;
            PopupPhoneticText.Text = result.Phonetic;
            PopupTranslationText.Text = result.Translation;
            WordPopup.IsOpen = true;
        }
        catch (OperationCanceledException)
        {
            WordPopup.IsOpen = false;
        }
        catch (Exception ex)
        {
            PopupPhoneticText.Text = "\u67E5\u8BE2\u5931\u8D25";
            PopupTranslationText.Text = ex.Message;
        }
    }

    private void WordPopup_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
    {
        WordPopup.IsOpen = false;
    }

    private async void PopupSpeakButton_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_popupWord))
        {
            return;
        }

        try
        {
            await _speechService.SpeakAsync(_popupWord, "en-US", CancellationToken.None);
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show($"\u6717\u8BFB\u5931\u8D25\uFF1A{ex.Message}", "\u7A97\u53E3\u7FFB\u8BD1\u5DE5\u5177", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }
}
