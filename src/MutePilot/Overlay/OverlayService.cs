using System.Diagnostics;
using System.Windows.Threading;

namespace MutePilot.Overlay;

public sealed class OverlayService : IOverlayService
{
    private readonly Dispatcher _dispatcher;
    private MuteOverlayWindow? _window;
    private IReadOnlyList<OverlayTargetState> _targets = [];
    private volatile bool _isEnabled = true;
    private bool _disposed;

    public OverlayService(Dispatcher dispatcher)
    {
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
    }

    public bool IsEnabled => _isEnabled;

    public void SetEnabled(bool isEnabled)
    {
        if (_disposed)
        {
            return;
        }

        _isEnabled = isEnabled;
        RunOnDispatcher(isEnabled ? ShowCore : HideCore);
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

        if (_window is not null)
        {
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

        _window ??= new MuteOverlayWindow();
        _window.UpdateTargets(_targets);

        if (!_window.IsVisible)
        {
            _window.Show();
        }

        _window.PositionNearPrimaryWorkAreaTopRight();
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
