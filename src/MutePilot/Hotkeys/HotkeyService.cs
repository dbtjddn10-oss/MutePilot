using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Interop;

namespace MutePilot.Hotkeys;

public sealed class HotkeyService : IHotkeyService
{
    private const int WmHotkey = 0x0312;
    private const int StandalonePollingIntervalMilliseconds = 15;
    private const short KeyDownMask = unchecked((short)0x8000);
    private const uint ModAlt = 0x0001;
    private const uint ModControl = 0x0002;
    private const uint ModShift = 0x0004;
    private const uint ModWindows = 0x0008;
    private const uint ModNoRepeat = 0x4000;
    private const int FirstHotkeyId = 0x4000;

    private readonly object _syncRoot = new();
    private readonly Dictionary<string, HotkeyRegistration> _registrationsByBinding =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<int, HotkeyRegistration> _registrationsById = [];
    private readonly Dictionary<int, HotkeyRegistration> _standaloneRegistrationsByVirtualKey = [];
    private readonly HashSet<int> _pressedStandaloneKeys = [];
    private HwndSource? _source;
    private CancellationTokenSource? _pollingCancellation;
    private Task? _pollingTask;
    private nint _windowHandle;
    private int _nextHotkeyId = FirstHotkeyId;
    private bool _disposed;

    public event EventHandler<HotkeyPressedEventArgs>? HotkeyPressed;

    public bool IsStandalonePollingAvailable { get; private set; }

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

        try
        {
            _pollingCancellation = new CancellationTokenSource();
            _pollingTask = Task.Run(
                () => PollStandaloneKeysAsync(_pollingCancellation.Token),
                _pollingCancellation.Token);
            IsStandalonePollingAvailable = true;
        }
        catch (Exception exception)
        {
            Debug.WriteLine(exception);
            InitializationWarning =
                "사용자 지정 단독 키 감시를 시작하지 못했습니다. modifier 조합은 계속 사용할 수 있습니다.";
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

        lock (_syncRoot)
        {
            var duplicate = _registrationsByBinding.Values.FirstOrDefault(registration =>
                !string.Equals(
                    registration.Binding.BindingId,
                    binding.BindingId,
                    StringComparison.OrdinalIgnoreCase) &&
                registration.Binding.Gesture == binding.Gesture);

            if (duplicate is not null)
            {
                errorMessage = "이미 MutePilot에서 사용 중인 단축키입니다.";
                return false;
            }

            _registrationsByBinding.TryGetValue(binding.BindingId, out var previousRegistration);

            if (previousRegistration?.Binding.Gesture == binding.Gesture)
            {
                UpdateRegistrationBinding(previousRegistration, binding);
                errorMessage = string.Empty;
                return true;
            }

            if (binding.Gesture.IsStandalone)
            {
                return TryRegisterStandaloneBinding(
                    binding,
                    previousRegistration,
                    out errorMessage);
            }

            return TryRegisterNativeBinding(binding, previousRegistration, out errorMessage);
        }
    }

    public bool TryUnregister(string bindingId, out string errorMessage)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        lock (_syncRoot)
        {
            if (!_registrationsByBinding.TryGetValue(bindingId, out var registration))
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
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        IsStandalonePollingAvailable = false;
        _pollingCancellation?.Cancel();

        if (_pollingTask is not null)
        {
            try
            {
                _pollingTask.Wait(TimeSpan.FromSeconds(1));
            }
            catch (AggregateException exception) when (
                exception.InnerExceptions.All(inner => inner is TaskCanceledException))
            {
                Debug.WriteLine(exception);
            }
        }

        _pollingCancellation?.Dispose();
        _pollingCancellation = null;
        _pollingTask = null;

        lock (_syncRoot)
        {
            foreach (var nativeId in _registrationsById.Keys.ToArray())
            {
                if (!UnregisterHotKey(_windowHandle, nativeId))
                {
                    Debug.WriteLine(new Win32Exception(Marshal.GetLastWin32Error()));
                }
            }

            _registrationsByBinding.Clear();
            _registrationsById.Clear();
            _standaloneRegistrationsByVirtualKey.Clear();
            _pressedStandaloneKeys.Clear();
        }

        if (_source is not null)
        {
            _source.RemoveHook(WindowMessageHook);
            _source = null;
        }

        _windowHandle = nint.Zero;
    }

