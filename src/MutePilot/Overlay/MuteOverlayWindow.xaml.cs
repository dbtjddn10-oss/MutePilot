using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Interop;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using MutePilot.Branding;

namespace MutePilot.Overlay;

public partial class MuteOverlayWindow : Window
{
    private const int GwlExStyle = -20;
    private const int WmNcHitTest = 0x0084;
    private const int HtTransparent = -1;
    private const int WsExTransparent = 0x00000020;
    private const int WsExToolWindow = 0x00000080;
    private const int WsExNoActivate = 0x08000000;
    private const uint MonitorDefaultToNearest = 0x00000002;
    private const int MaximumApplicationRows = 7;
    private const double WorkAreaMargin = 12;
    private const double ExpandedWidth = 252;
    private const double MinimizedWidth = 176;

    private readonly DispatcherTimer _configurationCommitTimer;
    private HwndSource? _windowSource;
    private bool _isApplyingConfiguration;
    private bool _isFullscreenDisplayOnly;
    private bool _isLocked = true;
    private bool _isMinimized;
    private double _overlayOpacity = 1.0;

    public MuteOverlayWindow()
    {
        InitializeComponent();
        var compactBrandIcon = BrandingAssetService.TryLoadCompactBrandIcon();
        MiniBrandImage.Source = compactBrandIcon;
        MiniBrandImage.Visibility = compactBrandIcon is null
            ? Visibility.Collapsed
            : Visibility.Visible;
        MiniBrandFallback.Visibility = compactBrandIcon is null
            ? Visibility.Visible
            : Visibility.Collapsed;
        _configurationCommitTimer = new DispatcherTimer(
            TimeSpan.FromMilliseconds(350),
            DispatcherPriority.Background,
            ConfigurationCommitTimer_Tick,
            Dispatcher);
        _configurationCommitTimer.Stop();
    }

    public event EventHandler<OverlayConfigurationChangedEventArgs>? ConfigurationChanged;

    public event EventHandler? CloseRequested;

