using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using MutePilot.Audio;
using MutePilot.Hotkeys;
using MutePilot.Overlay;
using MutePilot.Settings;

namespace MutePilot;

public partial class MainWindow : Window
{
    private readonly IAudioService _audioService = new AudioService();
    private readonly IHotkeyService _hotkeyService = new HotkeyService();
    private readonly ISettingsService _settingsService = new SettingsService();
    private readonly IOverlayService _overlayService;
    private AppSettings _settings = new();
    private bool _hotkeysInitialized;
    private bool _isCapturingHotkey;

    public MainWindow()
    {
        InitializeComponent();
        _overlayService = new OverlayService(Dispatcher);
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
            _overlayService.SetEnabled(_settings.OverlayEnabled);
            UpdateOverlaySettingDisplay();

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

        ShowHotkeyWarnings(warnings);
        UpdateMasterHotkeyDisplay();
    }

    protected override void OnClosed(EventArgs e)
    {
        _hotkeyService.HotkeyPressed -= HotkeyService_HotkeyPressed;
        _hotkeyService.Dispose();
        _overlayService.Dispose();
        base.OnClosed(e);
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
            _overlayService.ShowMuteState("Master Audio", isMuted);
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
        ConfigureHotkey(_settings.MasterHotkey, HotkeyBinding.ForMasterAudio);

    private void MasterHotkeyRemoveButton_Click(object sender, RoutedEventArgs e) =>
        RemoveHotkey(HotkeyBinding.MasterTargetId);

    private void OverlayToggleButton_Click(object sender, RoutedEventArgs e)
    {
        var previousSettings = CloneSettings(_settings);
        _settings.OverlayEnabled = !_settings.OverlayEnabled;

        try
        {
            _settingsService.Save(_settings);
            _overlayService.SetEnabled(_settings.OverlayEnabled);
            UpdateOverlaySettingDisplay();
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
            var session = _audioService.ToggleApplicationMute(applicationKey);
            _overlayService.ShowMuteState(session.ApplicationName, session.IsMuted);
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
                gesture => HotkeyBinding.ForApplication(processName, gesture));
        }
    }

    private void ApplicationHotkeyRemoveButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string processName })
        {
            RemoveHotkey(HotkeyBinding.GetApplicationTargetId(processName));
        }
    }

    private void ConfigureHotkey(
        HotkeyGesture? currentGesture,
        Func<HotkeyGesture, HotkeyBinding> createBinding)
    {
        if (!_hotkeysInitialized)
        {
            ShowHotkeyError("전역 단축키 서비스가 준비되지 않았습니다.");
            return;
        }

        var dialog = new HotkeyCaptureWindow(currentGesture) { Owner = this };

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
        var previousBinding = FindConfiguredBinding(newBinding.TargetId);

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
            RollBackRegistration(newBinding.TargetId, previousBinding);
            ShowHotkeyError("설정 파일을 저장하지 못해 단축키 변경을 취소했습니다.");
        }

        UpdateMasterHotkeyDisplay();
        RefreshApplicationSessions();
    }

    private void RemoveHotkey(string targetId)
    {
        var previousBinding = FindConfiguredBinding(targetId);

        if (previousBinding is null)
        {
            return;
        }

        if (!_hotkeyService.TryUnregister(targetId, out var errorMessage))
        {
            ShowHotkeyError(errorMessage);
            return;
        }

        var previousSettings = CloneSettings(_settings);
        RemoveConfiguredBinding(targetId);

        try
        {
            _settingsService.Save(_settings);
            HotkeyErrorText.Visibility = Visibility.Collapsed;
        }
        catch (Exception exception)
        {
            Debug.WriteLine(exception);
            _settings = previousSettings;
            RollBackRegistration(targetId, previousBinding);
            ShowHotkeyError("설정 파일을 저장하지 못해 단축키 삭제를 취소했습니다.");
        }

        UpdateMasterHotkeyDisplay();
        RefreshApplicationSessions();
    }

    private void RollBackRegistration(string targetId, HotkeyBinding? previousBinding)
    {
        bool restored;
        string rollbackError;

        if (previousBinding is null)
        {
            restored = _hotkeyService.TryUnregister(targetId, out rollbackError);
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
            if (e.Binding.TargetType == HotkeyTargetType.MasterAudio)
            {
                var isMuted = _audioService.ToggleMasterMuteState();
                UpdateMasterAudioState(isMuted);
                _overlayService.ShowMuteState("Master Audio", isMuted);
            }
            else if (!string.IsNullOrWhiteSpace(e.Binding.ProcessName))
            {
                var session = _audioService.ToggleApplicationMute(e.Binding.ProcessName);
                _overlayService.ShowMuteState(session.ApplicationName, session.IsMuted);
                RefreshApplicationSessions();
            }
        }
        catch (Exception exception)
        {
            Debug.WriteLine(exception);
            ShowHotkeyError(e.Binding.TargetType == HotkeyTargetType.MasterAudio
                ? "단축키로 전체 음소거 상태를 바꾸지 못했습니다."
                : $"{e.Binding.ProcessName}의 활성 오디오 세션을 찾거나 제어하지 못했습니다.");
            RefreshApplicationSessions();
        }
    }

    private void RefreshMasterAudioState()
    {
        try
        {
            UpdateMasterAudioState(_audioService.GetMasterMuteState());
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

    private void RefreshApplicationSessions()
    {
        ApplicationRefreshButton.IsEnabled = false;
        ApplicationErrorText.Visibility = Visibility.Collapsed;

        try
        {
            var activeSessions = _audioService.GetActiveApplicationSessions();
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
            ApplicationRefreshButton.IsEnabled = true;
        }
    }

    private ApplicationSessionItem CreateActiveApplicationItem(ApplicationAudioSession session)
    {
        var setting = FindApplicationSetting(session.ApplicationKey);
        return new ApplicationSessionItem(
            session.ApplicationKey,
            session.ApplicationName,
            $"PID: {string.Join(", ", session.ProcessIds)} · 세션 {session.SessionCount}개",
            session.HasMixedMuteState
                ? "현재 상태: 일부 세션 음소거"
                : session.IsMuted ? "현재 상태: 음소거" : "현재 상태: 음소거 해제",
            session.IsMuted ? "음소거 해제" : "음소거",
            setting is null ? "단축키: 설정 안 됨" : $"단축키: {setting.Hotkey.DisplayText}",
            setting is null ? "단축키 설정" : "단축키 변경",
            setting is null ? Visibility.Collapsed : Visibility.Visible,
            true);
    }

    private static ApplicationSessionItem CreateInactiveApplicationItem(ApplicationHotkeySetting setting) =>
        new(
            setting.ProcessName,
            setting.ProcessName,
            "저장된 앱 바인딩",
            "현재 상태: 실행 중이 아님",
            "음소거",
            $"단축키: {setting.Hotkey.DisplayText}",
            "단축키 변경",
            Visibility.Visible,
            false);

    private IEnumerable<HotkeyBinding> GetConfiguredBindings()
    {
        if (_settings.MasterHotkey is not null)
        {
            yield return HotkeyBinding.ForMasterAudio(_settings.MasterHotkey);
        }

        foreach (var setting in _settings.ApplicationBindings)
        {
            if (!string.IsNullOrWhiteSpace(setting.ProcessName) && setting.Hotkey is not null)
            {
                yield return HotkeyBinding.ForApplication(setting.ProcessName, setting.Hotkey);
            }
        }
    }

    private IEnumerable<Func<HotkeyBinding>> GetConfiguredBindingFactories()
    {
        if (_settings.MasterHotkey is not null)
        {
            yield return () => HotkeyBinding.ForMasterAudio(_settings.MasterHotkey);
        }

        foreach (var setting in _settings.ApplicationBindings)
        {
            var savedSetting = setting;
            yield return () => HotkeyBinding.ForApplication(
                savedSetting.ProcessName,
                savedSetting.Hotkey);
        }
    }

    private HotkeyBinding? FindConfiguredBinding(string targetId) =>
        GetConfiguredBindings().FirstOrDefault(binding =>
            string.Equals(binding.TargetId, targetId, StringComparison.OrdinalIgnoreCase));

    private ApplicationHotkeySetting? FindApplicationSetting(string processName) =>
        _settings.ApplicationBindings.FirstOrDefault(setting =>
            string.Equals(setting.ProcessName, processName, StringComparison.OrdinalIgnoreCase));

    private void SetConfiguredBinding(HotkeyBinding binding)
    {
        if (binding.TargetType == HotkeyTargetType.MasterAudio)
        {
            _settings.MasterHotkey = binding.Gesture;
            return;
        }

        var processName = binding.ProcessName!;
        _settings.ApplicationBindings.RemoveAll(setting =>
            string.Equals(setting.ProcessName, processName, StringComparison.OrdinalIgnoreCase));
        _settings.ApplicationBindings.Add(new ApplicationHotkeySetting(processName, binding.Gesture));
    }

    private void RemoveConfiguredBinding(string targetId)
    {
        if (string.Equals(targetId, HotkeyBinding.MasterTargetId, StringComparison.OrdinalIgnoreCase))
        {
            _settings.MasterHotkey = null;
            return;
        }

        _settings.ApplicationBindings.RemoveAll(setting => string.Equals(
            HotkeyBinding.GetApplicationTargetId(setting.ProcessName),
            targetId,
            StringComparison.OrdinalIgnoreCase));
    }

    private static AppSettings CloneSettings(AppSettings settings) => new()
    {
        OverlayEnabled = settings.OverlayEnabled,
        MasterHotkey = settings.MasterHotkey,
        ApplicationBindings = settings.ApplicationBindings.ToList()
    };

    private void UpdateOverlaySettingDisplay()
    {
        OverlayToggleButton.Content = _settings.OverlayEnabled ? "ON" : "OFF";
        OverlayToggleButton.ToolTip = _settings.OverlayEnabled
            ? "음소거 상태 오버레이를 끕니다."
            : "음소거 상태 오버레이를 켭니다.";
    }

    private void UpdateMasterHotkeyDisplay()
    {
        MasterHotkeyText.Text = _settings.MasterHotkey is null
            ? "단축키: 설정 안 됨"
            : $"단축키: {_settings.MasterHotkey.DisplayText}";
        MasterHotkeyButton.Content = _settings.MasterHotkey is null ? "단축키 설정" : "단축키 변경";
        MasterHotkeyRemoveButton.Visibility = _settings.MasterHotkey is null
            ? Visibility.Collapsed
            : Visibility.Visible;
    }

    private void UpdateMasterAudioState(bool isMuted)
    {
        MasterAudioStatusText.Text = isMuted ? "현재 상태: 음소거" : "현재 상태: 음소거 해제";
        MasterMuteButton.Content = isMuted ? "음소거 해제" : "음소거";
        AudioErrorText.Visibility = Visibility.Collapsed;
    }

    private void ShowAudioError(Exception exception)
    {
        Debug.WriteLine(exception);
        MasterAudioStatusText.Text = "현재 상태: 확인할 수 없음";
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

    private static string GetTargetDisplayName(HotkeyBinding binding) =>
        binding.TargetType == HotkeyTargetType.MasterAudio
            ? "전체 음소거 단축키"
            : $"{binding.ProcessName} 단축키";

    private sealed record ApplicationSessionItem(
        string ApplicationKey,
        string ApplicationName,
        string ProcessIdText,
        string StatusText,
        string ToggleButtonText,
        string HotkeyText,
        string HotkeyButtonText,
        Visibility RemoveButtonVisibility,
        bool IsRunning);
}
