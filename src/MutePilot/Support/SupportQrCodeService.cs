using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using QRCoder;

namespace MutePilot.Support;

public static class SupportQrCodeService
{
    private static readonly Uri TossSupportQrUri =
        new("Assets/toss-support-qr.jpg", UriKind.Relative);

    public static ImageSource? TryLoadTossSupportQr()
    {
        try
        {
            var resource = Application.GetResourceStream(TossSupportQrUri);

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
            Debug.WriteLine($"Toss support QR resource lookup failed: {exception.Message}");
            return null;
        }
    }

    public static ImageSource? TryCreateAccountQrCodeFallback()
    {
        try
        {
            var pngBytes = PngByteQRCodeHelper.GetQRCode(
                SupportInfo.QrPayload,
                QRCodeGenerator.ECCLevel.Q,
                14,
                true);
            using var stream = new MemoryStream(pngBytes, writable: false);
            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.StreamSource = stream;
            image.EndInit();
            image.Freeze();
            return image;
        }
        catch (Exception exception)
        {
            Debug.WriteLine($"Support QR generation failed: {exception.Message}");
            return null;
        }
    }
}
