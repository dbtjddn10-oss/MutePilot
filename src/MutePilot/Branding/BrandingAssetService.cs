using System.Diagnostics;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using DrawingIcon = System.Drawing.Icon;

namespace MutePilot.Branding;

public static class BrandingAssetService
{
    private static readonly Uri AppIconUri =
        new("Assets/app-icon.ico", UriKind.Relative);
    private static readonly Uri BrandIconUri =
        new("Assets/brand-icon.png", UriKind.Relative);

    public static ImageSource? TryLoadWindowIcon() => TryLoadImage(AppIconUri);

    public static ImageSource? TryLoadBrandIcon() => TryLoadImage(BrandIconUri);

    public static DrawingIcon? TryCreateTrayIcon()
    {
        try
        {
            var resource = Application.GetResourceStream(AppIconUri);

            if (resource is null)
            {
                return null;
            }

            using (resource.Stream)
            using (var source = new DrawingIcon(resource.Stream))
            {
                return (DrawingIcon)source.Clone();
            }
        }
        catch (Exception exception)
        {
            Debug.WriteLine($"Tray icon resource lookup failed: {exception.Message}");
            return null;
        }
    }

    private static ImageSource? TryLoadImage(Uri resourceUri)
    {
        try
        {
            var resource = Application.GetResourceStream(resourceUri);

            if (resource is null)
            {
                return null;
            }

            using (resource.Stream)
            {
                var decoder = BitmapDecoder.Create(
                    resource.Stream,
                    BitmapCreateOptions.PreservePixelFormat,
                    BitmapCacheOption.OnLoad);
                var frame = decoder.Frames.FirstOrDefault();
                frame?.Freeze();
                return frame;
            }
        }
        catch (Exception exception)
        {
            Debug.WriteLine($"Branding image resource lookup failed: {exception.Message}");
            return null;
        }
    }
}
