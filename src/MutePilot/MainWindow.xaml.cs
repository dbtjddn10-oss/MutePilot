using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using MutePilot.Audio;
using MutePilot.Branding;
using MutePilot.Hotkeys;
using MutePilot.Icons;
using MutePilot.Overlay;
using MutePilot.Security;
using MutePilot.Settings;
using MutePilot.Startup;
using MutePilot.Support;
using MutePilot.Tray;
using MutePilot.Theming;
using MutePilot.Volume;

namespace MutePilot;

public partial class MainWindow : Window
{
    private static readonly TimeSpan OverlayRefreshInterval = TimeSpan.FromSeconds(2);

    private readonly IAudioService _audioService = new AudioService();
    private readonly IVolumePresetToggleService _volumePresetToggleService;
    private readonly IHotkeyService _hotkeyService = new HotkeyService();
    private readonly ISettingsService _settingsService = new SettingsService();
    private readonly IStartupService _startupService = new StartupService();
    private readonly IPrivilegeService _privilegeService = new PrivilegeService();
    private readonly IApplicationIconService _applicationIconService = new ApplicationIconService();
    private readonly IOverlayService _overlayService;
    private readonly ITrayService _trayService;
    private readonly DispatcherTimer _overlayRefreshTimer;
    private AppSettings _settings = new();
    private IReadOnlyList<ApplicationAudioSession> _activeApplicationSessions = [];
    private bool? _masterIsMuted;
    private int? _masterVolumePercent;
    private bool _hotkeysInitialized;
    private bool _isCapturingHotkey;
    private bool _isOverlayRefreshRunning;
    private bool _isApplyingOverlayConfiguration;
    private bool _isClosed;
    private bool _isRealExitRequested;
    private bool _servicesStarted;
    private bool _settingsLoaded;
    private bool _isRefreshingApplicationItems;
    private bool _isSynchronizingPresetInputs;
    private bool _isUpdatingThemeSelection;
    private StartupStatus _startupStatus = new(StartupTaskState.Disabled);
    private long _audioStateRevision;

    public MainWindow()
    {
        InitializeComponent();
        var appIcon = BrandingAssetService.TryLoadWindowIcon();
        var brandIcon = BrandingAssetService.TryLoadBrandIcon();
        Icon = appIcon;
        SidebarBrandImage.Source = brandIcon;
        SidebarBrandImage.Visibility = brandIcon is null ? Visibility.Collapsed : Visibility.Visible;
        SidebarBrandFallback.Visibility = brandIcon is null ? Visibility.Visible : Visibility.Collapsed;
        SidebarBrandContainer.Visibility = Visibility.Visible;
        BrandIconImage.Source = brandIcon;
        BrandIconImage.Visibility = brandIcon is null ? Visibility.Collapsed : Visibility.Visible;
        BrandIconFallback.Visibility = brandIcon is null ? Visibility.Visible : Visibility.Collapsed;
        BrandIconContainer.Visibility = Visibility.Visible;
        FitWindowToWorkArea();
        _volumePresetToggleService = new VolumePresetToggleService(_audioService);
        _overlayService = new OverlayService(Dispatcher);
        _overlayService.ConfigurationChanged += OverlayService_ConfigurationChanged;
        _overlayService.CloseRequested += OverlayService_CloseRequested;
        _overlayService.MuteToggleRequested += OverlayService_MuteToggleRequested;
        _overlayService.VolumeChangeRequested += OverlayService_VolumeChangeRequested;
        _trayService = new TrayService();
        _trayService.OpenRequested += TrayService_OpenRequested;
        _trayService.OverlayToggleRequested += TrayService_OverlayToggleRequested;
        _trayService.ExitRequested += TrayService_ExitRequested;
        _overlayRefreshTimer = new DispatcherTimer(
            OverlayRefreshInterval,
            DispatcherPriority.Background,
            OverlayRefreshTimer_Tick,
            Dispatcher);
        _overlayRefreshTimer.Stop();
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (!_isRealExitRequested)
        {
            e.Cancel = true;
            Hide();
            _trayService.ShowRunningInBackgroundNotice();
            return;
        }

        base.OnClosing(e);
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        var warnings = new List<string>();

        try
        {
            _hotkeyService.Initialize(new WindowInteropHelper(this).Handle);
            _hotkeyService.HotkeyPressed += HotkeyService_HotkeyPressed;
            _hotkeysInitialized = true;

            if (!string.IsNullOrWhiteSpace(_hotkeyService.InitializationWarning))
            {
                warnings.Add(_hotkeyService.InitializationWarning);
            }

            var loadResult = _settingsService.Load();
            _settings = loadResult.Settings;
            ThemeManager.Apply(_settings.Theme);
            UpdateThemeSelection();
            _overlayService.Configure(CreateOverlayConfiguration(_settings));
            _overlayService.SetEnabled(_settings.OverlayEnabled);
            UpdateOverlaySettingDisplay();
            UpdateMasterVolumeSettingDisplay();
            RefreshOverlayHud();

            if (!string.IsNullOrWhiteSpace(loadResult.WarningMessage))
            {
                warnings.Add(loadResult.WarningMessage);
            }

            foreach (var createBinding in GetConfiguredBindingFactories())
            {
                try
                {
                    var binding = createBinding();

                    if (!_hotkeyService.TryRegisterOrReplace(binding, out var errorMessage))
                    {
                        warnings.Add($"{GetTargetDisplayName(binding)}: {errorMessage}");
                    }
                }
                catch (Exception exception)
                {
                    Debug.WriteLine(exception);
                    warnings.Add("저장된 단축키 하나가 올바르지 않아 건너뛰었습니다.");
                }
            }
        }
        catch (Exception exception)
        {
            Debug.WriteLine(exception);
            warnings.Add("전역 단축키 기능을 시작하지 못했습니다. 오디오 버튼은 계속 사용할 수 있습니다.");
        }

        _settingsLoaded = true;
        ShowHotkeyWarnings(warnings);
        UpdateMasterHotkeyDisplay();
        RefreshStartupStatus();
        UpdatePrivilegeStatus();
    }

    protected override void OnClosed(EventArgs e)
    {
        _isClosed = true;
        _overlayRefreshTimer.Stop();
        _overlayRefreshTimer.Tick -= OverlayRefreshTimer_Tick;
        _hotkeyService.HotkeyPressed -= HotkeyService_HotkeyPressed;
        _hotkeyService.Dispose();
        _volumePresetToggleService.Clear();
        _overlayService.ConfigurationChanged -= OverlayService_ConfigurationChanged;
        _overlayService.CloseRequested -= OverlayService_CloseRequested;
        _overlayService.MuteToggleRequested -= OverlayService_MuteToggleRequested;
        _overlayService.VolumeChangeRequested -= OverlayService_VolumeChangeRequested;
        _overlayService.Dispose();
        _trayService.OpenRequested -= TrayService_OpenRequested;
        _trayService.OverlayToggleRequested -= TrayService_OverlayToggleRequested;
        _trayService.ExitRequested -= TrayService_ExitRequested;
        _trayService.Dispose();
        base.OnClosed(e);
    }

    internal void StartServices()
    {
        if (_servicesStarted)
        {
            return;
        }

        _servicesStarted = true;
        RefreshMasterAudioState();
        RefreshApplicationSessions();
        _overlayRefreshTimer.Start();
    }

    private void MainWindow_Loaded(object sender, RoutedEventArgs e) => StartServices();

