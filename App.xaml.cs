using System.Drawing;
using System.IO;
using System.Windows;
using Application = System.Windows.Application;
using System.Runtime.Versioning;


namespace SysMonBar;

[SupportedOSPlatform("windows")]
public partial class App : Application

{
    private System.Windows.Forms.NotifyIcon? _trayIcon;
    private System.Windows.Threading.DispatcherTimer? _memoryTimer;

    [System.Runtime.InteropServices.DllImport("kernel32.dll")]
    private static extern bool SetProcessWorkingSetSize(IntPtr proc, int min, int max);

    protected override void OnStartup(StartupEventArgs e)
    {
        this.DispatcherUnhandledException += (s, ev) => 
        {
            File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "crash_log.txt"), ev.Exception.ToString());
            ev.Handled = true;
            Shutdown();
        };

        AppDomain.CurrentDomain.UnhandledException += (s, ev) =>
        {
            if (ev.ExceptionObject is Exception ex)
                File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "crash_log_domain.txt"), ex.ToString());
        };

        base.OnStartup(e);
        SetupTrayIcon();
        StartMemoryOptimization();
    }

    private void StartMemoryOptimization()
    {
        _memoryTimer = new System.Windows.Threading.DispatcherTimer 
        { 
            Interval = TimeSpan.FromMinutes(1) 
        };
        _memoryTimer.Tick += (s, e) => TrimMemory();
        _memoryTimer.Start();
        
        // Initial trim after startup finishes
        System.Threading.Tasks.Task.Delay(5000).ContinueWith(_ => 
        {
            Dispatcher.Invoke(TrimMemory);
        });
    }

    public static void TrimMemory()
    {
        try
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            
            if (Environment.OSVersion.Platform == PlatformID.Win32NT)
            {
                using var proc = System.Diagnostics.Process.GetCurrentProcess();
                SetProcessWorkingSetSize(proc.Handle, -1, -1);
            }
        }
        catch { }
    }

    private MainWindow? GetMainWin() => MainWindow as MainWindow;

    private void SetupTrayIcon()
    {
        try
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
        catch (Exception ex)
        {
            File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "icon_error.txt"), ex.ToString());
        }
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
