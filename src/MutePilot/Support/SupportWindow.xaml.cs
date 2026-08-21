using System.Windows;

namespace MutePilot.Support;

public partial class SupportWindow : Window
{
    public SupportWindow()
    {
        InitializeComponent();
        var qrCode = SupportQrCodeService.TryLoadTossSupportQr();
        var isTossQr = qrCode is not null;
        qrCode ??= SupportQrCodeService.TryCreateAccountQrCodeFallback();
        SupportQrImage.Source = qrCode;
        QrFallbackText.Visibility = qrCode is null
            ? Visibility.Visible
            : Visibility.Collapsed;
        QrInstructionText.Text = isTossQr
            ? "토스로 QR을 스캔해 후원할 수 있습니다."
            : "계좌정보 QR을 스캔하거나 계좌번호를 복사해 주세요.";
    }

    private void CopyAccountButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Clipboard.SetText(SupportInfo.AccountNumber);
            CopyStatusText.Foreground = FindResource("SuccessBrush") as System.Windows.Media.Brush;
            CopyStatusText.Text = "계좌번호를 복사했습니다.";
        }
        catch (Exception)
        {
            CopyStatusText.Foreground = FindResource("DangerBrush") as System.Windows.Media.Brush;
            CopyStatusText.Text = "계좌번호를 복사하지 못했습니다. 잠시 후 다시 시도해 주세요.";
        }
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();
}
