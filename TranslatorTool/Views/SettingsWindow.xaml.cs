using System.Windows;
using TranslatorTool.ViewModels;

namespace TranslatorTool.Views;

public partial class SettingsWindow : Window
{
    private readonly SettingsViewModel _viewModel;

    public SettingsWindow(SettingsViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = _viewModel;
        ApiKeyBox.Password = _viewModel.Settings.AiApiKey;
        _viewModel.Saved += (_, _) => Close();
    }

    private void ApiKeyBox_PasswordChanged(object sender, RoutedEventArgs e)
    {
        _viewModel.Settings.AiApiKey = ApiKeyBox.Password;
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
