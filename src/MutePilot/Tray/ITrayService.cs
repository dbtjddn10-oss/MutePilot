namespace MutePilot.Tray;

public interface ITrayService : IDisposable
{
    event EventHandler? OpenRequested;

    event EventHandler? OverlayToggleRequested;

    event EventHandler? ExitRequested;

    void SetOverlayEnabled(bool isEnabled);

    void ShowRunningInBackgroundNotice();
}
