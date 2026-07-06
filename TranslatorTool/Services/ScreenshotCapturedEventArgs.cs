using System.Drawing;
using System.Windows;

namespace TranslatorTool.Services;

public sealed class ScreenshotCapturedEventArgs(Bitmap image, Int32Rect screenBounds) : EventArgs
{
    public Bitmap Image { get; } = image;
    public Int32Rect ScreenBounds { get; } = screenBounds;
}
