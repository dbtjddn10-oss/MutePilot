namespace MutePilot.Hotkeys;

public enum HotkeyTargetType
{
    MasterAudio,
    Application
}

public sealed record HotkeyBinding(
    string TargetId,
    HotkeyTargetType TargetType,
    string? ProcessName,
    HotkeyGesture Gesture)
{
    public const string MasterTargetId = "master";

    public static HotkeyBinding ForMasterAudio(HotkeyGesture gesture)
    {
        return new HotkeyBinding(
            MasterTargetId,
            HotkeyTargetType.MasterAudio,
            null,
            gesture);
    }

    public static HotkeyBinding ForApplication(string processName, HotkeyGesture gesture)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(processName);

        return new HotkeyBinding(
            GetApplicationTargetId(processName),
            HotkeyTargetType.Application,
            processName,
            gesture);
    }

    public static string GetApplicationTargetId(string processName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(processName);
        return $"application:{processName.Trim().ToUpperInvariant()}";
    }
}

public sealed class HotkeyPressedEventArgs(HotkeyBinding binding) : EventArgs
{
    public HotkeyBinding Binding { get; } = binding;
}
