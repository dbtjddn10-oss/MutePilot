using System.Drawing;
using System.Windows.Forms;

namespace MutePilot.Tray;

public sealed class TrayService : ITrayService
{
    private readonly NotifyIcon _notifyIcon;
    private readonly ContextMenuStrip _contextMenu;
    private readonly ToolStripMenuItem _openMenuItem;
    private readonly ToolStripMenuItem _overlayMenuItem;
    private readonly ToolStripMenuItem _exitMenuItem;
    private readonly Icon _icon;
    private bool _noticeShown;
    private bool _disposed;

    public TrayService()
    {
        _openMenuItem = new ToolStripMenuItem("MutePilot 열기");
        _overlayMenuItem = new ToolStripMenuItem("오버레이 끄기");
        _exitMenuItem = new ToolStripMenuItem("종료");
        _contextMenu = new ContextMenuStrip();
        _contextMenu.Items.AddRange([
            _openMenuItem,
            _overlayMenuItem,
            new ToolStripSeparator(),
            _exitMenuItem
        ]);

        _icon = (Icon)SystemIcons.Application.Clone();
        _notifyIcon = new NotifyIcon
        {
            ContextMenuStrip = _contextMenu,
            Icon = _icon,
            Text = "MutePilot",
            Visible = true
        };

        _openMenuItem.Click += OpenMenuItem_Click;
        _overlayMenuItem.Click += OverlayMenuItem_Click;
        _exitMenuItem.Click += ExitMenuItem_Click;
        _notifyIcon.DoubleClick += NotifyIcon_DoubleClick;
    }

    public event EventHandler? OpenRequested;

    public event EventHandler? OverlayToggleRequested;

    public event EventHandler? ExitRequested;

    public void SetOverlayEnabled(bool isEnabled)
    {
        if (_disposed)
        {
            return;
        }

        _overlayMenuItem.Text = isEnabled ? "오버레이 끄기" : "오버레이 켜기";
    }

    public void ShowRunningInBackgroundNotice()
    {
        if (_disposed || _noticeShown)
        {
            return;
        }

        _noticeShown = true;
        _notifyIcon.BalloonTipTitle = "MutePilot";
        _notifyIcon.BalloonTipText = "MutePilot이 트레이에서 계속 실행 중입니다.";
        _notifyIcon.BalloonTipIcon = ToolTipIcon.Info;
        _notifyIcon.ShowBalloonTip(2500);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _notifyIcon.DoubleClick -= NotifyIcon_DoubleClick;
        _openMenuItem.Click -= OpenMenuItem_Click;
        _overlayMenuItem.Click -= OverlayMenuItem_Click;
        _exitMenuItem.Click -= ExitMenuItem_Click;
        _notifyIcon.Visible = false;
        _notifyIcon.ContextMenuStrip = null;
        _notifyIcon.Dispose();
        _contextMenu.Dispose();
        _icon.Dispose();
    }

    private void OpenMenuItem_Click(object? sender, EventArgs e) =>
        OpenRequested?.Invoke(this, EventArgs.Empty);

    private void OverlayMenuItem_Click(object? sender, EventArgs e) =>
        OverlayToggleRequested?.Invoke(this, EventArgs.Empty);

    private void ExitMenuItem_Click(object? sender, EventArgs e) =>
        ExitRequested?.Invoke(this, EventArgs.Empty);

    private void NotifyIcon_DoubleClick(object? sender, EventArgs e) =>
        OpenRequested?.Invoke(this, EventArgs.Empty);
}
