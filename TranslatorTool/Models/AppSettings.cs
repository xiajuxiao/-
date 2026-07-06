namespace TranslatorTool.Models;

public class AppSettings
{
    public string AiApiKey { get; set; } = string.Empty;
    public string AiBaseUrl { get; set; } = "https://api.openai.com/v1";
    public string AiTranslationModel { get; set; } = "gpt-4.1-mini";
    public string AiVisionOcrModel { get; set; } = "gpt-4.1-mini";
    public string ArgosPythonPath { get; set; } = "python";
    public string TargetLanguage { get; set; } = "中文";
    public bool EnableOfflineTranslation { get; set; } = true;
    public bool EnableAiTranslation { get; set; } = true;
    public int AiTimeoutMilliseconds { get; set; } = 1000;
}
