using System.Text.RegularExpressions;

namespace TranslatorTool.Services;

public static class AiCorrectionNoteApplier
{
    private static readonly Regex QuotedCorrectionPattern = new(
        @"['""](?<from>[^'""]{1,80})['""]\s*(?:to|->|=>|→)\s*['""](?<to>[^'""]{1,80})['""]",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static string Apply(string sourceText, string notes)
    {
        if (string.IsNullOrWhiteSpace(sourceText) || string.IsNullOrWhiteSpace(notes))
        {
            return sourceText;
        }

        var corrected = sourceText;
        foreach (Match match in QuotedCorrectionPattern.Matches(notes))
        {
            var from = match.Groups["from"].Value.Trim();
            var to = match.Groups["to"].Value.Trim();
            if (!IsSafeCorrection(from, to))
            {
                continue;
            }

            corrected = corrected.Replace(from, to, StringComparison.Ordinal);
            corrected = Regex.Replace(
                corrected,
                Regex.Escape(from).Replace(@"\ ", @"\s+"),
                to,
                RegexOptions.IgnoreCase);
        }

        return OcrTextPostProcessor.Normalize(corrected);
    }

    private static bool IsSafeCorrection(string from, string to)
    {
        if (string.IsNullOrWhiteSpace(from) || string.IsNullOrWhiteSpace(to))
        {
            return false;
        }

        if (from.Equals(to, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (from.Length > 80 || to.Length > 80)
        {
            return false;
        }

        return from.Any(char.IsLetterOrDigit) && to.Any(char.IsLetterOrDigit);
    }
}
