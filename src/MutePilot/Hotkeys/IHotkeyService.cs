namespace MutePilot.Hotkeys;

public interface IHotkeyService : IDisposable
{
    event EventHandler<HotkeyPressedEventArgs>? HotkeyPressed;

    bool IsRawInputAvailable { get; }

    string? InitializationWarning { get; }

    void Initialize(nint windowHandle);

    bool TryRegisterOrReplace(HotkeyBinding binding, out string errorMessage);

    bool TryUnregister(string targetId, out string errorMessage);
}
