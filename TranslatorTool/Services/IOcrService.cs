using System.Drawing;

namespace TranslatorTool.Services;

public interface IOcrService
{
    Task<string> RecognizeTextAsync(Bitmap image, CancellationToken cancellationToken);
}
