using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using TranslatorTool.Models;
using TranslatorTool.Services;

namespace TranslatorTool.ViewModels;

public sealed class ChineseInputTranslationViewModel : ViewModelBase
{
    private readonly ChineseInputTranslationService _translationService;
    private string _sourceText = string.Empty;
    private string _translatedText = string.Empty;
    private string _notes = string.Empty;
    private string _statusMessage = "\u8BF7\u8F93\u5165\u4E2D\u6587\u3002";
    private bool _isTranslating;
    private TargetLanguageOption _selectedLanguage;

    public ChineseInputTranslationViewModel(ChineseInputTranslationService translationService)
    {
        _translationService = translationService;
        TargetLanguages =
        [
            new TargetLanguageOption { DisplayName = "\u82F1\u6587", TargetLanguage = "English", Code = "en" },
            new TargetLanguageOption { DisplayName = "\u65E5\u6587", TargetLanguage = "Japanese", Code = "ja" },
            new TargetLanguageOption { DisplayName = "\u97E9\u6587", TargetLanguage = "Korean", Code = "ko" },
            new TargetLanguageOption { DisplayName = "\u5FB7\u6587", TargetLanguage = "German", Code = "de" },
            new TargetLanguageOption { DisplayName = "\u6CD5\u6587", TargetLanguage = "French", Code = "fr" },
            new TargetLanguageOption { DisplayName = "\u897F\u73ED\u7259\u6587", TargetLanguage = "Spanish", Code = "es" },
            new TargetLanguageOption { DisplayName = "\u4FC4\u6587", TargetLanguage = "Russian", Code = "ru" }
        ];
        _selectedLanguage = TargetLanguages[0];
        TranslateCommand = new RelayCommand(() => _ = TranslateAsync(), () => !IsTranslating);
        CopyCommand = new RelayCommand(CopyTranslation, () => !string.IsNullOrWhiteSpace(TranslatedText));
        ClearCommand = new RelayCommand(Clear, () => !IsTranslating);
    }

    public ObservableCollection<TargetLanguageOption> TargetLanguages { get; }
    public ICommand TranslateCommand { get; }
    public ICommand CopyCommand { get; }
    public ICommand ClearCommand { get; }

    public string SourceText
    {
        get => _sourceText;
        set
        {
            _sourceText = value;
            OnPropertyChanged();
        }
    }

    public TargetLanguageOption SelectedLanguage
    {
        get => _selectedLanguage;
        set
        {
            _selectedLanguage = value;
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
            RaiseCommandStates();
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

    public string StatusMessage
    {
        get => _statusMessage;
        private set
        {
            _statusMessage = value;
            OnPropertyChanged();
        }
    }

    public bool IsTranslating
    {
        get => _isTranslating;
        private set
        {
            _isTranslating = value;
            OnPropertyChanged();
            RaiseCommandStates();
        }
    }

    private async Task TranslateAsync()
    {
        if (string.IsNullOrWhiteSpace(SourceText))
        {
            StatusMessage = "\u8BF7\u8F93\u5165\u4E2D\u6587\u540E\u518D\u7FFB\u8BD1\u3002";
            return;
        }

        IsTranslating = true;
        StatusMessage = "\u6B63\u5728\u5728\u7EBF\u7FFB\u8BD1...";
        Notes = string.Empty;

        try
        {
            var result = await _translationService.TranslateAsync(SourceText, SelectedLanguage, CancellationToken.None);
            TranslatedText = result.TranslatedText;
            Notes = result.Notes;
            StatusMessage = result.StatusMessage;
        }
        catch (Exception ex)
        {
            StatusMessage = $"\u7FFB\u8BD1\u5931\u8D25\uFF1A{ex.Message}";
        }
        finally
        {
            IsTranslating = false;
        }
    }

    private void CopyTranslation()
    {
        if (!string.IsNullOrWhiteSpace(TranslatedText))
        {
            System.Windows.Clipboard.SetText(TranslatedText);
        }
    }

    private void Clear()
    {
        SourceText = string.Empty;
        TranslatedText = string.Empty;
        Notes = string.Empty;
        StatusMessage = "\u8BF7\u8F93\u5165\u4E2D\u6587\u3002";
    }

    private void RaiseCommandStates()
    {
        if (TranslateCommand is RelayCommand translateCommand)
        {
            translateCommand.RaiseCanExecuteChanged();
        }

        if (CopyCommand is RelayCommand copyCommand)
        {
            copyCommand.RaiseCanExecuteChanged();
        }

        if (ClearCommand is RelayCommand clearCommand)
        {
            clearCommand.RaiseCanExecuteChanged();
        }
    }
}
