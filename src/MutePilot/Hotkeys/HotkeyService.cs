using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Input;
using System.Windows.Interop;

namespace MutePilot.Hotkeys;

public sealed class HotkeyService : IHotkeyService
{
    private const int WmInput = 0x00FF;
    private const int WmHotkey = 0x0312;
    private const uint RidInput = 0x10000003;
    private const uint RimTypeKeyboard = 1;
    private const ushort RiKeyBreak = 0x0001;
    private const ushort RiKeyE0 = 0x0002;
    private const ushort RiKeyE1 = 0x0004;
    private const uint RidevRemove = 0x00000001;
    private const uint RidevInputSink = 0x00000100;
    private const ushort GenericDesktopUsagePage = 0x01;
    private const ushort KeyboardUsage = 0x06;
    private const uint ModAlt = 0x0001;
    private const uint ModControl = 0x0002;
    private const uint ModShift = 0x0004;
    private const uint ModNoRepeat = 0x4000;
    private const int FirstHotkeyId = 0x4000;

    private readonly Dictionary<string, HotkeyRegistration> _registrationsByTarget =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<int, HotkeyRegistration> _registrationsById = [];
    private readonly Dictionary<uint, HotkeyRegistration> _rawRegistrationsByVirtualKey = [];
    private readonly HashSet<uint> _pressedStandaloneKeys = [];
    private readonly HashSet<int> _pressedModifierKeys = [];
    private HwndSource? _source;
    private nint _windowHandle;
    private int _nextHotkeyId = FirstHotkeyId;
    private bool _rawInputRegistered;
    private bool _disposed;

    public event EventHandler<HotkeyPressedEventArgs>? HotkeyPressed;

    public bool IsRawInputAvailable => _rawInputRegistered;

    public string? InitializationWarning { get; private set; }

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

        _rawInputRegistered = TryRegisterRawKeyboard(windowHandle);

