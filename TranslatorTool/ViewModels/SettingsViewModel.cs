using System.Windows.Input;
using TranslatorTool.Models;
using TranslatorTool.Services;

namespace TranslatorTool.ViewModels;

public class SettingsViewModel : ViewModelBase
{
    private readonly SettingsService _settingsService;

    public SettingsViewModel(SettingsService settingsService)
    {
        _settingsService = settingsService;
        Settings = _settingsService.Load();
        SaveCommand = new RelayCommand(Save);
    }

    public AppSettings Settings { get; }
    public ICommand SaveCommand { get; }
    public event EventHandler? Saved;

    private void Save()
    {
        _settingsService.Save(Settings);
        Saved?.Invoke(this, EventArgs.Empty);
    }
}
