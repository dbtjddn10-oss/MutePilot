using System.Diagnostics;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using QRCoder;

namespace MutePilot.Support;

public static class SupportQrCodeService
{
    public static ImageSource? TryCreateAccountQrCode()
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
