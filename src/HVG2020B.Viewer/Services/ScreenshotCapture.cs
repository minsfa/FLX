using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace HVG2020B.Viewer.Services;

public static class ScreenshotCapture
{
    /// <summary>
    /// Captures a WPF Window as a PNG image file.
    /// Returns the saved file path.
    /// </summary>
    public static string CaptureWindow(Window window, string folderPath, string fileNameWithoutExtension)
    {
        if (!Directory.Exists(folderPath))
            Directory.CreateDirectory(folderPath);

        var dpiX = VisualTreeHelper.GetDpi(window).PixelsPerInchX;
        var dpiY = VisualTreeHelper.GetDpi(window).PixelsPerInchY;

        var width = (int)(window.ActualWidth * dpiX / 96.0);
        var height = (int)(window.ActualHeight * dpiY / 96.0);

        var renderTarget = new RenderTargetBitmap(width, height, dpiX, dpiY, PixelFormats.Pbgra32);
        renderTarget.Render(window);

        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(renderTarget));

        var filePath = Path.Combine(folderPath, $"{fileNameWithoutExtension}.png");
        using var stream = File.Create(filePath);
        encoder.Save(stream);

        return filePath;
    }
}
