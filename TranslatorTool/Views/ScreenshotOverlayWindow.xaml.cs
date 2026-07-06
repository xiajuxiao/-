using System.Drawing;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using TranslatorTool.Services;
using Drawing = System.Drawing;
using Point = System.Windows.Point;

namespace TranslatorTool.Views;

public partial class ScreenshotOverlayWindow : Window
{
    private Point? _startPoint;

    public ScreenshotOverlayWindow()
    {
        InitializeComponent();

        Left = SystemParameters.VirtualScreenLeft;
        Top = SystemParameters.VirtualScreenTop;
        Width = SystemParameters.VirtualScreenWidth;
        Height = SystemParameters.VirtualScreenHeight;
        Loaded += (_, _) => Activate();
    }

    public event Func<object?, ScreenshotCapturedEventArgs, Task>? ScreenshotCaptured;

    private void Window_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left)
        {
            return;
        }

        _startPoint = e.GetPosition(this);
        SelectionRectangle.Visibility = Visibility.Visible;
        CaptureMouse();
    }

    private void Window_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (_startPoint is null || e.LeftButton != MouseButtonState.Pressed)
        {
            return;
        }

        DrawSelection(_startPoint.Value, e.GetPosition(this));
    }

    private async void Window_MouseUp(object sender, MouseButtonEventArgs e)
    {
        if (_startPoint is null || e.ChangedButton != MouseButton.Left)
        {
            return;
        }

        ReleaseMouseCapture();
        var endPoint = e.GetPosition(this);
        var bounds = GetScreenBounds(_startPoint.Value, endPoint);
        _startPoint = null;

        if (bounds.Width < 3 || bounds.Height < 3)
        {
            Close();
            return;
        }

        try
        {
            Hide();
            var bitmap = CaptureScreen(bounds);
            DiagnosticsLog.Write($"Screenshot captured: {bounds.X},{bounds.Y},{bounds.Width},{bounds.Height}.");
            if (ScreenshotCaptured is not null)
            {
                await ScreenshotCaptured.Invoke(this, new ScreenshotCapturedEventArgs(bitmap, bounds));
            }
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show($"截图失败：{ex.Message}", "窗口翻译工具", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        finally
        {
            Close();
        }
    }

    private void Window_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            Close();
        }
    }

    private void DrawSelection(Point start, Point end)
    {
        var left = Math.Min(start.X, end.X);
        var top = Math.Min(start.Y, end.Y);
        var width = Math.Abs(start.X - end.X);
        var height = Math.Abs(start.Y - end.Y);

        Canvas.SetLeft(SelectionRectangle, left);
        Canvas.SetTop(SelectionRectangle, top);
        SelectionRectangle.Width = width;
        SelectionRectangle.Height = height;
    }

    private Int32Rect GetScreenBounds(Point start, Point end)
    {
        var left = (int)Math.Round(Math.Min(start.X, end.X) + Left);
        var top = (int)Math.Round(Math.Min(start.Y, end.Y) + Top);
        var right = (int)Math.Round(Math.Max(start.X, end.X) + Left);
        var bottom = (int)Math.Round(Math.Max(start.Y, end.Y) + Top);
        return new Int32Rect(left, top, Math.Max(0, right - left), Math.Max(0, bottom - top));
    }

    private static Bitmap CaptureScreen(Int32Rect bounds)
    {
        var bitmap = new Bitmap(bounds.Width, bounds.Height);
        using var graphics = Drawing.Graphics.FromImage(bitmap);
        graphics.CopyFromScreen(bounds.X, bounds.Y, 0, 0, new Drawing.Size(bounds.Width, bounds.Height));
        return bitmap;
    }
}
