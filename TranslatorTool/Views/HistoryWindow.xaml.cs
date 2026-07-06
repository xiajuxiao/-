using System.Windows;
using TranslatorTool.ViewModels;

namespace TranslatorTool.Views;

public partial class HistoryWindow : Window
{
    public HistoryWindow(HistoryViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