    private bool TryRegisterStandaloneBinding(
        HotkeyBinding binding,
        HotkeyRegistration? previousRegistration,
        out string errorMessage)
    {
        if (!IsStandalonePollingAvailable)
        {
            errorMessage = "사용자 지정 단독 키 감시를 사용할 수 없어 단축키를 등록하지 못했습니다.";
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

        var virtualKey = binding.Gesture.VirtualKey;
        var registration = new HotkeyRegistration(null, binding);
        _registrationsByBinding[binding.BindingId] = registration;
        _standaloneRegistrationsByVirtualKey[virtualKey] = registration;

        if (IsKeyDown(virtualKey))
        {
            _pressedStandaloneKeys.Add(virtualKey);
        }
        else
        {
            _pressedStandaloneKeys.Remove(virtualKey);
        }

        errorMessage = string.Empty;
        return true;
    }

    private bool TryRegisterNativeBinding(
        HotkeyBinding binding,
        HotkeyRegistration? previousRegistration,
        out string errorMessage)
    {
        var newId = _nextHotkeyId++;
        var virtualKey = unchecked((uint)binding.Gesture.VirtualKey);
        var nativeModifiers = ToNativeModifiers(binding.Gesture.Modifiers) | ModNoRepeat;

        if (!RegisterHotKey(_windowHandle, newId, nativeModifiers, virtualKey))
        {
            Debug.WriteLine(new Win32Exception(Marshal.GetLastWin32Error()));
            errorMessage =
                "이 단축키는 Windows 또는 다른 프로그램에서 사용 중이라 등록할 수 없습니다.";
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
        _registrationsByBinding[binding.BindingId] = registration;
        _registrationsById[newId] = registration;
        errorMessage = string.Empty;
        return true;
    }

    private void UpdateRegistrationBinding(
        HotkeyRegistration previousRegistration,
        HotkeyBinding binding)
    {
        var updatedRegistration = previousRegistration with { Binding = binding };
        _registrationsByBinding[binding.BindingId] = updatedRegistration;

        if (updatedRegistration.NativeId is int nativeId)
        {
            _registrationsById[nativeId] = updatedRegistration;
        }
        else
        {
            var virtualKey = binding.Gesture.VirtualKey;
            _standaloneRegistrationsByVirtualKey[virtualKey] = updatedRegistration;
        }
    }

    private void RemoveRegistrationMaps(HotkeyRegistration registration)
    {
        _registrationsByBinding.Remove(registration.Binding.BindingId);

        if (registration.NativeId is int nativeId)
        {
            _registrationsById.Remove(nativeId);
            return;
        }

        var virtualKey = registration.Binding.Gesture.VirtualKey;
        _standaloneRegistrationsByVirtualKey.Remove(virtualKey);
        _pressedStandaloneKeys.Remove(virtualKey);
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

    private async Task PollStandaloneKeysAsync(CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(
            TimeSpan.FromMilliseconds(StandalonePollingIntervalMilliseconds));

        try
        {
            while (await timer.WaitForNextTickAsync(cancellationToken).ConfigureAwait(false))
            {
                PollStandaloneKeys();
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Normal application shutdown.
        }
        catch (Exception exception)
        {
            Debug.WriteLine(exception);
        }
    }

    private void PollStandaloneKeys()
    {
        List<HotkeyBinding>? triggeredBindings = null;

        lock (_syncRoot)
        {
            if (_disposed)
            {
                return;
            }

            foreach (var pair in _standaloneRegistrationsByVirtualKey)
            {
                var virtualKey = pair.Key;
                var isDown = IsKeyDown(virtualKey);

                if (UpdatePressedState(_pressedStandaloneKeys, virtualKey, isDown))
                {
                    triggeredBindings ??= [];
                    triggeredBindings.Add(pair.Value.Binding);
                }
            }
        }

        if (triggeredBindings is null)
        {
            return;
        }

        foreach (var binding in triggeredBindings)
        {
            if (!IsBindingStillRegistered(binding))
            {
                continue;
            }

            try
            {
                HotkeyPressed?.Invoke(this, new HotkeyPressedEventArgs(binding));
            }
            catch (Exception exception)
            {
                Debug.WriteLine(exception);
            }
        }
    }

    private bool IsBindingStillRegistered(HotkeyBinding binding)
    {
        lock (_syncRoot)
        {
            return !_disposed &&
                _registrationsByBinding.TryGetValue(binding.BindingId, out var registration) &&
                registration.Binding == binding;
        }
    }

    private static bool IsKeyDown(int virtualKey)
    {
        return (GetAsyncKeyState(virtualKey) & KeyDownMask) != 0;
    }

    private static bool UpdatePressedState(
        HashSet<int> pressedKeys,
        int virtualKey,
        bool isDown)
    {
        if (isDown)
        {
            return pressedKeys.Add(virtualKey);
        }

        pressedKeys.Remove(virtualKey);
        return false;
    }

    private static uint ToNativeModifiers(HotkeyModifiers modifiers)
    {
        var nativeModifiers = 0U;

        if (modifiers.HasFlag(HotkeyModifiers.Alt)) nativeModifiers |= ModAlt;
        if (modifiers.HasFlag(HotkeyModifiers.Control)) nativeModifiers |= ModControl;
        if (modifiers.HasFlag(HotkeyModifiers.Shift)) nativeModifiers |= ModShift;
        if (modifiers.HasFlag(HotkeyModifiers.Windows)) nativeModifiers |= ModWindows;

        return nativeModifiers;
    }

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int virtualKey);

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

    private sealed record HotkeyRegistration(int? NativeId, HotkeyBinding Binding);
}
