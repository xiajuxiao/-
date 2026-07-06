namespace TranslatorTool.Services;

public static class TranslationTextQualityEvaluator
{
    public static bool LooksReadable(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var replacementCount = text.Count(ch => ch == '\uFFFD');
        if (replacementCount > 0)
        {
            return false;
        }

        var visibleCount = text.Count(ch => !char.IsWhiteSpace(ch));
        if (visibleCount == 0)
        {
            return false;
        }

        var questionMarkRun = 0;
        foreach (var ch in text)
        {
            questionMarkRun = ch == '?' ? questionMarkRun + 1 : 0;
            if (questionMarkRun >= 4)
            {
                return false;
            }
        }

        return true;
    }
}
