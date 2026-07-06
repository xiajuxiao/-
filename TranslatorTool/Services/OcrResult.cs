namespace TranslatorTool.Services;

public sealed record OcrResult(string Text, string Engine, bool IsReliable)
{
    public static OcrResult Empty(string engine) => new(string.Empty, engine, false);
}
