using System.IO;
using System.Text.Json;
using TranslatorTool.Models;

namespace TranslatorTool.Services;

public class SettingsService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private readonly string _settingsPath;

    public SettingsService()
    {
        _settingsPath = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
    }

    public void EnsureSettingsFile()
    {
        if (!File.Exists(_settingsPath))
        {
            Save(new AppSettings());
        }
    }

    public AppSettings Load()
    {
        EnsureSettingsFile();

        try
        {
            var json = File.ReadAllText(_settingsPath);
            return JsonSerializer.Deserialize<AppSettings>(json, JsonOptions) ?? new AppSettings();
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"读取设置失败：{_settingsPath}", ex);
        }
    }

    public void Save(AppSettings settings)
    {
        try
        {
            var json = JsonSerializer.Serialize(settings, JsonOptions);
            File.WriteAllText(_settingsPath, json);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"保存设置失败：{_settingsPath}", ex);
        }
    }
}
