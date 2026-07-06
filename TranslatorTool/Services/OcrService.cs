using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Graphics.Imaging;
using Windows.Globalization;
using Windows.Media.Ocr;

namespace TranslatorTool.Services;

public class OcrService : IOcrService
{
    public async Task<string> RecognizeTextAsync(Bitmap image, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var engine = OcrEngine.TryCreateFromLanguage(new Language("en-US"))
            ?? OcrEngine.TryCreateFromUserProfileLanguages();
        if (engine is null)
        {
            throw new InvalidOperationException("当前系统没有可用的 Windows OCR 语言包。");
        }

        using var softwareBitmap = CreateSoftwareBitmap(image);

        var result = await engine.RecognizeAsync(softwareBitmap).AsTask(cancellationToken);
        return result.Text.Trim();
    }

    private static SoftwareBitmap CreateSoftwareBitmap(Bitmap image)
    {
        using var normalized = new Bitmap(image.Width, image.Height, PixelFormat.Format32bppArgb);
        using (var graphics = Graphics.FromImage(normalized))
        {
            graphics.DrawImage(image, 0, 0, image.Width, image.Height);
        }

        var rectangle = new Rectangle(0, 0, normalized.Width, normalized.Height);
        var data = normalized.LockBits(rectangle, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        try
        {
            var bytes = new byte[Math.Abs(data.Stride) * data.Height];
            Marshal.Copy(data.Scan0, bytes, 0, bytes.Length);
            var softwareBitmap = new SoftwareBitmap(BitmapPixelFormat.Bgra8, normalized.Width, normalized.Height, BitmapAlphaMode.Premultiplied);
            softwareBitmap.CopyFromBuffer(bytes.AsBuffer());
            return softwareBitmap;
        }
        finally
        {
            normalized.UnlockBits(data);
        }
    }
}
