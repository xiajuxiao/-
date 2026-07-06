using System.Windows;
using TranslatorTool.Services;

namespace TranslatorTool;

public partial class App : System.Windows.Application
{
    public SettingsService SettingsService { get; private set; } = null!;
    public MainWindow Shell { get; private set; } = null!;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        SettingsService = new SettingsService();
        SettingsService.EnsureSettingsFile();

        Shell = new MainWindow(SettingsService);
        MainWindow = Shell;

        // 程序默认后台常驻，主窗口只作为消息宿主，不主动显示。
        Shell.Hide();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        Shell?.Dispose();
        base.OnExit(e);
    }
}
