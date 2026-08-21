using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace MutePilot.Overlay;

public partial class MuteOverlayWindow : Window
{
    private const int GwlExStyle = -20;
    private const int WsExTransparent = 0x00000020;
    private const int WsExToolWindow = 0x00000080;
    private const int WsExNoActivate = 0x08000000;
    private const double WorkAreaMargin = 20;

    public MuteOverlayWindow()
    {
        InitializeComponent();
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);

        var handle = new WindowInteropHelper(this).Handle;
        var extendedStyle = GetWindowLongPtr(handle, GwlExStyle).ToInt64();
        extendedStyle |= WsExTransparent | WsExToolWindow | WsExNoActivate;
        SetWindowLongPtr(handle, GwlExStyle, new nint(extendedStyle));
    }

    public void UpdateState(string targetName, bool isMuted)
    {
        TargetNameText.Text = targetName;
        StateIconText.Text = isMuted ? "🔇" : "🔊";
        StateText.Text = isMuted ? "음소거" : "음소거 해제";
    }

    public void PositionNearPrimaryWorkAreaTopRight()
    {
        var workArea = SystemParameters.WorkArea;
        Left = workArea.Right - Width - WorkAreaMargin;
        Top = workArea.Top + WorkAreaMargin;
    }

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW", SetLastError = true)]
    private static extern nint GetWindowLongPtr64(nint windowHandle, int index);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongW", SetLastError = true)]
    private static extern int GetWindowLong32(nint windowHandle, int index);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW", SetLastError = true)]
    private static extern nint SetWindowLongPtr64(nint windowHandle, int index, nint newValue);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongW", SetLastError = true)]
    private static extern int SetWindowLong32(nint windowHandle, int index, int newValue);

    private static nint GetWindowLongPtr(nint windowHandle, int index)
    {
        return nint.Size == 8
            ? GetWindowLongPtr64(windowHandle, index)
            : new nint(GetWindowLong32(windowHandle, index));
    }

    private static void SetWindowLongPtr(nint windowHandle, int index, nint newValue)
    {
        if (nint.Size == 8)
        {
            SetWindowLongPtr64(windowHandle, index, newValue);
        }
        else
        {
            SetWindowLong32(windowHandle, index, newValue.ToInt32());
        }
    }
}
