using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Input;
using System.Windows.Interop;

namespace MutePilot.Hotkeys;

public sealed class HotkeyService : IHotkeyService
{
    private const int WmHotkey = 0x0312;
    private const uint ModAlt = 0x0001;
    private const uint ModControl = 0x0002;
    private const uint ModShift = 0x0004;
    private const uint ModNoRepeat = 0x4000;
    private const int FirstHotkeyId = 0x4000;

    private readonly Dictionary<string, HotkeyRegistration> _registrationsByTarget =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<int, HotkeyRegistration> _registrationsById = [];
    private HwndSource? _source;
    private nint _windowHandle;
    private int _nextHotkeyId = FirstHotkeyId;
    private bool _disposed;

    public event EventHandler<HotkeyPressedEventArgs>? HotkeyPressed;

    public void Initialize(nint windowHandle)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_source is not null)
        {
            throw new InvalidOperationException("Hotkey service is already initialized.");
        }

        if (windowHandle == nint.Zero)
        {
            throw new ArgumentException("A valid window handle is required.", nameof(windowHandle));
        }

        _source = HwndSource.FromHwnd(windowHandle) ??
            throw new InvalidOperationException("WPF window message source is unavailable.");
        _windowHandle = windowHandle;
        _source.AddHook(WindowMessageHook);
    }

    public bool TryRegisterOrReplace(HotkeyBinding binding, out string errorMessage)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_source is null)
        {
            errorMessage = "전역 단축키 서비스가 아직 준비되지 않았습니다.";
            return false;
        }

        if (!binding.Gesture.TryValidate(out errorMessage))
        {
            return false;
        }

        var duplicate = _registrationsByTarget.Values.FirstOrDefault(registration =>
            !string.Equals(
                registration.Binding.TargetId,
                binding.TargetId,
                StringComparison.OrdinalIgnoreCase) &&
            registration.Binding.Gesture == binding.Gesture);

        if (duplicate is not null)
        {
            errorMessage = "이미 MutePilot에서 사용 중인 단축키입니다.";
            return false;
        }

        _registrationsByTarget.TryGetValue(binding.TargetId, out var previousRegistration);

        if (previousRegistration?.Binding.Gesture == binding.Gesture)
        {
            _registrationsByTarget[binding.TargetId] = previousRegistration with
            {
                Binding = binding
            };
            _registrationsById[previousRegistration.Id] = _registrationsByTarget[binding.TargetId];
            errorMessage = string.Empty;
            return true;
        }

        var newId = _nextHotkeyId++;
        var virtualKey = unchecked((uint)KeyInterop.VirtualKeyFromKey(binding.Gesture.Key));
        var nativeModifiers = ToNativeModifiers(binding.Gesture.Modifiers) | ModNoRepeat;

        if (!RegisterHotKey(_windowHandle, newId, nativeModifiers, virtualKey))
        {
            var exception = new Win32Exception(Marshal.GetLastWin32Error());
            Debug.WriteLine(exception);
            errorMessage =
                "Windows에서 단축키를 등록하지 못했습니다. 다른 프로그램에서 사용 중일 수 있습니다.";
            return false;
        }

        if (previousRegistration is not null &&
            !UnregisterHotKey(_windowHandle, previousRegistration.Id))
        {
            var exception = new Win32Exception(Marshal.GetLastWin32Error());
            Debug.WriteLine(exception);
            UnregisterHotKey(_windowHandle, newId);
            errorMessage = "기존 단축키를 안전하게 해제하지 못해 변경을 취소했습니다.";
            return false;
        }

        if (previousRegistration is not null)
        {
            _registrationsById.Remove(previousRegistration.Id);
        }

        var newRegistration = new HotkeyRegistration(newId, binding);
        _registrationsByTarget[binding.TargetId] = newRegistration;
        _registrationsById[newId] = newRegistration;
        errorMessage = string.Empty;
        return true;
    }

    public bool TryUnregister(string targetId, out string errorMessage)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (!_registrationsByTarget.TryGetValue(targetId, out var registration))
        {
            errorMessage = string.Empty;
            return true;
        }

        if (!UnregisterHotKey(_windowHandle, registration.Id))
        {
            var exception = new Win32Exception(Marshal.GetLastWin32Error());
            Debug.WriteLine(exception);
            errorMessage = "Windows에서 기존 단축키를 해제하지 못했습니다.";
            return false;
        }

        _registrationsByTarget.Remove(targetId);
        _registrationsById.Remove(registration.Id);
        errorMessage = string.Empty;
        return true;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        foreach (var registration in _registrationsById.Values.ToArray())
        {
            if (!UnregisterHotKey(_windowHandle, registration.Id))
            {
                Debug.WriteLine(new Win32Exception(Marshal.GetLastWin32Error()));
            }
        }

        _registrationsByTarget.Clear();
        _registrationsById.Clear();

        if (_source is not null)
        {
            _source.RemoveHook(WindowMessageHook);
            _source = null;
        }

        _windowHandle = nint.Zero;
        _disposed = true;
    }

    private nint WindowMessageHook(
        nint hwnd,
        int message,
        nint wParam,
        nint lParam,
        ref bool handled)
    {
        if (message != WmHotkey ||
            !_registrationsById.TryGetValue(wParam.ToInt32(), out var registration))
        {
            return nint.Zero;
        }

        handled = true;
        HotkeyPressed?.Invoke(this, new HotkeyPressedEventArgs(registration.Binding));
        return nint.Zero;
    }

    private static uint ToNativeModifiers(HotkeyModifiers modifiers)
    {
        var nativeModifiers = 0U;

        if (modifiers.HasFlag(HotkeyModifiers.Alt))
        {
            nativeModifiers |= ModAlt;
        }

        if (modifiers.HasFlag(HotkeyModifiers.Control))
        {
            nativeModifiers |= ModControl;
        }

        if (modifiers.HasFlag(HotkeyModifiers.Shift))
        {
            nativeModifiers |= ModShift;
        }

        return nativeModifiers;
    }

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool RegisterHotKey(
        nint windowHandle,
        int id,
        uint modifiers,
        uint virtualKey);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnregisterHotKey(nint windowHandle, int id);

    private sealed record HotkeyRegistration(int Id, HotkeyBinding Binding);
}
