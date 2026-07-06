namespace TranslatorTool.Models;

public sealed record WordLookupResult(
    string Word,
    string Phonetic,
    string Translation);
