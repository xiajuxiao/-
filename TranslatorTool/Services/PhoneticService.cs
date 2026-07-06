using System.Text.RegularExpressions;
using TranslatorTool.Models;

namespace TranslatorTool.Services;

public partial class PhoneticService : IPhoneticService
{
    private static readonly HashSet<string> StopWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "a", "an", "the", "and", "or", "but", "if", "then", "else", "to", "of", "in", "on", "at", "for", "from",
        "with", "without", "by", "is", "are", "was", "were", "be", "been", "being", "this", "that", "these", "those",
        "it", "its", "as", "into", "than", "when", "while", "can", "could", "should", "would", "will", "may", "might"
    };

    private static readonly Dictionary<string, PhoneticItem> LocalDictionary = new(StringComparer.OrdinalIgnoreCase)
    {
        ["application"] = Item("application", "/\u02CC\u00E6pl\u026A\u02C8ke\u026A\u0283\u0259n/", "\u5E94\u7528"),
        ["applications"] = Item("applications", "/\u02CC\u00E6pl\u026A\u02C8ke\u026A\u0283\u0259nz/", "\u5E94\u7528"),
        ["automotive"] = Item("automotive", "/\u02CC\u0254\u02D0t\u0259\u02C8mo\u028At\u026Av/", "\u6C7D\u8F66\u7684"),
        ["bandwidth"] = Item("bandwidth", "/\u02C8b\u00E6ndw\u026Ad\u03B8/", "\u5E26\u5BBD"),
        ["battery"] = Item("battery", "/\u02C8b\u00E6t\u0259ri/", "\u7535\u6C60"),
        ["buffer"] = Item("buffer", "/\u02C8b\u028Cf\u0259r/", "\u7F13\u51B2\u5668"),
        ["conditioning"] = Item("conditioning", "/k\u0259n\u02C8d\u026A\u0283\u0259n\u026A\u014B/", "\u8C03\u7406\uFF1B\u8C03\u8282"),
        ["consumption"] = Item("consumption", "/k\u0259n\u02C8s\u028Cmp\u0283\u0259n/", "\u6D88\u8017"),
        ["control"] = Item("control", "/k\u0259n\u02C8tro\u028Al/", "\u63A7\u5236"),
        ["current"] = Item("current", "/\u02C8k\u025C\u02D0r\u0259nt/", "\u7535\u6D41\uFF1B\u5F53\u524D\u7684"),
        ["datasheet"] = Item("datasheet", "/\u02C8de\u026At\u0259 \u0283i\u02D0t/", "\u6570\u636E\u624B\u518C"),
        ["device"] = Item("device", "/d\u026A\u02C8va\u026As/", "\u5668\u4EF6"),
        ["devices"] = Item("devices", "/d\u026A\u02C8va\u026As\u026Az/", "\u5668\u4EF6"),
        ["driver"] = Item("driver", "/\u02C8dra\u026Av\u0259r/", "\u9A71\u52A8\u5668\uFF1B\u9A71\u52A8\u7A0B\u5E8F"),
        ["filtering"] = Item("filtering", "/\u02C8f\u026Alt\u0259r\u026A\u014B/", "\u6EE4\u6CE2"),
        ["frequency"] = Item("frequency", "/\u02C8fri\u02D0kw\u0259nsi/", "\u9891\u7387"),
        ["gain"] = Item("gain", "/\u0261e\u026An/", "\u589E\u76CA"),
        ["high"] = Item("high", "/ha\u026A/", "\u9AD8"),
        ["input"] = Item("input", "/\u02C8\u026Anp\u028At/", "\u8F93\u5165"),
        ["instrumentation"] = Item("instrumentation", "/\u02CC\u026Anstr\u0259men\u02C8te\u026A\u0283\u0259n/", "\u4EEA\u5668\u4EEA\u8868"),
        ["loop"] = Item("loop", "/lu\u02D0p/", "\u73AF\u8DEF"),
        ["low"] = Item("low", "/lo\u028A/", "\u4F4E"),
        ["medical"] = Item("medical", "/\u02C8med\u026Akl/", "\u533B\u7597\u7684"),
        ["offset"] = Item("offset", "/\u02C8\u0254\u02D0fset/", "\u504F\u79FB\uFF1B\u5931\u8C03"),
        ["output"] = Item("output", "/\u02C8a\u028Atp\u028At/", "\u8F93\u51FA"),
        ["portable"] = Item("portable", "/\u02C8p\u0254\u02D0rt\u0259bl/", "\u4FBF\u643A\u5F0F\u7684"),
        ["power"] = Item("power", "/\u02C8pa\u028A\u0259r/", "\u529F\u7387\uFF1B\u7535\u6E90"),
        ["powered"] = Item("powered", "/\u02C8pa\u028A\u0259rd/", "\u4F9B\u7535\u7684"),
        ["protection"] = Item("protection", "/pr\u0259\u02C8tek\u0283\u0259n/", "\u4FDD\u62A4"),
        ["rail"] = Item("rail", "/re\u026Al/", "\u7535\u6E90\u8F68"),
        ["register"] = Item("register", "/\u02C8red\u0292\u026Ast\u0259r/", "\u5BC4\u5B58\u5668"),
        ["response"] = Item("response", "/r\u026A\u02C8sp\u0251\u02D0ns/", "\u54CD\u5E94"),
        ["signal"] = Item("signal", "/\u02C8s\u026A\u0261n\u0259l/", "\u4FE1\u53F7"),
        ["stable"] = Item("stable", "/\u02C8ste\u026Abl/", "\u7A33\u5B9A\u7684"),
        ["thermal"] = Item("thermal", "/\u02C8\u03B8\u025C\u02D0rml/", "\u70ED\u7684"),
        ["threshold"] = Item("threshold", "/\u02C8\u03B8re\u0283ho\u028Ald/", "\u9608\u503C"),
        ["voltage"] = Item("voltage", "/\u02C8vo\u028Alt\u026Ad\u0292/", "\u7535\u538B"),
        ["world"] = Item("world", "/w\u025C\u02D0rld/", "\u4E16\u754C"),
        ["hello"] = Item("hello", "/h\u0259\u02C8lo\u028A/", "\u4F60\u597D")
    };

    public Task<List<PhoneticItem>> GetAmericanPhoneticsAsync(string text, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var words = EnglishWordRegex()
            .Matches(text)
            .Select(match => match.Value.ToLowerInvariant())
            .Distinct()
            .Where(word => word.Length > 1 && !StopWords.Contains(word))
            .OrderByDescending(word => LocalDictionary.ContainsKey(word))
            .ThenByDescending(word => word.Length)
            .Take(6);

        var result = words
            .Where(LocalDictionary.ContainsKey)
            .Select(word => LocalDictionary[word])
            .ToList();

        return Task.FromResult(result);
    }

    private static PhoneticItem Item(string word, string phonetic, string meaning)
    {
        return new PhoneticItem
        {
            Word = word,
            AmericanPhonetic = phonetic,
            Meaning = meaning
        };
    }

    [GeneratedRegex("[A-Za-z]+")]
    private static partial Regex EnglishWordRegex();
}