        if (!_rawInputRegistered)
        {
            InitializationWarning =
                "Raw Input을 시작하지 못해 단독 F1~F11 단축키를 사용할 수 없습니다. modifier 조합은 계속 사용할 수 있습니다.";
        }
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
            UpdateRegistrationBinding(previousRegistration, binding);
            errorMessage = string.Empty;
            return true;
        }

        if (binding.Gesture.IsStandaloneFunctionKey)
        {
            return TryRegisterRawBinding(binding, previousRegistration, out errorMessage);
        }

        return TryRegisterNativeBinding(binding, previousRegistration, out errorMessage);
    }

    public bool TryUnregister(string targetId, out string errorMessage)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (!_registrationsByTarget.TryGetValue(targetId, out var registration))
        {
            errorMessage = string.Empty;
            return true;
        }

        if (registration.NativeId is int nativeId &&
            !UnregisterHotKey(_windowHandle, nativeId))
        {
            Debug.WriteLine(new Win32Exception(Marshal.GetLastWin32Error()));
            errorMessage = "Windows에서 기존 단축키를 해제하지 못했습니다.";
            return false;
        }

        RemoveRegistrationMaps(registration);
        errorMessage = string.Empty;
        return true;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        foreach (var nativeId in _registrationsById.Keys.ToArray())
        {
            if (!UnregisterHotKey(_windowHandle, nativeId))
            {
                Debug.WriteLine(new Win32Exception(Marshal.GetLastWin32Error()));
            }
        }

        _registrationsByTarget.Clear();
        _registrationsById.Clear();
        _rawRegistrationsByVirtualKey.Clear();
        _pressedStandaloneKeys.Clear();
        _pressedModifierKeys.Clear();

        if (_rawInputRegistered)
        {
            TryRemoveRawKeyboardRegistration();
            _rawInputRegistered = false;
        }

        if (_source is not null)
        {
            _source.RemoveHook(WindowMessageHook);
            _source = null;
        }

        _windowHandle = nint.Zero;
        _disposed = true;
    }

    private bool TryRegisterRawBinding(
        HotkeyBinding binding,
        HotkeyRegistration? previousRegistration,
        out string errorMessage)
    {
        if (!_rawInputRegistered)
        {
            errorMessage = "Raw Input을 사용할 수 없어 단독 F키를 등록하지 못했습니다.";
            return false;
        }

        if (previousRegistration?.NativeId is int previousNativeId &&
            !UnregisterHotKey(_windowHandle, previousNativeId))
        {
            Debug.WriteLine(new Win32Exception(Marshal.GetLastWin32Error()));
            errorMessage = "기존 단축키를 안전하게 해제하지 못해 변경을 취소했습니다.";
            return false;
        }

        if (previousRegistration is not null)
        {
            RemoveRegistrationMaps(previousRegistration);
        }

        var virtualKey = unchecked((uint)KeyInterop.VirtualKeyFromKey(binding.Gesture.Key));
        var registration = new HotkeyRegistration(null, binding);
        _registrationsByTarget[binding.TargetId] = registration;
        _rawRegistrationsByVirtualKey[virtualKey] = registration;
        _pressedStandaloneKeys.Remove(virtualKey);
        errorMessage = string.Empty;
        return true;
    }

    private bool TryRegisterNativeBinding(
        HotkeyBinding binding,
        HotkeyRegistration? previousRegistration,
        out string errorMessage)
    {
        var newId = _nextHotkeyId++;
        var virtualKey = unchecked((uint)KeyInterop.VirtualKeyFromKey(binding.Gesture.Key));
        var nativeModifiers = ToNativeModifiers(binding.Gesture.Modifiers) | ModNoRepeat;

        if (!RegisterHotKey(_windowHandle, newId, nativeModifiers, virtualKey))
        {
            Debug.WriteLine(new Win32Exception(Marshal.GetLastWin32Error()));
            errorMessage =
                "Windows에서 단축키를 등록하지 못했습니다. 다른 프로그램에서 사용 중일 수 있습니다.";
            return false;
        }

        if (previousRegistration?.NativeId is int previousNativeId &&
            !UnregisterHotKey(_windowHandle, previousNativeId))
        {
            Debug.WriteLine(new Win32Exception(Marshal.GetLastWin32Error()));
            UnregisterHotKey(_windowHandle, newId);
            errorMessage = "기존 단축키를 안전하게 해제하지 못해 변경을 취소했습니다.";
            return false;
        }

        if (previousRegistration is not null)
        {
            RemoveRegistrationMaps(previousRegistration);
        }

        var registration = new HotkeyRegistration(newId, binding);
        _registrationsByTarget[binding.TargetId] = registration;
        _registrationsById[newId] = registration;
        errorMessage = string.Empty;
        return true;
    }

    private void UpdateRegistrationBinding(
        HotkeyRegistration previousRegistration,
        HotkeyBinding binding)
    {
        var updatedRegistration = previousRegistration with { Binding = binding };
        _registrationsByTarget[binding.TargetId] = updatedRegistration;

        if (updatedRegistration.NativeId is int nativeId)
        {
            _registrationsById[nativeId] = updatedRegistration;
        }
        else
        {
            var virtualKey = unchecked((uint)KeyInterop.VirtualKeyFromKey(binding.Gesture.Key));
            _rawRegistrationsByVirtualKey[virtualKey] = updatedRegistration;
        }
    }

    private void RemoveRegistrationMaps(HotkeyRegistration registration)
    {
        _registrationsByTarget.Remove(registration.Binding.TargetId);

        if (registration.NativeId is int nativeId)
        {
            _registrationsById.Remove(nativeId);
            return;
        }

        var virtualKey = unchecked(
            (uint)KeyInterop.VirtualKeyFromKey(registration.Binding.Gesture.Key));
        _rawRegistrationsByVirtualKey.Remove(virtualKey);
        _pressedStandaloneKeys.Remove(virtualKey);

        if (_rawRegistrationsByVirtualKey.Count == 0)
        {
            _pressedModifierKeys.Clear();
        }
    }

    private nint WindowMessageHook(
        nint hwnd,
        int message,
        nint wParam,
        nint lParam,
        ref bool handled)
    {
        if (message == WmHotkey &&
            _registrationsById.TryGetValue(wParam.ToInt32(), out var registration))
        {
            handled = true;
            HotkeyPressed?.Invoke(this, new HotkeyPressedEventArgs(registration.Binding));
        }
        else if (message == WmInput && _rawInputRegistered)
        {
            ProcessRawKeyboardInput(lParam);
        }

        return nint.Zero;
    }

    private void ProcessRawKeyboardInput(nint rawInputHandle)
    {
        var headerSize = unchecked((uint)Marshal.SizeOf<RawInputHeader>());
        var minimumInputSize = headerSize + unchecked((uint)Marshal.SizeOf<RawKeyboard>());
        var dataSize = 0U;

        if (GetRawInputData(
                rawInputHandle,
                RidInput,
                nint.Zero,
                ref dataSize,
                headerSize) != 0 ||
            dataSize < minimumInputSize ||
            dataSize > int.MaxValue)
        {
            return;
        }

        var buffer = Marshal.AllocHGlobal(unchecked((int)dataSize));

        try
        {
            var copiedSize = dataSize;
            var result = GetRawInputData(
                rawInputHandle,
                RidInput,
                buffer,
                ref copiedSize,
                headerSize);

            if (result == uint.MaxValue || result < minimumInputSize)
            {
                return;
            }

            var header = Marshal.PtrToStructure<RawInputHeader>(buffer);

            if (header.Type != RimTypeKeyboard)
            {
                return;
            }

            var keyboardPointer = nint.Add(buffer, unchecked((int)headerSize));
            var keyboard = Marshal.PtrToStructure<RawKeyboard>(keyboardPointer);
            HandleRawKeyboardEvent(keyboard);
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    private void HandleRawKeyboardEvent(RawKeyboard keyboard)
    {
        if (_rawRegistrationsByVirtualKey.Count == 0 || keyboard.VirtualKey == byte.MaxValue)
        {
            return;
        }

        var virtualKey = unchecked((uint)keyboard.VirtualKey);
        var isKeyUp = (keyboard.Flags & RiKeyBreak) != 0;

        if (IsModifierVirtualKey(keyboard.VirtualKey))
        {
            var modifierToken = CreateModifierToken(keyboard);

            if (isKeyUp)
            {
                _pressedModifierKeys.Remove(modifierToken);
            }
            else
            {
                _pressedModifierKeys.Add(modifierToken);
            }

            return;
        }

        if (!_rawRegistrationsByVirtualKey.TryGetValue(virtualKey, out var registration))
        {
            return;
        }

        if (isKeyUp)
        {
            _pressedStandaloneKeys.Remove(virtualKey);
            return;
        }

        if (!_pressedStandaloneKeys.Add(virtualKey) || _pressedModifierKeys.Count > 0)
        {
            return;
        }

        HotkeyPressed?.Invoke(this, new HotkeyPressedEventArgs(registration.Binding));
    }

    private bool TryRegisterRawKeyboard(nint windowHandle)
    {
        var devices = new[]
        {
            new RawInputDevice
            {
                UsagePage = GenericDesktopUsagePage,
                Usage = KeyboardUsage,
                Flags = RidevInputSink,
                TargetWindow = windowHandle
            }
        };

        if (RegisterRawInputDevices(
                devices,
                unchecked((uint)devices.Length),
                unchecked((uint)Marshal.SizeOf<RawInputDevice>())))
        {
            return true;
        }

        Debug.WriteLine(new Win32Exception(Marshal.GetLastWin32Error()));
        return false;
    }

    private void TryRemoveRawKeyboardRegistration()
    {
        var devices = new[]
        {
            new RawInputDevice
            {
                UsagePage = GenericDesktopUsagePage,
                Usage = KeyboardUsage,
                Flags = RidevRemove,
                TargetWindow = nint.Zero
            }
        };

        if (!RegisterRawInputDevices(
                devices,
                unchecked((uint)devices.Length),
                unchecked((uint)Marshal.SizeOf<RawInputDevice>())))
        {
            Debug.WriteLine(new Win32Exception(Marshal.GetLastWin32Error()));
        }
    }

    private static int CreateModifierToken(RawKeyboard keyboard)
    {
        var extendedFlags = keyboard.Flags & (RiKeyE0 | RiKeyE1);
        return (keyboard.VirtualKey << 16) | (keyboard.MakeCode << 4) | extendedFlags;
    }

    private static bool IsModifierVirtualKey(ushort virtualKey)
    {
        return virtualKey is
            0x10 or 0x11 or 0x12 or
            0x5B or 0x5C or
            0xA0 or 0xA1 or 0xA2 or 0xA3 or 0xA4 or 0xA5;
    }

    private static uint ToNativeModifiers(HotkeyModifiers modifiers)
    {
        var nativeModifiers = 0U;

        if (modifiers.HasFlag(HotkeyModifiers.Alt)) nativeModifiers |= ModAlt;
        if (modifiers.HasFlag(HotkeyModifiers.Control)) nativeModifiers |= ModControl;
        if (modifiers.HasFlag(HotkeyModifiers.Shift)) nativeModifiers |= ModShift;

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

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool RegisterRawInputDevices(
        [In] RawInputDevice[] rawInputDevices,
        uint deviceCount,
        uint rawInputDeviceSize);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint GetRawInputData(
        nint rawInput,
        uint command,
        nint data,
        ref uint size,
        uint headerSize);

    [StructLayout(LayoutKind.Sequential)]
    private struct RawInputDevice
    {
        public ushort UsagePage;
        public ushort Usage;
        public uint Flags;
        public nint TargetWindow;
    }

    [StructLayout(LayoutKind.Sequential)]
    private readonly struct RawInputHeader
    {
        public readonly uint Type;
        public readonly uint Size;
        public readonly nint Device;
        public readonly nint WParam;
    }

    [StructLayout(LayoutKind.Sequential)]
    private readonly struct RawKeyboard
    {
        public readonly ushort MakeCode;
        public readonly ushort Flags;
        public readonly ushort Reserved;
        public readonly ushort VirtualKey;
        public readonly uint Message;
        public readonly uint ExtraInformation;
    }

    private sealed record HotkeyRegistration(int? NativeId, HotkeyBinding Binding);
}
