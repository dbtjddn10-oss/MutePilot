namespace MutePilot.Overlay;

public interface IOverlayService : IDisposable
{
    bool IsEnabled { get; }

    void SetEnabled(bool isEnabled);

    void UpdateTargets(IReadOnlyList<OverlayTargetState> targets);

    void Hide();
}

public sealed record OverlayTargetState(
    string TargetId,
    string DisplayName,
    OverlayTargetStatus Status);

public enum OverlayTargetStatus
{
    Unknown,
    Muted,
    Unmuted,
    Mixed,
    NotRunning
}
