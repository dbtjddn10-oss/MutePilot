using System.Diagnostics;
using System.Windows.Threading;

namespace MutePilot.Overlay;

public sealed class OverlayService : IOverlayService
{
    private static readonly TimeSpan FullscreenPollInterval = TimeSpan.FromMilliseconds(400);

    private readonly Dispatcher _dispatcher;
    private readonly IFullscreenStateDetector _fullscreenStateDetector;
    private readonly DispatcherTimer _fullscreenTimer;
    private MuteOverlayWindow? _window;
    private IReadOnlyList<OverlayTargetState> _targets = [];
    private OverlayConfiguration _configuration = new(true, 1.0, null, null);
    private volatile bool _isEnabled = true;
    private bool _isFullscreenDisplayOnly;
    private bool _disposed;

    public OverlayService(Dispatcher dispatcher) :
        this(dispatcher, new FullscreenStateDetector())
    {
    }

    public OverlayService(
        Dispatcher dispatcher,
        IFullscreenStateDetector fullscreenStateDetector)
    {
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        _fullscreenStateDetector = fullscreenStateDetector ??
            throw new ArgumentNullException(nameof(fullscreenStateDetector));
        _fullscreenTimer = new DispatcherTimer(
            FullscreenPollInterval,
            DispatcherPriority.Background,
            FullscreenTimer_Tick,
            dispatcher);
        _fullscreenTimer.Start();
    }

    public event EventHandler<OverlayConfigurationChangedEventArgs>? ConfigurationChanged;

    public event EventHandler? CloseRequested;

    public event EventHandler<OverlayMuteToggleRequestedEventArgs>? MuteToggleRequested;

    public event EventHandler<OverlayVolumeChangeRequestedEventArgs>? VolumeChangeRequested;

    public bool IsEnabled => _isEnabled;

    public bool IsFullscreenDisplayOnly => _isFullscreenDisplayOnly;

    public void SetEnabled(bool isEnabled)
    {
        if (_disposed)
        {
            return;
        }

        _isEnabled = isEnabled;
        RunOnDispatcher(() =>
        {
            RefreshFullscreenState();

            if (isEnabled)
            {
                ShowCore();
            }
            else
            {
                HideCore();
            }
        });
    }

    public void Configure(OverlayConfiguration configuration)
    {
        if (_disposed || configuration is null)
        {
            return;
        }

        var normalized = NormalizeConfiguration(configuration);
        RunOnDispatcher(() =>
        {
            _configuration = normalized;
            _window?.ApplyConfiguration(_configuration);
        });
    }

    public void UpdateTargets(IReadOnlyList<OverlayTargetState> targets)
    {
        if (_disposed || targets is null)
        {
            return;
        }

        var snapshot = targets
            .Where(target => !string.IsNullOrWhiteSpace(target.TargetId) &&
                             !string.IsNullOrWhiteSpace(target.DisplayName))
            .Select(target => target with { DisplayName = target.DisplayName.Trim() })
            .ToArray();

        RunOnDispatcher(() => UpdateTargetsCore(snapshot));
    }

    public void Hide()
    {
        if (_disposed)
        {
            return;
        }

        RunOnDispatcher(HideCore);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        if (!_dispatcher.CheckAccess())
        {
            _dispatcher.Invoke(Dispose);
            return;
        }

        _disposed = true;
        _isEnabled = false;
        _fullscreenTimer.Stop();
        _fullscreenTimer.Tick -= FullscreenTimer_Tick;

        if (_window is not null)
        {
            _window.ConfigurationChanged -= Window_ConfigurationChanged;
            _window.CloseRequested -= Window_CloseRequested;
            _window.MuteToggleRequested -= Window_MuteToggleRequested;
            _window.VolumeChangeRequested -= Window_VolumeChangeRequested;
            _window.Close();
            _window = null;
        }
    }

    private void UpdateTargetsCore(IReadOnlyList<OverlayTargetState> targets)
    {
        _targets = targets;

        if (_targets.Count == 0)
        {
            HideCore();
        }
        else if (_isEnabled)
        {
            ShowCore();
        }
    }

    private void ShowCore()
    {
        if (_disposed || !_isEnabled || _targets.Count == 0)
        {
            return;
        }

        var window = _window;
        var isNewWindow = window is null;

        if (isNewWindow)
        {
            window = new MuteOverlayWindow();
            window.ConfigurationChanged += Window_ConfigurationChanged;
            window.CloseRequested += Window_CloseRequested;
            window.MuteToggleRequested += Window_MuteToggleRequested;
            window.VolumeChangeRequested += Window_VolumeChangeRequested;
            _window = window;
        }

        var activeWindow = window ?? throw new InvalidOperationException(
            "Overlay window could not be created.");
        activeWindow.UpdateTargets(_targets);

        if (!activeWindow.IsVisible)
        {
            activeWindow.Show();
        }

        if (isNewWindow)
        {
            activeWindow.ApplyConfiguration(_configuration);
        }

        activeWindow.SetFullscreenDisplayOnly(_isFullscreenDisplayOnly);
    }

    private void FullscreenTimer_Tick(object? sender, EventArgs e) => RefreshFullscreenState();

    private void RefreshFullscreenState()
    {
        bool nextState;

        try
        {
            nextState = _fullscreenStateDetector.IsForegroundWindowFullscreen();
        }
        catch (Exception exception)
        {
            Debug.WriteLine($"Fullscreen state detection failed: {exception}");
            nextState = false;
        }

        if (_isFullscreenDisplayOnly == nextState)
        {
            return;
        }

        _isFullscreenDisplayOnly = nextState;
        _window?.SetFullscreenDisplayOnly(nextState);
    }

    private void Window_ConfigurationChanged(
        object? sender,
        OverlayConfigurationChangedEventArgs e)
    {
        _configuration = NormalizeConfiguration(e.Configuration);
        ConfigurationChanged?.Invoke(
            this,
            new OverlayConfigurationChangedEventArgs(_configuration));
    }

    private void Window_CloseRequested(object? sender, EventArgs e) =>
        CloseRequested?.Invoke(this, EventArgs.Empty);

    private void Window_MuteToggleRequested(
        object? sender,
        OverlayMuteToggleRequestedEventArgs e) => MuteToggleRequested?.Invoke(this, e);

    private void Window_VolumeChangeRequested(
        object? sender,
        OverlayVolumeChangeRequestedEventArgs e) => VolumeChangeRequested?.Invoke(this, e);

    private static OverlayConfiguration NormalizeConfiguration(OverlayConfiguration configuration)
    {
        var opacity = double.IsFinite(configuration.Opacity)
            ? Math.Clamp(configuration.Opacity, 0.2, 1.0)
            : 1.0;
        var hasValidPosition = configuration.Left is double left &&
                               configuration.Top is double top &&
                               double.IsFinite(left) &&
                               double.IsFinite(top);

        return configuration with
        {
            Opacity = opacity,
            Left = hasValidPosition ? configuration.Left : null,
            Top = hasValidPosition ? configuration.Top : null
        };
    }

    private void HideCore()
    {
        if (_window?.IsVisible == true)
        {
            _window.Hide();
        }
    }

    private void RunOnDispatcher(Action action)
    {
        if (_dispatcher.HasShutdownStarted || _dispatcher.HasShutdownFinished)
        {
            return;
        }

        if (_dispatcher.CheckAccess())
        {
            TryExecute(action);
            return;
        }

        _dispatcher.BeginInvoke(() => TryExecute(action));
    }

    private static void TryExecute(Action action)
    {
        try
        {
            action();
        }
        catch (Exception exception)
        {
            Debug.WriteLine(exception);
        }
    }
}
