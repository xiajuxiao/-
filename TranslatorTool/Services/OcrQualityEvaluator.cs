using System.Text.RegularExpressions;

namespace TranslatorTool.Services;

public static class OcrQualityEvaluator
{
    private static readonly Regex SuspiciousPattern = new(
        @"[\u300A\u300B<>�]|(?:\bV[O0]\s*t\s*e\b)|(?:\bI\s*OW\b)|(?:\bfamiliy\b)|(?:\b[a-zA-Z]\s+[a-zA-Z]\s+[a-zA-Z]\b)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static bool IsSuspicious(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return true;
        }

        return Score(text) >= 3;
    }

    public static string ChooseBetter(string first, string second)
    {
        if (string.IsNullOrWhiteSpace(first))
        {
            return second;
        }

        if (string.IsNullOrWhiteSpace(second))
        {
            return first;
        }

        var firstScore = Score(first);
        var secondScore = Score(second);
        if (secondScore < firstScore)
        {
            return second;
        }

        if (secondScore == firstScore && second.Length > first.Length * 1.08)
        {
            return second;
        }

        return first;
    }

    private static int Score(string text)
    {
        var score = 0;
        score += SuspiciousPattern.Matches(text).Count * 3;
        score += text.Count(ch => ch == '?' || ch == '�');
        score += Regex.Matches(text, @"\s{3,}").Count;
        return score;
    }
}