    public event EventHandler<OverlayMuteToggleRequestedEventArgs>? MuteToggleRequested;

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);

        var handle = new WindowInteropHelper(this).Handle;
        var extendedStyle = GetWindowLongPtr(handle, GwlExStyle).ToInt64();
        extendedStyle |= WsExToolWindow | WsExNoActivate;
        SetWindowLongPtr(handle, GwlExStyle, new nint(extendedStyle));

        _windowSource = HwndSource.FromHwnd(handle);
        _windowSource?.AddHook(WindowMessageHook);
        ApplyInteractionState();
    }

    protected override void OnClosed(EventArgs e)
    {
        _configurationCommitTimer.Stop();
        _windowSource?.RemoveHook(WindowMessageHook);
        _windowSource = null;
        base.OnClosed(e);
    }

    public void UpdateTargets(IReadOnlyList<OverlayTargetState> targets)
    {
        var master = targets.Take(1);
        var applications = targets.Skip(1).Take(MaximumApplicationRows);
        var rows = master.Concat(applications)
            .Select(CreateRow)
            .ToList();
        var hiddenCount = Math.Max(0, targets.Count - rows.Count);

        if (hiddenCount > 0)
        {
            rows.Add(new OverlayTargetRow(
                string.Empty,
                $"외 {hiddenCount}개",
                string.Empty,
                string.Empty,
                Brushes.Gray,
                0.65,
                false));
        }

        TargetItemsControl.ItemsSource = rows;
        MiniStatusText.Text = rows.Count > 0
            ? $"Master {rows[0].StatusText}"
            : "Master 확인 중";
        UpdateLayout();
        EnsurePositionOnScreen();
    }

    public void ApplyConfiguration(OverlayConfiguration configuration)
    {
        _isApplyingConfiguration = true;

        try
        {
            _isLocked = configuration.IsLocked;
            _overlayOpacity = double.IsFinite(configuration.Opacity)
                ? Math.Clamp(configuration.Opacity, 0.2, 1.0)
                : 1.0;
            HudCard.Opacity = _overlayOpacity;
            OpacitySlider.Value = _overlayOpacity * 100;
            OpacityValueText.Text = $"{Math.Round(_overlayOpacity * 100):0}%";
            ApplyInteractionState();
            UpdateLayout();

            if (configuration.Left is double left &&
                configuration.Top is double top &&
                double.IsFinite(left) &&
                double.IsFinite(top))
            {
                Left = left;
                Top = top;
                EnsurePositionOnScreen();
            }
            else
            {
                PositionNearPrimaryWorkAreaTopRight();
            }
        }
        finally
        {
            _isApplyingConfiguration = false;
        }
    }

    public void SetFullscreenDisplayOnly(bool isFullscreenDisplayOnly)
    {
        if (_isFullscreenDisplayOnly == isFullscreenDisplayOnly)
        {
            return;
        }

        _isFullscreenDisplayOnly = isFullscreenDisplayOnly;
        ApplyInteractionState();
        UpdateLayout();
        EnsurePositionOnScreen();
    }

    public void PositionNearPrimaryWorkAreaTopRight()
    {
        UpdateLayout();
        var workArea = SystemParameters.WorkArea;
        Left = workArea.Right - ActualWidth - WorkAreaMargin;
        Top = workArea.Top + WorkAreaMargin;
    }

    private void LockButton_Click(object sender, RoutedEventArgs e)
    {
        if (_isFullscreenDisplayOnly)
        {
            return;
        }

        _isLocked = !_isLocked;
        ApplyInteractionState();
        UpdateLayout();
        EnsurePositionOnScreen();
        RaiseConfigurationChanged();
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        if (!_isFullscreenDisplayOnly)
        {
            CloseRequested?.Invoke(this, EventArgs.Empty);
        }
    }

    private void MinimizeButton_Click(object sender, RoutedEventArgs e)
    {
        if (_isFullscreenDisplayOnly)
        {
            return;
        }

        _isMinimized = true;
        ApplyInteractionState();
        UpdateLayout();
        EnsurePositionOnScreen();
    }

    private void RestoreButton_Click(object sender, RoutedEventArgs e)
    {
        if (_isFullscreenDisplayOnly)
        {
            return;
        }

        _isMinimized = false;
        ApplyInteractionState();
        UpdateLayout();
        EnsurePositionOnScreen();
    }

    private void AudioToggleButton_Click(object sender, RoutedEventArgs e)
    {
        if (!_isFullscreenDisplayOnly && sender is System.Windows.Controls.Button { Tag: string targetId })
        {
            MuteToggleRequested?.Invoke(
                this,
                new OverlayMuteToggleRequestedEventArgs(targetId));
        }
    }

    private void DragSurface_DragDelta(object sender, DragDeltaEventArgs e)
    {
        if (_isFullscreenDisplayOnly || _isLocked)
        {
            return;
        }

        Left += e.HorizontalChange;
        Top += e.VerticalChange;
    }

    private void DragSurface_DragCompleted(object sender, DragCompletedEventArgs e)
    {
        if (_isFullscreenDisplayOnly || _isLocked)
        {
            return;
        }

        _configurationCommitTimer.Stop();
        EnsurePositionOnScreen();
        RaiseConfigurationChanged();
    }

    private void OpacitySlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (HudCard is null || OpacityValueText is null)
        {
            return;
        }

        _overlayOpacity = Math.Clamp(e.NewValue / 100, 0.2, 1.0);
        HudCard.Opacity = _overlayOpacity;
        OpacityValueText.Text = $"{Math.Round(_overlayOpacity * 100):0}%";

        if (_isApplyingConfiguration || _isFullscreenDisplayOnly || _isLocked)
        {
            return;
        }

        RestartConfigurationCommitTimer();
    }

    private void Window_LocationChanged(object? sender, EventArgs e)
    {
        if (_isApplyingConfiguration || _isFullscreenDisplayOnly || _isLocked || !IsVisible)
        {
            return;
        }

        RestartConfigurationCommitTimer();
    }

    private void RestartConfigurationCommitTimer()
    {
        _configurationCommitTimer.Stop();
        _configurationCommitTimer.Start();
    }

    private void ConfigurationCommitTimer_Tick(object? sender, EventArgs e)
    {
        _configurationCommitTimer.Stop();
        RaiseConfigurationChanged();
    }

    private void ApplyInteractionState()
    {
        if (HeaderLockButton is null || MinimizeButton is null || CloseButton is null ||
            RestoreButton is null || MiniCloseButton is null || ConfigurationPanel is null ||
            DragSurface is null || MiniDragSurface is null || TargetItemsControl is null ||
            FullHudPanel is null || MiniHudPanel is null)
        {
            return;
        }

        Width = _isMinimized ? MinimizedWidth : ExpandedWidth;
        FullHudPanel.Visibility = _isMinimized ? Visibility.Collapsed : Visibility.Visible;
        MiniHudPanel.Visibility = _isMinimized ? Visibility.Visible : Visibility.Collapsed;
        HeaderLockButton.Visibility = _isFullscreenDisplayOnly
            ? Visibility.Collapsed
            : Visibility.Visible;
        MinimizeButton.Visibility = _isFullscreenDisplayOnly
            ? Visibility.Collapsed
            : Visibility.Visible;
        CloseButton.Visibility = _isFullscreenDisplayOnly
            ? Visibility.Collapsed
            : Visibility.Visible;
        RestoreButton.Visibility = _isFullscreenDisplayOnly
            ? Visibility.Collapsed
            : Visibility.Visible;
        MiniCloseButton.Visibility = _isFullscreenDisplayOnly
            ? Visibility.Collapsed
            : Visibility.Visible;
        HeaderLockButton.Content = _isLocked ? "\uE72E" : "\uE785";
        HeaderLockButton.Tag = _isLocked ? "Locked" : "Unlocked";
        HeaderLockButton.ToolTip = _isLocked ? "위치 잠금 해제" : "위치 잠금";
        TargetItemsControl.IsHitTestVisible = !_isFullscreenDisplayOnly;
        ConfigurationPanel.Visibility = !_isMinimized && !_isFullscreenDisplayOnly && !_isLocked
            ? Visibility.Visible
            : Visibility.Collapsed;
        DragSurface.Cursor = !_isFullscreenDisplayOnly && !_isLocked
            ? Cursors.SizeAll
            : Cursors.Arrow;
        MiniDragSurface.Cursor = DragSurface.Cursor;
        SetNativeClickThrough(_isFullscreenDisplayOnly);
    }

    private void SetNativeClickThrough(bool isClickThrough)
    {
        var handle = new WindowInteropHelper(this).Handle;

        if (handle == nint.Zero)
        {
            return;
        }

        var extendedStyle = GetWindowLongPtr(handle, GwlExStyle).ToInt64();
        extendedStyle = isClickThrough
            ? extendedStyle | WsExTransparent
            : extendedStyle & ~WsExTransparent;
        SetWindowLongPtr(handle, GwlExStyle, new nint(extendedStyle));
    }

    private nint WindowMessageHook(
        nint windowHandle,
        int message,
        nint wordParameter,
        nint longParameter,
        ref bool handled)
    {
        if (message != WmNcHitTest)
        {
            return nint.Zero;
        }

        if (_isFullscreenDisplayOnly)
        {
            handled = true;
            return HtTransparent;
        }

        return nint.Zero;
    }

    private void RaiseConfigurationChanged()
    {
        ConfigurationChanged?.Invoke(
            this,
            new OverlayConfigurationChangedEventArgs(new OverlayConfiguration(
                _isLocked,
                _overlayOpacity,
                Left,
                Top)));
    }

    private void EnsurePositionOnScreen()
    {
        if (!double.IsFinite(Left) || !double.IsFinite(Top))
        {
            PositionNearPrimaryWorkAreaTopRight();
            return;
        }

        var monitor = MonitorFromPoint(
            new NativePoint((int)Math.Round(Left), (int)Math.Round(Top)),
            MonitorDefaultToNearest);
        var monitorInfo = new MonitorInfo { Size = Marshal.SizeOf<MonitorInfo>() };

        if (monitor == nint.Zero || !GetMonitorInfo(monitor, ref monitorInfo))
        {
            PositionNearPrimaryWorkAreaTopRight();
            return;
        }

        var workArea = monitorInfo.WorkArea;
        var width = Math.Max(ActualWidth, Width);
        var height = Math.Max(ActualHeight, 1);
        Left = Math.Clamp(Left, workArea.Left, Math.Max(workArea.Left, workArea.Right - width));
        Top = Math.Clamp(Top, workArea.Top, Math.Max(workArea.Top, workArea.Bottom - height));
    }

    private static OverlayTargetRow CreateRow(OverlayTargetState target)
    {
        var volumeText = target.HasMixedVolume
            ? "혼합"
            : target.VolumePercent is int percent
                ? $"{percent}%"
                : string.Empty;

        return target.Status switch
        {
            OverlayTargetStatus.Muted => new OverlayTargetRow(
                target.TargetId,
                target.DisplayName,
                volumeText,
                "🔇",
                Brushes.LightCoral,
                1,
                true),
            OverlayTargetStatus.Unmuted => new OverlayTargetRow(
                target.TargetId,
                target.DisplayName,
                volumeText,
                "🔊",
                Brushes.LightGreen,
                1,
                true),
            OverlayTargetStatus.Mixed => new OverlayTargetRow(
                target.TargetId,
                target.DisplayName,
                string.IsNullOrEmpty(volumeText) ? "혼합" : $"혼합 · {volumeText}",
                "◐",
                Brushes.Khaki,
                1,
                true),
            OverlayTargetStatus.NotRunning => new OverlayTargetRow(
                target.TargetId, target.DisplayName, "실행 안 됨", "—", Brushes.Gray, 0.68, false),
            _ => new OverlayTargetRow(
                target.TargetId, target.DisplayName, "확인 중", "—", Brushes.Gray, 0.68, false)
        };
    }

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW", SetLastError = true)]
    private static extern nint GetWindowLongPtr64(nint windowHandle, int index);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongW", SetLastError = true)]
    private static extern int GetWindowLong32(nint windowHandle, int index);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW", SetLastError = true)]
    private static extern nint SetWindowLongPtr64(nint windowHandle, int index, nint newValue);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongW", SetLastError = true)]
    private static extern int SetWindowLong32(nint windowHandle, int index, int newValue);

    [DllImport("user32.dll")]
    private static extern nint MonitorFromPoint(NativePoint point, uint flags);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetMonitorInfo(nint monitor, ref MonitorInfo monitorInfo);

    private static nint GetWindowLongPtr(nint windowHandle, int index)
    {
        return nint.Size == 8
            ? GetWindowLongPtr64(windowHandle, index)
            : new nint(GetWindowLong32(windowHandle, index));
    }

    private static void SetWindowLongPtr(nint windowHandle, int index, nint newValue)
    {
        if (nint.Size == 8)
        {
            SetWindowLongPtr64(windowHandle, index, newValue);
        }
        else
        {
            SetWindowLong32(windowHandle, index, newValue.ToInt32());
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private readonly struct NativePoint(int x, int y)
    {
        public readonly int X = x;
        public readonly int Y = y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct MonitorInfo
    {
        public int Size;
        public NativeRect Monitor;
        public NativeRect WorkArea;
        public uint Flags;
    }

    private sealed record OverlayTargetRow(
        string TargetId,
        string Name,
        string StatusText,
        string ToggleGlyph,
        Brush StatusBrush,
        double Opacity,
        bool CanToggle);
}
