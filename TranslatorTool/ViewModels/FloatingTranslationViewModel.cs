using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using TranslatorTool.Models;
using TranslatorTool.Services;

namespace TranslatorTool.ViewModels;

public class FloatingTranslationViewModel : ViewModelBase
{
    private readonly ISpeechService _speechService;
    private readonly Func<Task>? _refreshOnlineAsync;
    private string _sourceText = string.Empty;
    private string _translatedText = string.Empty;
    private string _notes = string.Empty;
    private string _engine = string.Empty;
    private string _textSource = string.Empty;
    private bool _isFinal;
    private bool _isOnlineRefreshVisible;
    private string _statusMessage = string.Empty;

    public FloatingTranslationViewModel(
        TranslationResult result,
        ISpeechService speechService,
        Func<Task>? refreshOnlineAsync = null)
    {
        _speechService = speechService;
        _refreshOnlineAsync = refreshOnlineAsync;
        Phonetics = [];
        CopyCommand = new RelayCommand(CopyTranslation);
        SpeakCommand = new RelayCommand(() => _ = SpeakAsync());
        RefreshOnlineCommand = new RelayCommand(() => _ = RefreshOnlineAsync());
        Update(result);
    }

    public string SourceText
    {
        get => _sourceText;
        private set
        {
            _sourceText = value;
            OnPropertyChanged();
        }
    }

    public string TranslatedText
    {
        get => _translatedText;
        private set
        {
            _translatedText = value;
            OnPropertyChanged();
        }
    }

    public string Notes
    {
        get => _notes;
        private set
        {
            _notes = value;
            OnPropertyChanged();
        }
    }

    public string Engine
    {
        get => _engine;
        private set
        {
            _engine = value;
            OnPropertyChanged();
        }
    }

    public string TextSource
    {
        get => _textSource;
        private set
        {
            _textSource = value;
            OnPropertyChanged();
        }
    }

    public bool IsFinal
    {
        get => _isFinal;
        private set
        {
            _isFinal = value;
            OnPropertyChanged();
        }
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set
        {
            _statusMessage = string.IsNullOrWhiteSpace(value) ? "\u6B63\u5728\u5904\u7406\uFF0C\u8BF7\u7A0D\u7B49..." : value;
            OnPropertyChanged();
        }
    }

    public bool IsOnlineRefreshVisible
    {
        get => _isOnlineRefreshVisible;
        private set
        {
            _isOnlineRefreshVisible = value;
            OnPropertyChanged();
        }
    }

    public ObservableCollection<PhoneticItem> Phonetics { get; }
    public ICommand CopyCommand { get; }
    public ICommand SpeakCommand { get; }
    public ICommand RefreshOnlineCommand { get; }

    public string SourceDisplay
    {
        get
        {
            var source = string.IsNullOrWhiteSpace(TextSource) ? string.Empty : TextSource;
            var engine = string.IsNullOrWhiteSpace(Engine) ? string.Empty : Engine;
            if (string.IsNullOrWhiteSpace(source) && string.IsNullOrWhiteSpace(engine))
            {
                return "\u6765\u6E90\uFF1A\u5904\u7406\u4E2D";
            }

            if (string.IsNullOrWhiteSpace(engine))
            {
                return $"\u6765\u6E90\uFF1A{source}";
            }

            if (string.IsNullOrWhiteSpace(source))
            {
                return $"\u6765\u6E90\uFF1A{engine}";
            }

            return $"\u6765\u6E90\uFF1A{source} / {engine}";
        }
    }

    public void Update(TranslationResult result)
    {
        SourceText = result.SourceText;
        TranslatedText = result.TranslatedText;
        Notes = result.Notes;
        Engine = result.Engine;
        TextSource = result.TextSource;
        IsFinal = result.IsFinal;
        StatusMessage = result.StatusMessage;
        IsOnlineRefreshVisible = result.IsFinal
                                 && StatusMessage.Contains("\u5F53\u524D\u4E3A\u79BB\u7EBF\u7ED3\u679C", StringComparison.Ordinal)
                                 && _refreshOnlineAsync is not null;

        Phonetics.Clear();
        foreach (var item in result.Phonetics)
        {
            Phonetics.Add(item);
        }

        OnPropertyChanged(nameof(SourceDisplay));
    }

    private async Task RefreshOnlineAsync()
    {
        if (_refreshOnlineAsync is null)
        {
            return;
        }

        await _refreshOnlineAsync();
    }

    private void CopyTranslation()
    {
        System.Windows.Clipboard.SetText(TranslatedText);
    }

    private async Task SpeakAsync()
    {
        try
        {
            await _speechService.SpeakAsync(SourceText, "en-US", CancellationToken.None);
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show($"\u6717\u8BFB\u5931\u8D25\uFF1A{ex.Message}", "\u7A97\u53E3\u7FFB\u8BD1\u5DE5\u5177", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }
}
