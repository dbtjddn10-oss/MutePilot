using System.Diagnostics;
using System.Windows.Threading;

namespace MutePilot.Overlay;

public sealed class OverlayService : IOverlayService
{
    private static readonly TimeSpan DisplayDuration = TimeSpan.FromMilliseconds(1500);

    private readonly Dispatcher _dispatcher;
    private readonly DispatcherTimer _hideTimer;
    private MuteOverlayWindow? _window;
    private volatile bool _isEnabled = true;
    private bool _disposed;

    public OverlayService(Dispatcher dispatcher)
    {
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        _hideTimer = new DispatcherTimer(DisplayDuration, DispatcherPriority.Normal, HideTimer_Tick, dispatcher);
        _hideTimer.Stop();
    }

    public bool IsEnabled => _isEnabled;

    public void SetEnabled(bool isEnabled)
    {
        if (_disposed)
        {
            return;
        }

        _isEnabled = isEnabled;

        if (!isEnabled)
        {
            Hide();
        }
    }

    public void ShowMuteState(string targetName, bool isMuted)
    {
        if (_disposed || !_isEnabled || string.IsNullOrWhiteSpace(targetName))
        {
            return;
        }

        RunOnDispatcher(() => ShowCore(targetName.Trim(), isMuted));
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
        _hideTimer.Stop();

        if (_window is not null)
        {
            _window.Close();
            _window = null;
        }
    }

    private void ShowCore(string targetName, bool isMuted)
    {
        if (_disposed || !_isEnabled)
        {
            return;
        }

        _window ??= new MuteOverlayWindow();
        _window.UpdateState(targetName, isMuted);

        if (!_window.IsVisible)
        {
            _window.Show();
        }

        _window.PositionNearPrimaryWorkAreaTopRight();
        _hideTimer.Stop();
        _hideTimer.Start();
    }

    private void HideCore()
    {
        _hideTimer.Stop();

        if (_window?.IsVisible == true)
        {
            _window.Hide();
        }
    }

    private void HideTimer_Tick(object? sender, EventArgs e) => HideCore();

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
