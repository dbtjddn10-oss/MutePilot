using System.Runtime.InteropServices;

namespace MutePilot.Overlay;

public interface IFullscreenStateDetector
{
    bool IsForegroundWindowFullscreen();
}

public sealed class FullscreenStateDetector : IFullscreenStateDetector
{
    private const uint MonitorDefaultToNearest = 0x00000002;
    private const int EdgeTolerancePixels = 3;

    public bool IsForegroundWindowFullscreen()
    {
        var foregroundWindow = GetForegroundWindow();

        if (foregroundWindow == nint.Zero ||
            !IsWindowVisible(foregroundWindow) ||
            IsIconic(foregroundWindow))
        {
            return false;
        }

        GetWindowThreadProcessId(foregroundWindow, out var processId);

        if (processId == Environment.ProcessId)
        {
            return false;
        }

        var monitor = MonitorFromWindow(foregroundWindow, MonitorDefaultToNearest);
        var monitorInfo = new MonitorInfo { Size = Marshal.SizeOf<MonitorInfo>() };

        if (monitor == nint.Zero ||
            !GetWindowRect(foregroundWindow, out var windowRect) ||
            !GetMonitorInfo(monitor, ref monitorInfo))
        {
            return false;
        }

        var monitorRect = monitorInfo.Monitor;
        return windowRect.Left <= monitorRect.Left + EdgeTolerancePixels &&
               windowRect.Top <= monitorRect.Top + EdgeTolerancePixels &&
               windowRect.Right >= monitorRect.Right - EdgeTolerancePixels &&
               windowRect.Bottom >= monitorRect.Bottom - EdgeTolerancePixels;
    }

    [DllImport("user32.dll")]
    private static extern nint GetForegroundWindow();

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetWindowRect(nint windowHandle, out NativeRect rectangle);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsIconic(nint windowHandle);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindowVisible(nint windowHandle);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(nint windowHandle, out int processId);

    [DllImport("user32.dll")]
    private static extern nint MonitorFromWindow(nint windowHandle, uint flags);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetMonitorInfo(nint monitor, ref MonitorInfo monitorInfo);

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct MonitorInfo
    {
        public int Size;
        public NativeRect Monitor;
        public NativeRect WorkArea;
        public uint Flags;
    }
}
