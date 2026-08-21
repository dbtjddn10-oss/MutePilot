using MutePilot.Hotkeys;

namespace MutePilot.Settings;

public sealed class AppSettings
{
    public HotkeyGesture? MasterHotkey { get; set; }

    public List<ApplicationHotkeySetting> ApplicationBindings { get; set; } = [];
}

public sealed record ApplicationHotkeySetting(
    string ProcessName,
    HotkeyGesture Hotkey);
