namespace MutePilot.Overlay;

public interface IOverlayService : IDisposable
{
    bool IsEnabled { get; }

    void SetEnabled(bool isEnabled);

    void ShowMuteState(string targetName, bool isMuted);

    void Hide();
}
