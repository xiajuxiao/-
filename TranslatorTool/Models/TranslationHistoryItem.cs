namespace TranslatorTool.Models;

public class TranslationHistoryItem
{
    public long Id { get; set; }
    public string SourceText { get; set; } = string.Empty;
    public string TranslatedText { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;
    public string PhoneticsJson { get; set; } = string.Empty;
    public string SourceLanguage { get; set; } = string.Empty;
    public string TargetLanguage { get; set; } = "中文";
    public string TriggerSource { get; set; } = string.Empty;
    public string Engine { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.Now;
    public bool FromOcr { get; set; }
    public string? AudioPath { get; set; }
}
