namespace TranslatorTool.Models;

public sealed class TargetLanguageOption
{
    public required string DisplayName { get; init; }
    public required string TargetLanguage { get; init; }
    public required string Code { get; init; }

    public override string ToString() => DisplayName;
}

