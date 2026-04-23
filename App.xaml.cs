using System.Drawing;
using System.IO;
using System.Windows;
using Application = System.Windows.Application;

namespace SysMonBar;

public partial class App : Application
{
    private System.Windows.Forms.NotifyIcon? _trayIcon;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        SetupTrayIcon();
    }

    private MainWindow? GetMainWin() => MainWindow as MainWindow;

    private void SetupTrayIcon()
    {
        _trayIcon = new System.Windows.Forms.NotifyIcon();

        var iconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "icon.png");
        if (File.Exists(iconPath))
        {
            using var bmp = new Bitmap(iconPath);
            _trayIcon.Icon = Icon.FromHandle(bmp.GetHicon());
        }
        else
        {
            _trayIcon.Icon = SystemIcons.Application;
        }

        _trayIcon.Text = "SysMonBar";
        _trayIcon.Visible = true;

        var menu = new System.Windows.Forms.ContextMenuStrip();
        menu.Items.Add("📊 Analytics", null, (s, ev) => GetMainWin()?.OpenAnalytics());
        menu.Items.Add("⚙️ Settings", null, (s, ev) => GetMainWin()?.OpenSettings());
        menu.Items.Add(new System.Windows.Forms.ToolStripSeparator());
        menu.Items.Add("❌ Exit", null, (s, ev) => Shutdown());

        _trayIcon.ContextMenuStrip = menu;
    }

    protected override void OnExit(ExitEventArgs e)
    {
        if (_trayIcon != null)
        {
            _trayIcon.Visible = false;
            _trayIcon.Dispose();
        }
        base.OnExit(e);
    }
}
