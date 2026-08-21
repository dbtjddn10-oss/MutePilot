using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

namespace MutePilot.Overlay;

public partial class MuteOverlayWindow : Window
{
    private const int GwlExStyle = -20;
    private const int WsExTransparent = 0x00000020;
    private const int WsExToolWindow = 0x00000080;
    private const int WsExNoActivate = 0x08000000;
    private const int MaximumApplicationRows = 7;
    private const double WorkAreaMargin = 12;

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

    public void UpdateTargets(IReadOnlyList<OverlayTargetState> targets)
    {
        var master = targets.Take(1);
        var applications = targets.Skip(1).Take(MaximumApplicationRows);
        var rows = master.Concat(applications)
            .Select(CreateRow)
            .ToList();
        var hiddenCount = Math.Max(0, targets.Count - rows.Count);

        if (hiddenCount > 0)
        {
            rows.Add(new OverlayTargetRow(
                $"외 {hiddenCount}개",
                string.Empty,
                Brushes.Gray,
                0.65));
        }

        TargetItemsControl.ItemsSource = rows;
    }

    private static OverlayTargetRow CreateRow(OverlayTargetState target)
    {
        return target.Status switch
        {
            OverlayTargetStatus.Muted => new OverlayTargetRow(
                target.DisplayName, "🔇 음소거", Brushes.LightCoral, 1),
            OverlayTargetStatus.Unmuted => new OverlayTargetRow(
                target.DisplayName, "🔊 음소거 해제", Brushes.LightGreen, 1),
            OverlayTargetStatus.Mixed => new OverlayTargetRow(
                target.DisplayName, "일부 음소거", Brushes.Khaki, 1),
            OverlayTargetStatus.NotRunning => new OverlayTargetRow(
                target.DisplayName, "실행 안 됨", Brushes.Gray, 0.68),
            _ => new OverlayTargetRow(
                target.DisplayName, "확인 중", Brushes.Gray, 0.68)
        };
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

    private sealed record OverlayTargetRow(
        string Name,
        string StatusText,
        Brush StatusBrush,
        double Opacity);
}
