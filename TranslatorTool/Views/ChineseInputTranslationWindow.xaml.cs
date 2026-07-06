using System.Windows;
using TranslatorTool.ViewModels;

namespace TranslatorTool.Views;

public partial class ChineseInputTranslationWindow : Window
{
    public ChineseInputTranslationWindow(ChineseInputTranslationViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void SourceTextBox_PreviewMouseRightButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (sender is not System.Windows.Controls.TextBox textBox || !System.Windows.Clipboard.ContainsText())
        {
            return;
        }

        textBox.Focus();
        textBox.Paste();
        e.Handled = true;
    }
}
