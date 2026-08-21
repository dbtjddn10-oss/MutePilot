using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using MutePilot.Audio;

namespace MutePilot;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private readonly IAudioService _audioService;

    public MainWindow()
    {
        InitializeComponent();
        _audioService = new AudioService();
    }

    private void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        RefreshMasterAudioState();
    }

    private void MasterMuteButton_Click(object sender, RoutedEventArgs e)
    {
        MasterMuteButton.IsEnabled = false;
        AudioErrorText.Visibility = Visibility.Collapsed;

        try
        {
            var isMuted = _audioService.ToggleMasterMuteState();
            UpdateMasterAudioState(isMuted);
        }
        catch (Exception exception)
        {
            ShowAudioError(exception);
        }
        finally
        {
            MasterMuteButton.IsEnabled = true;
        }
    }

    private void RefreshMasterAudioState()
    {
        try
        {
            var isMuted = _audioService.GetMasterMuteState();
            UpdateMasterAudioState(isMuted);
        }
        catch (Exception exception)
        {
            ShowAudioError(exception);
        }
        finally
        {
            MasterMuteButton.IsEnabled = true;
        }
    }

    private void UpdateMasterAudioState(bool isMuted)
    {
        MasterAudioStatusText.Text = isMuted
            ? "현재 상태: 음소거"
            : "현재 상태: 음소거 해제";
        MasterMuteButton.Content = isMuted ? "음소거 해제" : "음소거";
        AudioErrorText.Visibility = Visibility.Collapsed;
    }

    private void ShowAudioError(Exception exception)
    {
        System.Diagnostics.Debug.WriteLine(exception);

        MasterAudioStatusText.Text = "현재 상태: 확인할 수 없음";
        MasterMuteButton.Content = "음소거 상태 전환";
        AudioErrorText.Text =
            "기본 오디오 장치를 제어할 수 없습니다. 장치 연결 상태를 확인한 뒤 다시 시도해 주세요.";
        AudioErrorText.Visibility = Visibility.Visible;
    }
}