    private void FitWindowToWorkArea()
    {
        const double margin = 24;
        var workArea = SystemParameters.WorkArea;
        var safeWidth = Math.Max(640, workArea.Width - margin);
        var safeHeight = Math.Max(520, workArea.Height - margin);
        MinWidth = Math.Min(MinWidth, safeWidth);
        MinHeight = Math.Min(MinHeight, safeHeight);
        Width = Math.Min(Width, safeWidth);
        Height = Math.Min(Height, safeHeight);
    }

    private void SidebarNavigationButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string sectionName })
        {
            return;
        }

        ShowPage(string.Equals(sectionName, "Settings", StringComparison.OrdinalIgnoreCase));
    }

    private void ShowPage(bool showSettings)
    {
        DashboardPage.Visibility = showSettings ? Visibility.Collapsed : Visibility.Visible;
        SettingsPage.Visibility = showSettings ? Visibility.Visible : Visibility.Collapsed;
        DashboardNavigationButton.Background = showSettings
            ? Brushes.Transparent
            : FindResource("SidebarSelectedBrush") as Brush;
        SettingsNavigationButton.Background = showSettings
            ? FindResource("SidebarSelectedBrush") as Brush
            : Brushes.Transparent;
        PageTitleText.Text = showSettings ? "실행 설정" : "오디오 대시보드";
        PageDescriptionText.Text = showSettings
            ? "오버레이, Windows 시작, 실행 권한과 앱 정보를 관리합니다."
            : "마스터와 애플리케이션 오디오를 한곳에서 관리합니다.";
    }

    private void ThemeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_settingsLoaded || _isUpdatingThemeSelection ||
            ThemeComboBox.SelectedItem is not ComboBoxItem { Tag: string themeName } ||
            !Enum.TryParse(themeName, true, out AppTheme selectedTheme) ||
            selectedTheme == _settings.Theme)
        {
            return;
        }

        var previousSettings = CloneSettings(_settings);
        _settings.Theme = selectedTheme;
        ThemeManager.Apply(selectedTheme);

        try
        {
            _settingsService.Save(_settings);
            HotkeyErrorText.Visibility = Visibility.Collapsed;
        }
        catch (Exception exception)
        {
            Debug.WriteLine(exception);
            _settings = previousSettings;
            ThemeManager.Apply(_settings.Theme);
            UpdateThemeSelection();
            ShowHotkeyError("테마 설정을 저장하지 못해 이전 테마로 되돌렸습니다.");
        }
    }

    private void MasterMuteButton_Click(object sender, RoutedEventArgs e)
    {
        MasterMuteButton.IsEnabled = false;
        AudioErrorText.Visibility = Visibility.Collapsed;

        try
        {
            _audioService.ToggleMasterMuteState();
            RefreshMasterAudioState();
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

    private void MasterHotkeyButton_Click(object sender, RoutedEventArgs e) =>
        ConfigureHotkey(
            _settings.MasterHotkey,
            HotkeyBinding.ForMasterMute,
            "Master Audio · 음소거 단축키 설정");

    private void MasterHotkeyRemoveButton_Click(object sender, RoutedEventArgs e) =>
        RemoveHotkey(HotkeyBinding.MasterMuteBindingId);

    private void MasterVolumeHotkeyButton_Click(object sender, RoutedEventArgs e) =>
        ConfigureHotkey(
            _settings.MasterVolumeHotkey,
            HotkeyBinding.ForMasterVolume,
            "Master Audio · 볼륨 단축키 설정");

    private void MasterVolumeHotkeyRemoveButton_Click(object sender, RoutedEventArgs e) =>
        RemoveHotkey(HotkeyBinding.MasterVolumeBindingId);

    private void MasterVolumeSlider_ValueChanged(
        object sender,
        RoutedPropertyChangedEventArgs<double> e)
    {
        if (!_settingsLoaded || _isSynchronizingPresetInputs)
        {
            return;
        }

        SaveMasterVolumePreset((int)Math.Round(e.NewValue));
    }

    private void MasterVolumeInputTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (!_settingsLoaded || _isSynchronizingPresetInputs || sender is not TextBox)
        {
            return;
        }

        _ = TryCommitMasterVolumeInput();
    }

    private void MasterVolumeInputTextBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter)
        {
            return;
        }

        e.Handled = true;
        if (TryCommitMasterVolumeInput())
        {
            MasterVolumeInputTextBox.SelectAll();
        }
    }

    private bool TryCommitMasterVolumeInput()
    {
        if (!PresetVolumeInput.TryParse(MasterVolumeInputTextBox.Text, out var percent))
        {
            MasterVolumeInputErrorText.Text = PresetVolumeInput.ValidationMessage;
            MasterVolumeInputErrorText.Visibility = Visibility.Visible;
            return false;
        }

        MasterVolumeInputErrorText.Visibility = Visibility.Collapsed;
        _isSynchronizingPresetInputs = true;

        try
        {
            MasterVolumeSlider.Value = percent;
        }
        finally
        {
            _isSynchronizingPresetInputs = false;
        }

        SaveMasterVolumePreset(percent);
        return true;
    }

    private void MasterVolumeApplyButton_Click(object sender, RoutedEventArgs e)
    {
        if (!TryCommitMasterVolumeInput())
        {
            MasterVolumeInputTextBox.Focus();
            return;
        }

        MasterVolumeApplyButton.IsEnabled = false;
        AudioErrorText.Visibility = Visibility.Collapsed;

        try
        {
            _volumePresetToggleService.ToggleMaster(_settings.MasterVolumePercent);
            RefreshMasterAudioState();
        }
        catch (Exception exception)
        {
            ShowAudioError(exception);
        }
        finally
        {
            MasterVolumeApplyButton.IsEnabled = true;
        }
    }

    private void OverlayToggleButton_Click(object sender, RoutedEventArgs e) =>
        SetOverlayEnabled(!_settings.OverlayEnabled);

    private void OverlayQuickButton_Click(object sender, RoutedEventArgs e) =>
        SetOverlayEnabled(!_settings.OverlayEnabled);

    private void OverlayLockToggleButton_Click(object sender, RoutedEventArgs e)
    {
        var nextSettings = CloneSettings(_settings);
        nextSettings.OverlayLocked = !_settings.OverlayLocked;
        SaveOverlaySettings(nextSettings);
    }

    private void OverlayOpacitySlider_ValueChanged(
        object sender,
        RoutedPropertyChangedEventArgs<double> e)
    {
        if (!_settingsLoaded || _isApplyingOverlayConfiguration)
        {
            return;
        }

        var nextSettings = CloneSettings(_settings);
        nextSettings.OverlayOpacity = Math.Clamp(e.NewValue / 100, 0.2, 1.0);
        SaveOverlaySettings(nextSettings);
    }

    private void SaveOverlaySettings(AppSettings nextSettings)
    {
        var previousSettings = CloneSettings(_settings);
        _settings = nextSettings;

        try
        {
            _settingsService.Save(_settings);
            _isApplyingOverlayConfiguration = true;

            try
            {
                _overlayService.Configure(CreateOverlayConfiguration(_settings));
            }
            finally
            {
                _isApplyingOverlayConfiguration = false;
            }

            UpdateOverlaySettingDisplay();
            HotkeyErrorText.Visibility = Visibility.Collapsed;
        }
        catch (Exception exception)
        {
            Debug.WriteLine(exception);
            _settings = previousSettings;
            _overlayService.Configure(CreateOverlayConfiguration(_settings));
            UpdateOverlaySettingDisplay();
            ShowHotkeyError("오버레이 설정을 저장하지 못해 이전 값으로 되돌렸습니다.");
        }
    }

    private void SetOverlayEnabled(bool isEnabled)
    {
        var previousSettings = CloneSettings(_settings);
        _settings.OverlayEnabled = isEnabled;

        try
        {
            _settingsService.Save(_settings);
            _overlayService.SetEnabled(_settings.OverlayEnabled);
            UpdateOverlaySettingDisplay();
            RefreshOverlayHud();

            if (_settings.OverlayEnabled)
            {
                _ = RefreshOverlayAudioStateAsync();
            }

            HotkeyErrorText.Visibility = Visibility.Collapsed;
        }
        catch (Exception exception)
        {
            Debug.WriteLine(exception);
            _settings = previousSettings;
            _overlayService.SetEnabled(_settings.OverlayEnabled);
            UpdateOverlaySettingDisplay();
            ShowHotkeyError("오버레이 설정을 저장하지 못했습니다.");
        }
    }

    private async void StartupToggleButton_Click(object sender, RoutedEventArgs e)
    {
        var enableStartup = !_startupStatus.TaskExists;
        StartupToggleButton.IsEnabled = false;
        RestartAsAdministratorButton.IsEnabled = false;
        StartupErrorText.Visibility = Visibility.Collapsed;

        var result = await _startupService.SetEnabledAsync(enableStartup);
        _startupStatus = result.Status;
        UpdateStartupStatusDisplay();
        UpdatePrivilegeStatus();

        if (result.Outcome != StartupChangeOutcome.Succeeded)
        {
            StartupErrorText.Text = result.Message ??
                "Windows 자동 실행 설정을 변경하지 못했습니다.";
            StartupErrorText.Visibility = Visibility.Visible;
        }
    }

    private void RestartAsAdministratorButton_Click(object sender, RoutedEventArgs e)
        => RestartAsAdministrator();

    private void AdminQuickButton_Click(object sender, RoutedEventArgs e)
    {
        if (_privilegeService.IsElevated)
        {
            var answer = MessageBox.Show(
                "일반 권한으로 바꾸려면 MutePilot을 다시 시작해야 합니다. 계속할까요?",
                "일반 권한으로 재시작",
                MessageBoxButton.YesNo,
                MessageBoxImage.Information);

            if (answer == MessageBoxResult.Yes)
            {
                RestartAsStandardUser();
            }

            return;
        }

        var elevationAnswer = MessageBox.Show(
            "관리자 권한으로 바꾸려면 MutePilot을 다시 시작해야 합니다. 계속할까요?",
            "관리자 권한으로 재시작",
            MessageBoxButton.YesNo,
            MessageBoxImage.Information);

        if (elevationAnswer == MessageBoxResult.Yes)
        {
            RestartAsAdministrator();
        }
    }

    private void SupportButton_Click(object sender, RoutedEventArgs e)
    {
        var supportWindow = new SupportWindow { Owner = this };
        supportWindow.ShowDialog();
    }

    private void RestartAsAdministrator()
    {
        StartupErrorText.Visibility = Visibility.Collapsed;
        var result = _privilegeService.RestartAsAdministrator(!IsVisible);

        if (result.Outcome == ElevationRestartOutcome.Started)
        {
            RequestRealExit();
            return;
        }

        if (result.Outcome == ElevationRestartOutcome.AlreadyElevated)
        {
            UpdatePrivilegeStatus();
            return;
        }

        StartupErrorText.Text = result.Message ??
            "MutePilot을 관리자 권한으로 재시작하지 못했습니다.";
        StartupErrorText.Visibility = Visibility.Visible;
    }

    private void RestartAsStandardUser()
    {
        StartupErrorText.Visibility = Visibility.Collapsed;
        var result = _privilegeService.RestartAsStandardUser(!IsVisible);

        if (result.Outcome == StandardRestartOutcome.Started)
        {
            RequestRealExit();
            return;
        }

        if (result.Outcome == StandardRestartOutcome.AlreadyStandard)
        {
            UpdatePrivilegeStatus();
            return;
        }

        StartupErrorText.Text = result.Message ??
            "MutePilot을 일반 권한으로 재시작하지 못했습니다.";
        StartupErrorText.Visibility = Visibility.Visible;
        ShowPage(true);
    }

    private void OverlayService_CloseRequested(object? sender, EventArgs e) =>
        RunOnUiThread(() => SetOverlayEnabled(false));

    private void OverlayService_MuteToggleRequested(
        object? sender,
        OverlayMuteToggleRequestedEventArgs e) =>
        RunOnUiThread(() => ToggleMuteFromOverlay(e.TargetId));

    private void OverlayService_VolumeChangeRequested(
        object? sender,
        OverlayVolumeChangeRequestedEventArgs e) =>
        RunOnUiThread(() => SetLiveVolumeFromOverlay(e));

    private void SetLiveVolumeFromOverlay(OverlayVolumeChangeRequestedEventArgs request)
    {
        try
        {
            if (string.Equals(
                    request.TargetId,
                    HotkeyBinding.MasterTargetId,
                    StringComparison.OrdinalIgnoreCase))
            {
                _masterVolumePercent = _audioService.SetMasterVolumePercent(
                    request.VolumePercent);
            }
            else
            {
                var session = _activeApplicationSessions.FirstOrDefault(candidate => string.Equals(
                    HotkeyBinding.GetApplicationTargetId(candidate.ApplicationKey),
                    request.TargetId,
                    StringComparison.OrdinalIgnoreCase));

                if (session is null)
                {
                    if (request.IsFinal)
                    {
                        _ = RefreshOverlayAudioStateAsync();
                    }

                    return;
                }

                _audioService.SetApplicationVolumePercent(
                    session.ApplicationKey,
                    request.VolumePercent);
            }

            _audioStateRevision++;

            if (request.IsFinal)
            {
                _ = RefreshOverlayAudioStateAsync();
            }
        }
        catch (Exception exception)
        {
            Debug.WriteLine(exception);
            ShowHotkeyError("오버레이에서 현재 볼륨을 변경하지 못했습니다.");
            _ = RefreshOverlayAudioStateAsync();
        }
    }

    private void ToggleMuteFromOverlay(string targetId)
    {
        try
        {
            if (string.Equals(targetId, HotkeyBinding.MasterTargetId, StringComparison.OrdinalIgnoreCase))
            {
                _audioService.ToggleMasterMuteState();
                RefreshMasterAudioState();
                return;
            }

            var session = _activeApplicationSessions.FirstOrDefault(candidate => string.Equals(
                HotkeyBinding.GetApplicationTargetId(candidate.ApplicationKey),
                targetId,
                StringComparison.OrdinalIgnoreCase));

            if (session is null)
            {
                RefreshApplicationSessions();
                return;
            }

            _audioService.ToggleApplicationMute(session.ApplicationKey);
            RefreshApplicationSessions();
        }
        catch (Exception exception)
        {
            Debug.WriteLine(exception);
            ShowHotkeyError("오버레이에서 음소거 상태를 변경하지 못했습니다.");
            RefreshApplicationSessions();
        }
    }

    private void TrayService_OpenRequested(object? sender, EventArgs e) =>
        RunOnUiThread(RestoreMainWindow);

    private void TrayService_OverlayToggleRequested(object? sender, EventArgs e) =>
        RunOnUiThread(() => SetOverlayEnabled(!_settings.OverlayEnabled));

    private void TrayService_ExitRequested(object? sender, EventArgs e) =>
        RunOnUiThread(RequestRealExit);

    private void RestoreMainWindow()
    {
        if (_isClosed || _isRealExitRequested)
        {
            return;
        }

        if (!IsVisible)
        {
            Show();
        }

        if (WindowState == WindowState.Minimized)
        {
            WindowState = WindowState.Normal;
        }

        Activate();
    }

    private void RequestRealExit()
    {
        if (_isClosed || _isRealExitRequested)
        {
            return;
        }

        _isRealExitRequested = true;
        System.Windows.Application.Current.Shutdown();
    }

    private void RunOnUiThread(Action action)
    {
        if (_isClosed || Dispatcher.HasShutdownStarted || Dispatcher.HasShutdownFinished)
        {
            return;
        }

        if (Dispatcher.CheckAccess())
        {
            action();
        }
        else
        {
            Dispatcher.BeginInvoke(action);
        }
    }

    private void OverlayPositionResetButton_Click(object sender, RoutedEventArgs e)
    {
        var previousSettings = CloneSettings(_settings);
        _settings.OverlayLeft = null;
        _settings.OverlayTop = null;

        try
        {
            _settingsService.Save(_settings);
            _overlayService.Configure(CreateOverlayConfiguration(_settings));
            HotkeyErrorText.Visibility = Visibility.Collapsed;
        }
        catch (Exception exception)
        {
            Debug.WriteLine(exception);
            _settings = previousSettings;
            _overlayService.Configure(CreateOverlayConfiguration(_settings));
            ShowHotkeyError("오버레이 위치를 초기화하지 못했습니다.");
        }
    }

    private void ApplicationRefreshButton_Click(object sender, RoutedEventArgs e) =>
        RefreshApplicationSessions();

    private void ApplicationMuteButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string applicationKey } button)
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

    private void ApplicationHotkeyButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string processName })
        {
            ConfigureHotkey(
                FindApplicationSetting(processName)?.Hotkey,
                gesture => HotkeyBinding.ForApplicationMute(processName, gesture),
                $"{processName} · 음소거 단축키 설정");
        }
    }

    private void ApplicationHotkeyRemoveButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string processName })
        {
            RemoveHotkey(HotkeyBinding.GetApplicationMuteBindingId(processName));
        }
    }

    private void ApplicationVolumeHotkeyButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string processName })
        {
            ConfigureHotkey(
                FindApplicationSetting(processName)?.VolumeHotkey,
                gesture => HotkeyBinding.ForApplicationVolume(processName, gesture),
                $"{processName} · 볼륨 단축키 설정");
        }
    }

    private void ApplicationVolumeHotkeyRemoveButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string processName })
        {
            RemoveHotkey(HotkeyBinding.GetApplicationVolumeBindingId(processName));
        }
    }

    private void ApplicationVolumeSlider_ValueChanged(
        object sender,
        RoutedPropertyChangedEventArgs<double> e)
    {
        if (!_settingsLoaded || _isRefreshingApplicationItems ||
            _isSynchronizingPresetInputs ||
            sender is not Slider { Tag: string processName } slider)
        {
            return;
        }

        if (slider.Parent is Grid grid)
        {
            var inputTextBox = grid.Children.OfType<TextBox>().FirstOrDefault();
            var toggleButton = grid.Children.OfType<Button>().FirstOrDefault();
            var normalizedPercent = Math.Clamp((int)Math.Round(e.NewValue), 0, 100);

            if (inputTextBox is not null && inputTextBox.Text != normalizedPercent.ToString())
            {
                _isSynchronizingPresetInputs = true;

                try
                {
                    inputTextBox.Text = normalizedPercent.ToString();
                }
                finally
                {
                    _isSynchronizingPresetInputs = false;
                }
            }

            if (toggleButton is not null)
            {
                toggleButton.Content = _volumePresetToggleService
                    .IsApplicationPresetActive(processName)
                        ? "기본 볼륨으로 복원"
                        : $"{normalizedPercent}%로 전환";
            }
        }

        SaveApplicationVolumePreset(processName, (int)Math.Round(e.NewValue));
    }

    private void ApplicationVolumeInputTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (!_settingsLoaded || _isRefreshingApplicationItems || _isSynchronizingPresetInputs ||
            sender is not TextBox { Tag: string processName } textBox)
        {
            return;
        }

        _ = TryCommitApplicationVolumeInput(textBox, processName);
    }

    private void ApplicationVolumeInputTextBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter || sender is not TextBox { Tag: string processName } textBox)
        {
            return;
        }

        e.Handled = true;
        if (TryCommitApplicationVolumeInput(textBox, processName))
        {
            textBox.SelectAll();
        }
    }

    private bool TryCommitApplicationVolumeInput(TextBox textBox, string processName)
    {
        if (!PresetVolumeInput.TryParse(textBox.Text, out var percent))
        {
            ShowApplicationInputError(processName);
            return false;
        }

        ApplicationErrorText.Visibility = Visibility.Collapsed;
        if (textBox.Parent is Grid grid &&
            grid.Children.OfType<Slider>().FirstOrDefault() is Slider slider)
        {
            _isSynchronizingPresetInputs = true;

            try
            {
                slider.Value = percent;
            }
            finally
            {
                _isSynchronizingPresetInputs = false;
            }

            if (grid.Children.OfType<Button>().FirstOrDefault() is Button toggleButton)
            {
                toggleButton.Content = _volumePresetToggleService
                    .IsApplicationPresetActive(processName)
                        ? "기본 볼륨으로 복원"
                        : $"{percent}%로 전환";
            }
        }

        _isSynchronizingPresetInputs = true;

        try
        {
            textBox.Text = percent.ToString();
        }
        finally
        {
            _isSynchronizingPresetInputs = false;
        }

        SaveApplicationVolumePreset(processName, percent);
        return true;
    }

    private void ShowApplicationInputError(string processName)
    {
        ApplicationErrorText.Text = $"{processName}: {PresetVolumeInput.ValidationMessage}";
        ApplicationErrorText.Visibility = Visibility.Visible;
    }

    private void ApplicationVolumeApplyButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string processName } button)
        {
            return;
        }

        if (button.Parent is Grid grid &&
            grid.Children.OfType<TextBox>().FirstOrDefault() is TextBox textBox &&
            !TryCommitApplicationVolumeInput(textBox, processName))
        {
            textBox.Focus();
            return;
        }

        var setting = FindApplicationSetting(processName) ??
            new ApplicationHotkeySetting(processName);

        button.IsEnabled = false;
        ApplicationErrorText.Visibility = Visibility.Collapsed;

        try
        {
            _volumePresetToggleService.ToggleApplication(
                processName,
                setting.VolumePercent);
            RefreshApplicationSessions();
        }
        catch (Exception exception)
        {
            RefreshApplicationSessions();
            ShowApplicationError(
                $"{processName}의 볼륨 프리셋을 적용하지 못했습니다. 앱이 실행 중인지 확인해 주세요.",
                exception);
        }
        finally
        {
            button.IsEnabled = true;
        }
    }

    private void ConfigureHotkey(
        HotkeyGesture? currentGesture,
        Func<HotkeyGesture, HotkeyBinding> createBinding,
        string contextText)
    {
        if (!_hotkeysInitialized)
        {
            ShowHotkeyError("전역 단축키 서비스가 준비되지 않았습니다.");
            return;
        }

        var dialog = new HotkeyCaptureWindow(currentGesture, contextText) { Owner = this };

        bool? dialogResult;

        try
        {
            _isCapturingHotkey = true;
            dialogResult = dialog.ShowDialog();
        }
        finally
        {
            _isCapturingHotkey = false;
        }

        if (dialogResult == true && dialog.SelectedGesture is not null)
        {
            ApplyHotkeyChange(createBinding(dialog.SelectedGesture));
        }
    }

    private void ApplyHotkeyChange(HotkeyBinding newBinding)
    {
        var previousBinding = FindConfiguredBinding(newBinding.BindingId);

        if (!_hotkeyService.TryRegisterOrReplace(newBinding, out var errorMessage))
        {
            ShowHotkeyError(errorMessage);
            return;
        }

        var previousSettings = CloneSettings(_settings);
        SetConfiguredBinding(newBinding);

        try
        {
            _settingsService.Save(_settings);
            HotkeyErrorText.Visibility = Visibility.Collapsed;
        }
        catch (Exception exception)
        {
            Debug.WriteLine(exception);
            _settings = previousSettings;
            RollBackRegistration(newBinding.BindingId, previousBinding);
            ShowHotkeyError("설정 파일을 저장하지 못해 단축키 변경을 취소했습니다.");
        }

        UpdateMasterHotkeyDisplay();
        RefreshApplicationSessions();
    }

    private void RemoveHotkey(string bindingId)
    {
        var previousBinding = FindConfiguredBinding(bindingId);

        if (previousBinding is null)
        {
            return;
        }

        if (!_hotkeyService.TryUnregister(bindingId, out var errorMessage))
        {
            ShowHotkeyError(errorMessage);
            return;
        }

        var previousSettings = CloneSettings(_settings);
        RemoveConfiguredBinding(bindingId);

        try
        {
            _settingsService.Save(_settings);
            HotkeyErrorText.Visibility = Visibility.Collapsed;
        }
        catch (Exception exception)
        {
            Debug.WriteLine(exception);
            _settings = previousSettings;
            RollBackRegistration(bindingId, previousBinding);
            ShowHotkeyError("설정 파일을 저장하지 못해 단축키 삭제를 취소했습니다.");
        }

        UpdateMasterHotkeyDisplay();
        RefreshApplicationSessions();
    }

    private void RollBackRegistration(string bindingId, HotkeyBinding? previousBinding)
    {
        bool restored;
        string rollbackError;

        if (previousBinding is null)
        {
            restored = _hotkeyService.TryUnregister(bindingId, out rollbackError);
        }
        else
        {
            restored = _hotkeyService.TryRegisterOrReplace(previousBinding, out rollbackError);
        }

        if (!restored)
        {
            ShowHotkeyError($"설정 복원 중 문제가 발생했습니다: {rollbackError}");
        }
    }

    private void HotkeyService_HotkeyPressed(object? sender, HotkeyPressedEventArgs e)
    {
        if (!Dispatcher.CheckAccess())
        {
            if (!Dispatcher.HasShutdownStarted)
            {
                Dispatcher.BeginInvoke(
                    () => HotkeyService_HotkeyPressed(sender, e));
            }

            return;
        }

        if (_isCapturingHotkey)
        {
            return;
        }

        try
        {
            if (e.Binding.TargetType == HotkeyTargetType.MasterAudio &&
                e.Binding.ActionType == HotkeyActionType.ToggleMute)
            {
                _audioService.ToggleMasterMuteState();
                RefreshMasterAudioState();
            }
            else if (e.Binding.TargetType == HotkeyTargetType.MasterAudio)
            {
                _volumePresetToggleService.ToggleMaster(_settings.MasterVolumePercent);
                RefreshMasterAudioState();
            }
            else if (!string.IsNullOrWhiteSpace(e.Binding.ProcessName) &&
                     e.Binding.ActionType == HotkeyActionType.ToggleMute)
            {
                _audioService.ToggleApplicationMute(e.Binding.ProcessName);
                RefreshApplicationSessions();
            }
            else if (!string.IsNullOrWhiteSpace(e.Binding.ProcessName))
            {
                var setting = FindApplicationSetting(e.Binding.ProcessName) ??
                    throw new InvalidOperationException("저장된 앱 볼륨 프리셋을 찾을 수 없습니다.");
                _volumePresetToggleService.ToggleApplication(
                    e.Binding.ProcessName,
                    setting.VolumePercent);
                RefreshApplicationSessions();
            }
        }
        catch (Exception exception)
        {
            Debug.WriteLine(exception);
            var actionName = e.Binding.ActionType == HotkeyActionType.ToggleMute
                ? "음소거 상태"
                : "볼륨 프리셋";
            ShowHotkeyError(e.Binding.TargetType == HotkeyTargetType.MasterAudio
                ? $"단축키로 Master Audio {actionName}를 적용하지 못했습니다."
                : $"{e.Binding.ProcessName}의 {actionName}를 적용하지 못했습니다.");
            RefreshApplicationSessions();
        }
    }

    private void RefreshMasterAudioState()
    {
        try
        {
            var snapshot = _audioService.CaptureMasterVolumeSnapshot();
            _volumePresetToggleService.InvalidateStaleMasterBaseline(snapshot.DeviceId);
            UpdateMasterAudioState(snapshot.IsMuted, snapshot.VolumePercent);
            UpdateMasterVolumeSettingDisplay();
        }
        catch (Exception exception)
        {
            ShowAudioError(exception);
        }
        finally
        {
            MasterMuteButton.IsEnabled = true;
            MasterVolumeApplyButton.IsEnabled = true;
        }
    }

    private void RefreshApplicationSessions()
    {
        ApplicationRefreshButton.IsEnabled = false;
        ApplicationErrorText.Visibility = Visibility.Collapsed;
        _isRefreshingApplicationItems = true;

        try
        {
            var activeSessions = _audioService.GetActiveApplicationSessions();
            _activeApplicationSessions = activeSessions;
            _volumePresetToggleService.InvalidateStaleApplicationBaselines(activeSessions);
            _audioStateRevision++;
            var activeNames = new HashSet<string>(
                activeSessions.Select(session => session.ApplicationKey),
                StringComparer.OrdinalIgnoreCase);
            var items = activeSessions.Select(CreateActiveApplicationItem)
                .Concat(_settings.ApplicationBindings
                    .Where(setting => !activeNames.Contains(setting.ProcessName))
                    .Select(CreateInactiveApplicationItem))
                .OrderBy(item => item.ApplicationName, StringComparer.OrdinalIgnoreCase)
                .ToArray();

            ApplicationsItemsControl.ItemsSource = items;
            ApplicationEmptyText.Visibility = items.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
        }
        catch (Exception exception)
        {
            _activeApplicationSessions = [];
            _audioStateRevision++;
            var savedItems = _settings.ApplicationBindings.Select(CreateInactiveApplicationItem)
                .OrderBy(item => item.ApplicationName, StringComparer.OrdinalIgnoreCase)
                .ToArray();
            ApplicationsItemsControl.ItemsSource = savedItems;
            ApplicationEmptyText.Visibility = savedItems.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
            ShowApplicationError(
                "활성 애플리케이션 오디오 세션을 불러올 수 없습니다. 저장된 단축키는 유지됩니다.",
                exception);
        }
        finally
        {
            _isRefreshingApplicationItems = false;
            ApplicationRefreshButton.IsEnabled = true;
            RefreshOverlayHud();
        }
    }

    private ApplicationSessionItem CreateActiveApplicationItem(ApplicationAudioSession session)
    {
        var setting = FindApplicationSetting(session.ApplicationKey);
        return new ApplicationSessionItem(
            session.ApplicationKey,
            session.ApplicationName,
            _applicationIconService.GetIcon(session.ApplicationKey, session.ProcessIds),
            $"PID: {string.Join(", ", session.ProcessIds)} · 세션 {session.SessionCount}개",
            session.HasMixedMuteState
                ? "현재 상태: 일부 세션 음소거"
                : session.IsMuted ? "현재 상태: 음소거" : "현재 상태: 음소거 해제",
            session.HasMixedVolume
                ? "현재 볼륨: 혼합"
                : $"현재 볼륨: {session.VolumePercent}%",
            session.IsMuted ? "음소거 해제" : "음소거",
            setting?.Hotkey?.DisplayText ?? "설정 안 됨",
            setting?.Hotkey is null ? "설정" : "변경",
            setting?.Hotkey is null ? Visibility.Collapsed : Visibility.Visible,
            setting?.VolumeHotkey?.DisplayText ?? "설정 안 됨",
            setting?.VolumeHotkey is null ? "설정" : "변경",
            setting?.VolumeHotkey is null ? Visibility.Collapsed : Visibility.Visible,
            setting?.VolumePercent ?? AppSettings.DefaultVolumePercent,
            _volumePresetToggleService.IsApplicationPresetActive(session.ApplicationKey)
                ? "기본 볼륨으로 복원"
                : $"{setting?.VolumePercent ?? AppSettings.DefaultVolumePercent}%로 전환",
            true);
    }

    private ApplicationSessionItem CreateInactiveApplicationItem(ApplicationHotkeySetting setting) =>
        new(
            setting.ProcessName,
            setting.ProcessName,
            _applicationIconService.GetIcon(setting.ProcessName, []),
            "저장된 앱 바인딩",
            "현재 상태: 실행 중이 아님",
            "현재 볼륨: 실행 중이 아님",
            "음소거",
            setting.Hotkey?.DisplayText ?? "설정 안 됨",
            setting.Hotkey is null ? "설정" : "변경",
            setting.Hotkey is null ? Visibility.Collapsed : Visibility.Visible,
            setting.VolumeHotkey?.DisplayText ?? "설정 안 됨",
            setting.VolumeHotkey is null ? "설정" : "변경",
            setting.VolumeHotkey is null ? Visibility.Collapsed : Visibility.Visible,
            setting.VolumePercent,
            $"{setting.VolumePercent}%로 전환",
            false);

    private IEnumerable<HotkeyBinding> GetConfiguredBindings()
    {
        if (_settings.MasterHotkey is not null)
        {
            yield return HotkeyBinding.ForMasterMute(_settings.MasterHotkey);
        }

        if (_settings.MasterVolumeHotkey is not null)
        {
            yield return HotkeyBinding.ForMasterVolume(_settings.MasterVolumeHotkey);
        }

        foreach (var setting in _settings.ApplicationBindings)
        {
            if (!string.IsNullOrWhiteSpace(setting.ProcessName) && setting.Hotkey is not null)
            {
                yield return HotkeyBinding.ForApplicationMute(setting.ProcessName, setting.Hotkey);
            }

            if (!string.IsNullOrWhiteSpace(setting.ProcessName) && setting.VolumeHotkey is not null)
            {
                yield return HotkeyBinding.ForApplicationVolume(
                    setting.ProcessName,
                    setting.VolumeHotkey);
            }
        }
    }

    private IEnumerable<Func<HotkeyBinding>> GetConfiguredBindingFactories()
    {
        if (_settings.MasterHotkey is not null)
        {
            yield return () => HotkeyBinding.ForMasterMute(_settings.MasterHotkey);
        }

        if (_settings.MasterVolumeHotkey is not null)
        {
            yield return () => HotkeyBinding.ForMasterVolume(_settings.MasterVolumeHotkey);
        }

        foreach (var setting in _settings.ApplicationBindings)
        {
            var savedSetting = setting;
            if (savedSetting.Hotkey is not null)
            {
                yield return () => HotkeyBinding.ForApplicationMute(
                    savedSetting.ProcessName,
                    savedSetting.Hotkey);
            }

            if (savedSetting.VolumeHotkey is not null)
            {
                yield return () => HotkeyBinding.ForApplicationVolume(
                    savedSetting.ProcessName,
                    savedSetting.VolumeHotkey);
            }
        }
    }

    private HotkeyBinding? FindConfiguredBinding(string bindingId) =>
        GetConfiguredBindings().FirstOrDefault(binding =>
            string.Equals(binding.BindingId, bindingId, StringComparison.OrdinalIgnoreCase));

    private ApplicationHotkeySetting? FindApplicationSetting(string processName) =>
        _settings.ApplicationBindings.FirstOrDefault(setting =>
            string.Equals(setting.ProcessName, processName, StringComparison.OrdinalIgnoreCase));

    private void SetConfiguredBinding(HotkeyBinding binding)
    {
        if (binding.TargetType == HotkeyTargetType.MasterAudio &&
            binding.ActionType == HotkeyActionType.ToggleMute)
        {
            _settings.MasterHotkey = binding.Gesture;
            return;
        }

        if (binding.TargetType == HotkeyTargetType.MasterAudio)
        {
            _settings.MasterVolumeHotkey = binding.Gesture;
            return;
        }

        var processName = binding.ProcessName!;
        var currentSetting = FindApplicationSetting(processName) ??
            new ApplicationHotkeySetting(processName);
        ReplaceApplicationSetting(binding.ActionType == HotkeyActionType.ToggleMute
            ? currentSetting with { Hotkey = binding.Gesture }
            : currentSetting with { VolumeHotkey = binding.Gesture });
    }

    private void RemoveConfiguredBinding(string bindingId)
    {
        if (string.Equals(
                bindingId,
                HotkeyBinding.MasterMuteBindingId,
                StringComparison.OrdinalIgnoreCase))
        {
            _settings.MasterHotkey = null;
            return;
        }

        if (string.Equals(
                bindingId,
                HotkeyBinding.MasterVolumeBindingId,
                StringComparison.OrdinalIgnoreCase))
        {
            _settings.MasterVolumeHotkey = null;
            return;
        }

        var setting = _settings.ApplicationBindings.FirstOrDefault(item =>
            string.Equals(
                HotkeyBinding.GetApplicationMuteBindingId(item.ProcessName),
                bindingId,
                StringComparison.OrdinalIgnoreCase) ||
            string.Equals(
                HotkeyBinding.GetApplicationVolumeBindingId(item.ProcessName),
                bindingId,
                StringComparison.OrdinalIgnoreCase));

        if (setting is null)
        {
            return;
        }

        ReplaceApplicationSetting(string.Equals(
            HotkeyBinding.GetApplicationMuteBindingId(setting.ProcessName),
            bindingId,
            StringComparison.OrdinalIgnoreCase)
                ? setting with { Hotkey = null }
                : setting with { VolumeHotkey = null });
    }

    private void ReplaceApplicationSetting(ApplicationHotkeySetting setting)
    {
        _settings.ApplicationBindings.RemoveAll(existing => string.Equals(
            existing.ProcessName,
            setting.ProcessName,
            StringComparison.OrdinalIgnoreCase));
        _settings.ApplicationBindings.Add(setting);
    }

    private static AppSettings CloneSettings(AppSettings settings) => new()
    {
        Theme = settings.Theme,
        OverlayEnabled = settings.OverlayEnabled,
        OverlayLocked = settings.OverlayLocked,
        OverlayOpacity = settings.OverlayOpacity,
        OverlayLeft = settings.OverlayLeft,
        OverlayTop = settings.OverlayTop,
        MasterHotkey = settings.MasterHotkey,
        MasterVolumeHotkey = settings.MasterVolumeHotkey,
        MasterVolumePercent = settings.MasterVolumePercent,
        ApplicationBindings = settings.ApplicationBindings.ToList()
    };

    private void SaveMasterVolumePreset(int percent)
    {
        var normalizedPercent = Math.Clamp(percent, 0, 100);
        _isSynchronizingPresetInputs = true;

        try
        {
            MasterVolumeInputTextBox.Text = normalizedPercent.ToString();
        }
        finally
        {
            _isSynchronizingPresetInputs = false;
        }

        MasterVolumeInputErrorText.Visibility = Visibility.Collapsed;
        MasterVolumePresetText.Text = $"설정값: {normalizedPercent}%";
        MasterVolumeApplyButton.Content = _volumePresetToggleService.IsMasterPresetActive
            ? "기본 볼륨으로 복원"
            : $"{normalizedPercent}%로 전환";

        if (_settings.MasterVolumePercent == normalizedPercent)
        {
            return;
        }

        var previousSettings = CloneSettings(_settings);
        _settings.MasterVolumePercent = normalizedPercent;

        try
        {
            _settingsService.Save(_settings);
            HotkeyErrorText.Visibility = Visibility.Collapsed;
        }
        catch (Exception exception)
        {
            Debug.WriteLine(exception);
            _settings = previousSettings;
            UpdateMasterVolumeSettingDisplay();
            ShowHotkeyError("Master Audio 볼륨 프리셋을 저장하지 못했습니다.");
        }
    }

    private void SaveApplicationVolumePreset(string processName, int percent)
    {
        var normalizedPercent = Math.Clamp(percent, 0, 100);
        var previousSettings = CloneSettings(_settings);
        var setting = FindApplicationSetting(processName) ??
            new ApplicationHotkeySetting(processName);

        if (setting.VolumePercent == normalizedPercent &&
            FindApplicationSetting(processName) is not null)
        {
            return;
        }

        ReplaceApplicationSetting(setting with { VolumePercent = normalizedPercent });

        try
        {
            _settingsService.Save(_settings);
            HotkeyErrorText.Visibility = Visibility.Collapsed;
        }
        catch (Exception exception)
        {
            Debug.WriteLine(exception);
            _settings = previousSettings;
            ShowHotkeyError($"{processName}의 볼륨 프리셋을 저장하지 못했습니다.");
            RefreshApplicationSessions();
            return;
        }

        RefreshOverlayHud();
    }

    private static OverlayConfiguration CreateOverlayConfiguration(AppSettings settings) => new(
        settings.OverlayLocked,
        settings.OverlayOpacity,
        settings.OverlayLeft,
        settings.OverlayTop);

    private void OverlayService_ConfigurationChanged(
        object? sender,
        OverlayConfigurationChangedEventArgs e)
    {
        if (_isApplyingOverlayConfiguration)
        {
            return;
        }

        var previousSettings = CloneSettings(_settings);
        _settings.OverlayLocked = e.Configuration.IsLocked;
        _settings.OverlayOpacity = e.Configuration.Opacity;
        _settings.OverlayLeft = e.Configuration.Left;
        _settings.OverlayTop = e.Configuration.Top;

        try
        {
            _settingsService.Save(_settings);
            UpdateOverlaySettingDisplay();
            HotkeyErrorText.Visibility = Visibility.Collapsed;
        }
        catch (Exception exception)
        {
            Debug.WriteLine(exception);
            _settings = previousSettings;
            _isApplyingOverlayConfiguration = true;

            try
            {
                _overlayService.Configure(CreateOverlayConfiguration(_settings));
            }
            finally
            {
                _isApplyingOverlayConfiguration = false;
            }

            ShowHotkeyError("오버레이 설정을 저장하지 못해 이전 값으로 되돌렸습니다.");
        }
    }

    private void UpdateOverlaySettingDisplay()
    {
        OverlayToggleButton.Content = _settings.OverlayEnabled ? "ON" : "OFF";
        OverlayQuickButton.Content = _settings.OverlayEnabled ? "오버레이 ON" : "오버레이 OFF";
        OverlayLockToggleButton.Content = _settings.OverlayLocked ? "ON" : "OFF";
        _isApplyingOverlayConfiguration = true;

        try
        {
            OverlayOpacitySlider.Value = _settings.OverlayOpacity * 100;
        }
        finally
        {
            _isApplyingOverlayConfiguration = false;
        }

        OverlayOpacityText.Text = $"{Math.Round(_settings.OverlayOpacity * 100):0}%";
        OverlayToggleButton.ToolTip = _settings.OverlayEnabled
            ? "음소거 상태 오버레이를 끕니다."
            : "음소거 상태 오버레이를 켭니다.";
        _trayService.SetOverlayEnabled(_settings.OverlayEnabled);
    }

    private void RefreshStartupStatus()
    {
        _startupStatus = _startupService.GetStatus();
        UpdateStartupStatusDisplay();
    }

    private void UpdateStartupStatusDisplay()
    {
        switch (_startupStatus.State)
        {
            case StartupTaskState.Disabled:
                StartupToggleButton.Content = "OFF";
                StartupToggleButton.IsEnabled = true;
                StartupDetailText.Text =
                    "ON으로 바꾸면 UAC 확인 후 현재 실행 파일을 highest privileges 로그인 작업으로 등록합니다.";
                break;

            case StartupTaskState.Enabled:
                StartupToggleButton.Content = "ON";
                StartupToggleButton.IsEnabled = true;
                StartupDetailText.Text =
                    "로그인 시 --background로 실행합니다. 개발 빌드 위치가 바뀌면 OFF/ON으로 다시 등록해 주세요.";
                break;

            case StartupTaskState.ConfigurationMismatch:
                StartupToggleButton.Content = "ON";
                StartupToggleButton.IsEnabled = true;
                StartupDetailText.Text =
                    "등록 경로나 실행 조건이 현재 앱과 다릅니다. OFF 후 ON으로 다시 등록해 주세요.";
                break;

            default:
                StartupToggleButton.Content = "확인 실패";
                StartupToggleButton.IsEnabled = false;
                StartupDetailText.Text = _startupStatus.DetailMessage ??
                    "Windows 자동 시작 작업 상태를 확인하지 못했습니다.";
                break;
        }
    }

    private void UpdatePrivilegeStatus()
    {
        try
        {
            var isElevated = _privilegeService.IsElevated;
            AdminQuickButton.IsEnabled = true;
            PrivilegeStatusText.Text = isElevated
                ? "현재 실행 권한: 관리자 권한"
                : "현재 실행 권한: 일반 권한";
            AdminQuickButton.Content = isElevated
                ? "관리자모드 실행 ON"
                : "관리자모드 실행 OFF";
            AdminQuickButton.ToolTip = isElevated
                ? "일반 권한으로 재시작"
                : "관리자 권한으로 재시작";
            RestartAsAdministratorButton.Content = "MutePilot을 관리자 권한으로 재시작";
            RestartAsAdministratorButton.IsEnabled = !isElevated;
        }
        catch (Exception exception)
        {
            Debug.WriteLine(exception);
            PrivilegeStatusText.Text = "현재 실행 권한: 확인할 수 없음";
            AdminQuickButton.Content = "🛡 권한 확인 실패";
            AdminQuickButton.IsEnabled = false;
            RestartAsAdministratorButton.IsEnabled = false;
        }
    }

    private void UpdateMasterHotkeyDisplay()
    {
        MasterHotkeyText.Text = _settings.MasterHotkey is null
            ? "설정 안 됨"
            : _settings.MasterHotkey.DisplayText;
        MasterHotkeyButton.Content = _settings.MasterHotkey is null ? "단축키 설정" : "단축키 변경";
        MasterHotkeyRemoveButton.Visibility = _settings.MasterHotkey is null
            ? Visibility.Collapsed
            : Visibility.Visible;

        MasterVolumeHotkeyText.Text = _settings.MasterVolumeHotkey is null
            ? "볼륨 단축키: 설정 안 됨"
            : $"볼륨 단축키: {_settings.MasterVolumeHotkey.DisplayText}";
        MasterVolumeHotkeyButton.Content = _settings.MasterVolumeHotkey is null
            ? "단축키 설정"
            : "단축키 변경";
        MasterVolumeHotkeyRemoveButton.Visibility = _settings.MasterVolumeHotkey is null
            ? Visibility.Collapsed
            : Visibility.Visible;
    }

    private void UpdateMasterVolumeSettingDisplay()
    {
        var percent = Math.Clamp(_settings.MasterVolumePercent, 0, 100);
        _isSynchronizingPresetInputs = true;

        try
        {
            MasterVolumeInputTextBox.Text = percent.ToString();
        }
        finally
        {
            _isSynchronizingPresetInputs = false;
        }

        MasterVolumeSlider.Value = percent;
        MasterVolumeInputErrorText.Visibility = Visibility.Collapsed;
        MasterVolumePresetText.Text = $"설정값: {percent}%";
        MasterVolumeApplyButton.Content = _volumePresetToggleService.IsMasterPresetActive
            ? "기본 볼륨으로 복원"
            : $"{percent}%로 전환";
    }

    private void UpdateThemeSelection()
    {
        _isUpdatingThemeSelection = true;

        try
        {
            ThemeComboBox.SelectedItem = ThemeComboBox.Items
                .OfType<ComboBoxItem>()
                .FirstOrDefault(item => string.Equals(
                    item.Tag?.ToString(),
                    _settings.Theme.ToString(),
                    StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            _isUpdatingThemeSelection = false;
        }
    }

    private void UpdateMasterAudioState(bool isMuted, int? volumePercent = null)
    {
        _masterIsMuted = isMuted;
        if (volumePercent is not null)
        {
            _masterVolumePercent = Math.Clamp(volumePercent.Value, 0, 100);
        }
        _audioStateRevision++;
        MasterAudioStatusText.Text = isMuted ? "현재 상태: 음소거" : "현재 상태: 음소거 해제";
        MasterVolumeStatusText.Text = _masterVolumePercent is int currentVolume
            ? $"현재 볼륨: {currentVolume}%"
            : "현재 볼륨: 확인 중";
        MasterMuteButton.Content = isMuted ? "음소거 해제" : "음소거";
        AudioErrorText.Visibility = Visibility.Collapsed;
        RefreshOverlayHud();
    }

    private async void OverlayRefreshTimer_Tick(object? sender, EventArgs e) =>
        await RefreshOverlayAudioStateAsync();

    private async Task RefreshOverlayAudioStateAsync()
    {
        if (_isClosed || _isOverlayRefreshRunning)
        {
            return;
        }

        _isOverlayRefreshRunning = true;
        var revisionAtStart = _audioStateRevision;

        try
        {
            var snapshot = await Task.Run(() => new OverlayAudioSnapshot(
                _audioService.CaptureMasterVolumeSnapshot(),
                _audioService.GetActiveApplicationSessions()));

            if (_isClosed || revisionAtStart != _audioStateRevision)
            {
                return;
            }

            var masterBaselineInvalidated =
                _volumePresetToggleService.InvalidateStaleMasterBaseline(
                    snapshot.MasterSnapshot.DeviceId);
            var applicationBaselineInvalidated =
                _volumePresetToggleService.InvalidateStaleApplicationBaselines(
                    snapshot.ApplicationSessions);
            _masterIsMuted = snapshot.MasterSnapshot.IsMuted;
            _masterVolumePercent = snapshot.MasterSnapshot.VolumePercent;
            _activeApplicationSessions = snapshot.ApplicationSessions;
            _audioStateRevision++;

            if (masterBaselineInvalidated)
            {
                UpdateMasterVolumeSettingDisplay();
            }

            if (applicationBaselineInvalidated)
            {
                RefreshApplicationSessions();
                return;
            }

            RefreshOverlayHud();
        }
        catch (Exception exception)
        {
            Debug.WriteLine($"Overlay state refresh failed: {exception}");
        }
        finally
        {
            _isOverlayRefreshRunning = false;
        }
    }

    private void RefreshOverlayHud()
    {
        var activeSessions = _activeApplicationSessions.ToDictionary(
            session => session.ApplicationKey,
            StringComparer.OrdinalIgnoreCase);
        var targets = new List<OverlayTargetState>
        {
            new(
                HotkeyBinding.MasterTargetId,
                "Master",
                _masterIsMuted switch
                {
                    true => OverlayTargetStatus.Muted,
                    false => OverlayTargetStatus.Unmuted,
                    null => OverlayTargetStatus.Unknown
                },
                _masterVolumePercent)
        };

        foreach (var processName in _settings.ApplicationBindings
                     .Select(setting => setting.ProcessName)
                     .Where(processName => !string.IsNullOrWhiteSpace(processName))
                     .Distinct(StringComparer.OrdinalIgnoreCase)
                     .OrderBy(processName => processName, StringComparer.OrdinalIgnoreCase))
        {
            var status = OverlayTargetStatus.NotRunning;
            int? volumePercent = null;
            var hasMixedVolume = false;

            if (activeSessions.TryGetValue(processName, out var session))
            {
                status = session.HasMixedMuteState
                    ? OverlayTargetStatus.Mixed
                    : session.IsMuted
                        ? OverlayTargetStatus.Muted
                        : OverlayTargetStatus.Unmuted;
                volumePercent = session.VolumePercent;
                hasMixedVolume = session.HasMixedVolume;
            }

            targets.Add(new OverlayTargetState(
                HotkeyBinding.GetApplicationTargetId(processName),
                processName,
                status,
                volumePercent,
                hasMixedVolume));
        }

        _overlayService.UpdateTargets(targets);
    }

    private void ShowAudioError(Exception exception)
    {
        Debug.WriteLine(exception);
        MasterAudioStatusText.Text = "현재 상태: 확인할 수 없음";
        MasterVolumeStatusText.Text = "현재 볼륨: 확인할 수 없음";
        MasterMuteButton.Content = "음소거 상태 전환";
        AudioErrorText.Text = "기본 오디오 장치를 제어할 수 없습니다. 장치 연결 상태를 확인한 뒤 다시 시도해 주세요.";
        AudioErrorText.Visibility = Visibility.Visible;
    }

    private void ShowApplicationError(string message, Exception exception)
    {
        Debug.WriteLine(exception);
        ApplicationErrorText.Text = message;
        ApplicationErrorText.Visibility = Visibility.Visible;
    }

    private void ShowHotkeyError(string message)
    {
        HotkeyErrorText.Text = message;
        HotkeyErrorText.Visibility = Visibility.Visible;
    }

    private void ShowHotkeyWarnings(IEnumerable<string> warnings)
    {
        var messages = warnings.Where(message => !string.IsNullOrWhiteSpace(message)).ToArray();
        if (messages.Length > 0) ShowHotkeyError(string.Join(Environment.NewLine, messages));
    }

    private static string GetTargetDisplayName(HotkeyBinding binding)
    {
        var actionName = binding.ActionType == HotkeyActionType.ToggleMute
            ? "음소거"
            : "볼륨";
        return binding.TargetType == HotkeyTargetType.MasterAudio
            ? $"Master Audio {actionName} 단축키"
            : $"{binding.ProcessName} {actionName} 단축키";
    }

    private sealed record ApplicationSessionItem(
        string ApplicationKey,
        string ApplicationName,
        ImageSource Icon,
        string ProcessIdText,
        string StatusText,
        string VolumeText,
        string ToggleButtonText,
        string MuteHotkeyText,
        string MuteHotkeyButtonText,
        Visibility MuteRemoveButtonVisibility,
        string VolumeHotkeyText,
        string VolumeHotkeyButtonText,
        Visibility VolumeRemoveButtonVisibility,
        int VolumePercent,
        string VolumeToggleButtonText,
        bool IsRunning);

    private sealed record OverlayAudioSnapshot(
        MasterVolumeSnapshot MasterSnapshot,
        IReadOnlyList<ApplicationAudioSession> ApplicationSessions);
}
