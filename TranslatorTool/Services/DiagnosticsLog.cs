using System.IO;

namespace TranslatorTool.Services;

public static class DiagnosticsLog
{
    private static readonly object SyncRoot = new();

    public static void Write(string message)
    {
        try
        {
            var folder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "TranslatorTool");
            Directory.CreateDirectory(folder);
            var path = Path.Combine(folder, "diagnostics.log");
            lock (SyncRoot)
            {
                File.AppendAllText(path, $"{DateTimeOffset.Now:O} {message}{Environment.NewLine}");
            }
        }
        catch
        {
            // 诊断日志不能影响主流程。
        }
    }
}
