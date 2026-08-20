using System;
using System.Drawing;
using System.Windows.Forms;

namespace DynamicIsland;

public sealed class TrayIconService : IDisposable
{
    private readonly NotifyIcon _notifyIcon;
    private readonly ToolStripMenuItem _autostartItem;

    public event Action? ShowRequested;
    public event Action? QuitRequested;
    public event Action<bool>? AutostartToggled;

    public TrayIconService(bool autostartEnabled)
    {
        var menu = new ContextMenuStrip();

        var showItem = menu.Items.Add("Show/Hide Island");
        showItem.Click += (_, _) => ShowRequested?.Invoke();

        _autostartItem = new ToolStripMenuItem("Launch at startup")
        {
            CheckOnClick = true,
            Checked = autostartEnabled
        };
        _autostartItem.CheckedChanged += (_, _) => AutostartToggled?.Invoke(_autostartItem.Checked);
        menu.Items.Add(_autostartItem);

        menu.Items.Add(new ToolStripSeparator());

        var quitItem = menu.Items.Add("Quit");
        quitItem.Click += (_, _) => QuitRequested?.Invoke();

        _notifyIcon = new NotifyIcon
        {
            Icon = SystemIcons.Application,
            Visible = true,
            Text = "Dynamic Island",
            ContextMenuStrip = menu
        };
    }

    public void Dispose()
    {
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
    }
}
