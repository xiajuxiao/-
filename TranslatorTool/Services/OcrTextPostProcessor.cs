using System.Text.RegularExpressions;

namespace TranslatorTool.Services;

public static class OcrTextPostProcessor
{
    public static string Normalize(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        var normalized = text.Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Replace('\u03BC', '\u00B5');

        normalized = Regex.Replace(normalized, @"(?<=\d)\s*\.\s*(?=\d\s*[A-Za-z\u00B5])", ".");
        normalized = Regex.Replace(normalized, @"(?<=\d)\s+(?=[A-Za-z\u00B5]{1,3}\b)", string.Empty);
        normalized = RestoreDatasheetListLineBreaks(normalized);
        normalized = Regex.Replace(normalized, @"[ \t]{2,}", " ");
        normalized = Regex.Replace(normalized, @" ?\n ?", "\n");
        return normalized.Trim();
    }

    private static string RestoreDatasheetListLineBreaks(string text)
    {
        var normalized = text;
        normalized = Regex.Replace(normalized, @"\b(Features?)\s+(?=[A-Z])", "$1\n", RegexOptions.IgnoreCase);
        normalized = Regex.Replace(normalized, @"\b(Related products?)\s+(?=[A-Z])", "\n$1\n", RegexOptions.IgnoreCase);
        normalized = Regex.Replace(normalized, @"\b(Applications?)\s+(?=[A-Z])", "\n$1\n", RegexOptions.IgnoreCase);

        var itemStarts = new[]
        {
            "Low input offset voltage",
            "Rail-to-rail input and output",
            "Wide bandwidth",
            "Stable for gain",
            "Low power consumption",
            "High output current",
            "Operating from",
            "Low input bias current",
            "ESD internal protection",
            "Battery-powered applications",
            "Portable devices",
            "Signal conditioning",
            "Medical instrumentation",
            "Automotive applications",
            "See TSV"
        };

        foreach (var itemStart in itemStarts)
        {
            normalized = Regex.Replace(
                normalized,
                $@"(?<!^)(?<!\n)\s+({Regex.Escape(itemStart)})",
                "\n$1",
                RegexOptions.IgnoreCase);
        }

        normalized = Regex.Replace(normalized, @"\s+(\(A grade\))", "\n$1", RegexOptions.IgnoreCase);
        return normalized;
    }
}
