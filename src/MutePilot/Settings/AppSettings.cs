using MutePilot.Hotkeys;

namespace MutePilot.Settings;

public sealed class AppSettings
{
    public const int DefaultVolumePercent = 50;

    public AppTheme Theme { get; set; } = AppTheme.Dark;

    public bool OverlayEnabled { get; set; } = true;

    public bool OverlayLocked { get; set; } = true;

    public double OverlayOpacity { get; set; } = 1.0;

    public double? OverlayLeft { get; set; }

    public double? OverlayTop { get; set; }

    public HotkeyGesture? MasterHotkey { get; set; }

    public HotkeyGesture? MasterVolumeHotkey { get; set; }

    public int MasterVolumePercent { get; set; } = DefaultVolumePercent;

    public List<ApplicationHotkeySetting> ApplicationBindings { get; set; } = [];
}

public sealed record ApplicationHotkeySetting(
    string ProcessName,
    HotkeyGesture? Hotkey = null,
    HotkeyGesture? VolumeHotkey = null,
    int VolumePercent = AppSettings.DefaultVolumePercent);

public enum AppTheme
{
    Dark,
    Light,
    Pink
}
