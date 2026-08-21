namespace MutePilot.Hotkeys;

public enum HotkeyTargetType
{
    MasterAudio,
    Application
}

public enum HotkeyActionType
{
    ToggleMute,
    SetVolumePreset
}

public sealed record HotkeyBinding(
    string BindingId,
    string TargetId,
    HotkeyTargetType TargetType,
    HotkeyActionType ActionType,
    string? ProcessName,
    HotkeyGesture Gesture)
{
    public const string MasterTargetId = "master";
    public const string MasterMuteBindingId = "master:mute";
    public const string MasterVolumeBindingId = "master:volume";

    public static HotkeyBinding ForMasterMute(HotkeyGesture gesture)
    {
        return new HotkeyBinding(
            MasterMuteBindingId,
            MasterTargetId,
            HotkeyTargetType.MasterAudio,
            HotkeyActionType.ToggleMute,
            null,
            gesture);
    }

    public static HotkeyBinding ForMasterVolume(HotkeyGesture gesture)
    {
        return new HotkeyBinding(
            MasterVolumeBindingId,
            MasterTargetId,
            HotkeyTargetType.MasterAudio,
            HotkeyActionType.SetVolumePreset,
            null,
            gesture);
    }

    public static HotkeyBinding ForApplicationMute(string processName, HotkeyGesture gesture)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(processName);

        return new HotkeyBinding(
            GetApplicationMuteBindingId(processName),
            GetApplicationTargetId(processName),
            HotkeyTargetType.Application,
            HotkeyActionType.ToggleMute,
            processName,
            gesture);
    }

    public static HotkeyBinding ForApplicationVolume(string processName, HotkeyGesture gesture)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(processName);

        return new HotkeyBinding(
            GetApplicationVolumeBindingId(processName),
            GetApplicationTargetId(processName),
            HotkeyTargetType.Application,
            HotkeyActionType.SetVolumePreset,
            processName,
            gesture);
    }

    public static string GetApplicationTargetId(string processName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(processName);
        return $"application:{processName.Trim().ToUpperInvariant()}";
    }

    public static string GetApplicationMuteBindingId(string processName) =>
        $"{GetApplicationTargetId(processName)}:mute";

    public static string GetApplicationVolumeBindingId(string processName) =>
        $"{GetApplicationTargetId(processName)}:volume";
}

public sealed class HotkeyPressedEventArgs(HotkeyBinding binding) : EventArgs
{
    public HotkeyBinding Binding { get; } = binding;
}
