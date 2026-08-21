namespace MutePilot.Overlay;

public interface IOverlayService : IDisposable
{
    event EventHandler<OverlayConfigurationChangedEventArgs>? ConfigurationChanged;

    event EventHandler? CloseRequested;

    event EventHandler<OverlayMuteToggleRequestedEventArgs>? MuteToggleRequested;

    bool IsEnabled { get; }

    bool IsFullscreenDisplayOnly { get; }

    void SetEnabled(bool isEnabled);

    void Configure(OverlayConfiguration configuration);

    void UpdateTargets(IReadOnlyList<OverlayTargetState> targets);

    void Hide();
}

public sealed record OverlayConfiguration(
    bool IsLocked,
    double Opacity,
    double? Left,
    double? Top);

public sealed class OverlayConfigurationChangedEventArgs(
    OverlayConfiguration configuration) : EventArgs
{
    public OverlayConfiguration Configuration { get; } = configuration;
}

public sealed class OverlayMuteToggleRequestedEventArgs(string targetId) : EventArgs
{
    public string TargetId { get; } = targetId;
}

public sealed record OverlayTargetState(
    string TargetId,
    string DisplayName,
    OverlayTargetStatus Status,
    int? VolumePercent = null,
    bool HasMixedVolume = false);

public enum OverlayTargetStatus
{
    Unknown,
    Muted,
    Unmuted,
    Mixed,
    NotRunning
}
