namespace TranslatorTool.Models;

public class TranslationResult
{
    public string SourceText { get; set; } = string.Empty;
    public string TranslatedText { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;
    public List<PhoneticItem> Phonetics { get; set; } = [];
    public string SourceLanguage { get; set; } = string.Empty;
    public string TargetLanguage { get; set; } = "中文";
    public string Engine { get; set; } = string.Empty;
    public string TextSource { get; set; } = string.Empty;
    public bool IsFinal { get; set; }
    public string StatusMessage { get; set; } = string.Empty;
}
