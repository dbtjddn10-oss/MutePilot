using System.Windows;

namespace MutePilot.Support;

public partial class SupportWindow : Window
{
    public SupportWindow()
    {
        InitializeComponent();
        var qrCode = SupportQrCodeService.TryCreateAccountQrCode();
        SupportQrImage.Source = qrCode;
        QrFallbackText.Visibility = qrCode is null
            ? Visibility.Visible
            : Visibility.Collapsed;
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
