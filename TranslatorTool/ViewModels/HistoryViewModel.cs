using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using TranslatorTool.Models;
using TranslatorTool.Services;

namespace TranslatorTool.ViewModels;

public class HistoryViewModel : ViewModelBase
{
    private readonly HistoryService _historyService;
    private TranslationHistoryItem? _selectedItem;
    private string _statusMessage = string.Empty;

    public HistoryViewModel(HistoryService historyService)
    {
        _historyService = historyService;
        Items = [];
        RefreshCommand = new RelayCommand(() => _ = LoadAsync());
        CopyTranslationCommand = new RelayCommand(CopySelectedTranslation, () => SelectedItem is not null);
        ClearCommand = new RelayCommand(() => _ = ClearAsync());
        _ = LoadAsync();
    }

    public ObservableCollection<TranslationHistoryItem> Items { get; }
    public ICommand RefreshCommand { get; }
    public ICommand CopyTranslationCommand { get; }
    public ICommand ClearCommand { get; }

    public TranslationHistoryItem? SelectedItem
    {
        get => _selectedItem;
        set
        {
            _selectedItem = value;
            OnPropertyChanged();
            if (CopyTranslationCommand is RelayCommand relayCommand)
            {
                relayCommand.RaiseCanExecuteChanged();
            }
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

    private async Task LoadAsync()
    {
        try
        {
            var items = await _historyService.GetRecentAsync(200, CancellationToken.None);
            Items.Clear();
            foreach (var item in items)
            {
                Items.Add(item);
            }

            StatusMessage = items.Count == 0 ? "暂无历史记录。" : $"共 {items.Count} 条历史记录。";
        }
        catch (Exception ex)
        {
            StatusMessage = $"读取历史记录失败：{ex.Message}";
        }
    }

    private void CopySelectedTranslation()
    {
        if (SelectedItem is null)
        {
            return;
        }

        System.Windows.Clipboard.SetText(SelectedItem.TranslatedText);
        StatusMessage = "译文已复制。";
    }

    private async Task ClearAsync()
    {
        try
        {
            await _historyService.ClearAsync(CancellationToken.None);
            Items.Clear();
            SelectedItem = null;
            StatusMessage = "历史记录已清空。";
        }
        catch (Exception ex)
        {
            StatusMessage = $"清空历史记录失败：{ex.Message}";
        }
    }
}
