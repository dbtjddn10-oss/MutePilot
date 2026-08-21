using MutePilot.Hotkeys;

namespace MutePilot.Settings;

public sealed class AppSettings
{
    public bool OverlayEnabled { get; set; } = true;

    public bool OverlayLocked { get; set; } = true;

    public double OverlayOpacity { get; set; } = 1.0;

    public double? OverlayLeft { get; set; }

    public double? OverlayTop { get; set; }

    public HotkeyGesture? MasterHotkey { get; set; }

    public List<ApplicationHotkeySetting> ApplicationBindings { get; set; } = [];
}

public sealed record ApplicationHotkeySetting(
    string ProcessName,
    HotkeyGesture Hotkey);
