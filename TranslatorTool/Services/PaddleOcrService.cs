using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using OpenCvSharp;
using Sdcb.PaddleInference;
using Sdcb.PaddleOCR;
using Sdcb.PaddleOCR.Models.Local;

namespace TranslatorTool.Services;

public sealed class PaddleOcrService : IDisposable
{
    private readonly Lazy<PaddleOcrAll> _ocr = new(CreateOcr, LazyThreadSafetyMode.ExecutionAndPublication);

    public Task<string> RecognizeTextAsync(Bitmap image, CancellationToken cancellationToken)
    {
        return Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            using var stream = new MemoryStream();
            image.Save(stream, ImageFormat.Png);
            using var mat = Cv2.ImDecode(stream.ToArray(), ImreadModes.Color);
            var result = _ocr.Value.Run(mat);
            return OcrTextPostProcessor.Normalize(result.Text);
        }, cancellationToken);
    }

    public void Dispose()
    {
        if (_ocr.IsValueCreated)
        {
            _ocr.Value.Dispose();
        }
    }

    private static PaddleOcrAll CreateOcr()
    {
        var model = LocalFullModels.ChineseV5;
        return new PaddleOcrAll(model, PaddleDevice.Mkldnn())
        {
            AllowRotateDetection = false,
            Enable180Classification = false
        };
    }
}
