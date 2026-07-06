namespace TranslatorTool.Services;

public static class SelectedTextQualityEvaluator
{
    private static readonly string[] UiNoiseMarkers =
    [
        "翻译结果",
        "朗读",
        "复制",
        "开始",
        "插入",
        "编辑",
        "页面",
        "批注",
        "工具",
        "保护",
        "FEATURES",
        "Input Offset Voltage",
        "Supply Voltage Range"
    ];

    public static bool LooksLikeUserSelection(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var trimmed = text.Trim();
        if (trimmed.Length > 3000)
        {
            return false;
        }

        if (LooksLikeCoordinateList(trimmed))
        {
            return false;
        }

        var markerHits = UiNoiseMarkers.Count(marker => trimmed.Contains(marker, StringComparison.OrdinalIgnoreCase));
        if (markerHits >= 2)
        {
            return false;
        }

        var lines = trimmed.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (lines.Length >= 12)
        {
            var shortLines = lines.Count(line => line.Length <= 16);
            if (shortLines >= lines.Length / 2)
            {
                return false;
            }
        }

        var letterCount = trimmed.Count(char.IsLetter);
        var controlLikeCount = trimmed.Count(ch => ch is '\t' or '\u001B');
        return letterCount >= 2 && controlLikeCount == 0;
    }

    private static bool LooksLikeCoordinateList(string text)
    {
        var lines = text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (lines.Length < 4)
        {
            return false;
        }

        var coordinateLines = lines.Count(line =>
        {
            var parts = line.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            return parts.Length is >= 3 and <= 6
                   && parts.All(part => int.TryParse(part, out _));
        });

        return coordinateLines >= Math.Max(4, lines.Length * 3 / 4);
    }
}
