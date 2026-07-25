using DrawingIcon = System.Drawing.Icon;
using Forms = System.Windows.Forms;
using WpfApplication = System.Windows.Application;

namespace Honey.Desktop.Tray;

public sealed class TrayIconService : IDisposable
{
    private readonly Forms.NotifyIcon _notifyIcon;
    private readonly Forms.ToolStripMenuItem _pausedItem;
    private readonly Forms.ToolStripMenuItem _focusItem;
    private readonly DrawingIcon _icon;
    private bool _disposed;

    public TrayIconService()
    {
        _icon = LoadIcon();
        var menu = new Forms.ContextMenuStrip();
        var visibilityItem = new Forms.ToolStripMenuItem("显示/隐藏");
        _pausedItem = new Forms.ToolStripMenuItem("暂停自主行为") { CheckOnClick = true };
        _focusItem = new Forms.ToolStripMenuItem("专注模式") { CheckOnClick = true };
        var settingsItem = new Forms.ToolStripMenuItem("设置");
        var exitItem = new Forms.ToolStripMenuItem("退出");
        menu.Items.AddRange(
        [
            visibilityItem,
            _pausedItem,
            _focusItem,
            settingsItem,
            new Forms.ToolStripSeparator(),
            exitItem
        ]);

        visibilityItem.Click += (_, _) => VisibilityToggleRequested?.Invoke(this, EventArgs.Empty);
        _pausedItem.CheckedChanged += (_, _) => PauseChanged?.Invoke(this, _pausedItem.Checked);
        _focusItem.CheckedChanged += (_, _) => FocusModeChanged?.Invoke(this, _focusItem.Checked);
        settingsItem.Click += (_, _) => SettingsRequested?.Invoke(this, EventArgs.Empty);
        exitItem.Click += (_, _) => ExitRequested?.Invoke(this, EventArgs.Empty);

        _notifyIcon = new Forms.NotifyIcon
        {
            Icon = _icon,
            Text = "Honey 白玉蜘蛛",
            ContextMenuStrip = menu,
            Visible = true
        };
        _notifyIcon.DoubleClick += (_, _) => VisibilityToggleRequested?.Invoke(this, EventArgs.Empty);
    }

    public event EventHandler? VisibilityToggleRequested;
    public event Action<object?, bool>? PauseChanged;
    public event Action<object?, bool>? FocusModeChanged;
    public event EventHandler? SettingsRequested;
    public event EventHandler? ExitRequested;

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _notifyIcon.Visible = false;
        _notifyIcon.ContextMenuStrip?.Dispose();
        _notifyIcon.Dispose();
        _icon.Dispose();
    }

    private static DrawingIcon LoadIcon()
    {
        var resource = WpfApplication.GetResourceStream(
            new Uri("/Honey.Desktop;component/Assets/Honey.ico", UriKind.Relative))
            ?? throw new InvalidOperationException("无法加载托盘图标 Assets/Honey.ico。");
        using (resource.Stream)
        using (var icon = new DrawingIcon(resource.Stream))
        {
            return (DrawingIcon)icon.Clone();
        }
    }
}
