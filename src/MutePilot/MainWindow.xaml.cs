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
        RefreshApplicationSessions();
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

    private void ApplicationRefreshButton_Click(object sender, RoutedEventArgs e)
    {
        RefreshApplicationSessions();
    }

    private void ApplicationMuteButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button || button.Tag is not string applicationKey)
        {
            return;
        }

        button.IsEnabled = false;
        ApplicationRefreshButton.IsEnabled = false;
        ApplicationErrorText.Visibility = Visibility.Collapsed;

        try
        {
            _audioService.ToggleApplicationMute(applicationKey);
            RefreshApplicationSessions();
        }
        catch (Exception exception)
        {
            RefreshApplicationSessions();
            ShowApplicationError(
                "선택한 애플리케이션의 오디오 세션을 제어할 수 없습니다. 목록을 새로고침한 뒤 다시 시도해 주세요.",
                exception);
        }
        finally
        {
            button.IsEnabled = true;
            ApplicationRefreshButton.IsEnabled = true;
        }
    }

    private void RefreshApplicationSessions()
    {
        ApplicationRefreshButton.IsEnabled = false;
        ApplicationErrorText.Visibility = Visibility.Collapsed;

        try
        {
            var items = _audioService.GetActiveApplicationSessions()
                .Select(session => new ApplicationSessionItem(
                    session.ApplicationKey,
                    session.ApplicationName,
                    $"PID: {string.Join(", ", session.ProcessIds)} · 세션 {session.SessionCount}개",
                    session.HasMixedMuteState
                        ? "현재 상태: 일부 세션 음소거"
                        : session.IsMuted
                            ? "현재 상태: 음소거"
                            : "현재 상태: 음소거 해제",
                    session.IsMuted ? "음소거 해제" : "음소거"))
                .ToArray();

            ApplicationsItemsControl.ItemsSource = items;
            ApplicationEmptyText.Visibility = items.Length == 0
                ? Visibility.Visible
                : Visibility.Collapsed;
        }
        catch (Exception exception)
        {
            ApplicationsItemsControl.ItemsSource = null;
            ApplicationEmptyText.Visibility = Visibility.Collapsed;
            ShowApplicationError(
                "활성 애플리케이션 오디오 세션을 불러올 수 없습니다. 오디오 장치 상태를 확인해 주세요.",
                exception);
        }
        finally
        {
            ApplicationRefreshButton.IsEnabled = true;
        }
    }

    private void ShowApplicationError(string message, Exception exception)
    {
        System.Diagnostics.Debug.WriteLine(exception);

        ApplicationErrorText.Text = message;
        ApplicationErrorText.Visibility = Visibility.Visible;
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

    private sealed record ApplicationSessionItem(
        string ApplicationKey,
        string ApplicationName,
        string ProcessIdText,
        string StatusText,
        string ToggleButtonText);
}
