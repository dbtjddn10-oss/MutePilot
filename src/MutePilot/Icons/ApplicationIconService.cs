using System.Diagnostics;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace MutePilot.Icons;

public sealed class ApplicationIconService : IApplicationIconService
{
    private readonly Dictionary<string, ImageSource> _applicationCache =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, ImageSource> _pathCache =
        new(StringComparer.OrdinalIgnoreCase);

    public ImageSource GetIcon(string applicationKey, IReadOnlyList<int> processIds)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(applicationKey);

        if (_applicationCache.TryGetValue(applicationKey, out var cachedIcon))
        {
            return cachedIcon;
        }

        foreach (var processId in processIds.Distinct())
        {
            var icon = TryGetProcessIcon(processId);

            if (icon is not null)
            {
                _applicationCache[applicationKey] = icon;
                return icon;
            }
        }

        if (processIds.Count > 0)
        {
            _applicationCache[applicationKey] = FallbackIcon;
        }

        return FallbackIcon;
    }

    private ImageSource? TryGetProcessIcon(int processId)
    {
        try
        {
            using var process = Process.GetProcessById(processId);
            var executablePath = process.MainModule?.FileName;

            if (string.IsNullOrWhiteSpace(executablePath))
            {
                return null;
            }

            if (_pathCache.TryGetValue(executablePath, out var cachedIcon))
            {
                return cachedIcon;
            }

            using var icon = System.Drawing.Icon.ExtractAssociatedIcon(executablePath);

            if (icon is null)
            {
                return null;
            }

            var source = Imaging.CreateBitmapSourceFromHIcon(
                icon.Handle,
                Int32Rect.Empty,
                BitmapSizeOptions.FromWidthAndHeight(32, 32));
            source.Freeze();
            _pathCache[executablePath] = source;
            return source;
        }
        catch (Exception exception)
        {
            Debug.WriteLine($"Application icon lookup failed for PID {processId}: {exception.Message}");
            return null;
        }
    }

    private static ImageSource CreateFallbackIcon()
    {
        var background = new GeometryDrawing(
            new SolidColorBrush(Color.FromRgb(54, 76, 105)),
            new Pen(new SolidColorBrush(Color.FromRgb(112, 151, 199)), 1),
            new RectangleGeometry(new Rect(2, 4, 28, 24), 6, 6));
        var window = new GeometryDrawing(
            new SolidColorBrush(Color.FromRgb(144, 181, 226)),
            null,
            new RectangleGeometry(new Rect(8, 10, 16, 12), 2, 2));
        var titleBar = new GeometryDrawing(
            new SolidColorBrush(Color.FromRgb(62, 104, 159)),
            null,
            new RectangleGeometry(new Rect(8, 10, 16, 4), 2, 2));
        var drawing = new DrawingGroup { Children = { background, window, titleBar } };
        drawing.Freeze();
        var image = new DrawingImage(drawing);
        image.Freeze();
        return image;
    }

    private static ImageSource FallbackIcon { get; } = CreateFallbackIcon();
}
