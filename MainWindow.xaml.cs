using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Threading;

namespace SysMonBar
{
    public partial class MainWindow : Window
    {
        private readonly HardwareMonitor _monitor;
        private readonly DispatcherTimer _updateTimer;
        private readonly DispatcherTimer _topMostTimer;

        // Win32 API for staying above taskbar
        [DllImport("user32.dll")]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

        [DllImport("user32.dll")]
        private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        [DllImport("user32.dll")]
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        private static readonly IntPtr HWND_TOPMOST = new(-1);
        private const uint SWP_NOMOVE = 0x0002;
        private const uint SWP_NOSIZE = 0x0001;
        private const uint SWP_NOACTIVATE = 0x0010;
        private const uint SWP_SHOWWINDOW = 0x0040;
        private const int GWL_EXSTYLE = -20;
        private const int WS_EX_TOOLWINDOW = 0x00000080;
        private const int WS_EX_TOPMOST = 0x00000008;

        public MainWindow()
        {
            InitializeComponent();
            _monitor = new HardwareMonitor();

            // Hardware stats update every 1 second
            _updateTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _updateTimer.Tick += Timer_Tick;
            _updateTimer.Start();

            // Keep window above taskbar every 500ms (like the Python version)
            _topMostTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
            _topMostTimer.Tick += TopMostTimer_Tick;
            _topMostTimer.Start();

            Loaded += MainWindow_Loaded;
            SourceInitialized += MainWindow_SourceInitialized;
        }

        private void MainWindow_SourceInitialized(object? sender, EventArgs e)
        {
            // Set WS_EX_TOOLWINDOW style — this makes the window behave like a toolbar:
            // - Stays above taskbar
            // - Doesn't appear in Alt+Tab
            // - Doesn't appear in taskbar
            var hwnd = new WindowInteropHelper(this).Handle;
            int exStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
            exStyle |= WS_EX_TOOLWINDOW;
            SetWindowLong(hwnd, GWL_EXSTYLE, exStyle);

            // Force topmost position
            ForceTopMost();
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            UpdatePosition();
        }

        private void UpdatePosition()
        {
            var workArea = SystemParameters.WorkArea;
            Left = workArea.Right - Width - 300;
            Top = workArea.Bottom - Height;
        }

        private void ForceTopMost()
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            if (hwnd != IntPtr.Zero)
            {
                SetWindowPos(hwnd, HWND_TOPMOST, 0, 0, 0, 0,
                    SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE | SWP_SHOWWINDOW);
            }
        }

        private void TopMostTimer_Tick(object? sender, EventArgs e)
        {
            ForceTopMost();
        }

        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            DragMove();
        }

        private void Timer_Tick(object? sender, EventArgs e)
        {
            try
            {
                var stats = _monitor.GetStats();

                CpuMetric.Value = stats.CpuUsage;
                CpuMetric.ToolTip = $"CPU: {stats.CpuUsage:F0}%";

                RamMetric.Value = stats.RamUsedGb;
                RamMetric.MaxValue = stats.RamTotalGb;
                RamMetric.ToolTip = $"RAM: {stats.RamUsedGb:F1}/{stats.RamTotalGb:F1} GB";

                GpuMetric.Value = stats.GpuUsage;
                GpuMetric.ToolTip = $"GPU: {stats.GpuUsage:F0}%";

                NetMetric.Value = stats.NetUp + stats.NetDown;
                NetMetric.MaxValue = 10;
                NetMetric.ToolTip = $"Net: ↓{stats.NetDown:F1} ↑{stats.NetUp:F1} MB/s";

                PowerText.Text = $"{stats.PowerWatts:F0}W";
                TempText.Text = $"{stats.CombinedTemp:F0}°C";
                TempText.ToolTip = $"CPU: {stats.CpuTemp:F0}°C | GPU: {stats.GpuTemp:F0}°C";
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Update error: {ex.Message}");
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            _updateTimer.Stop();
            _topMostTimer.Stop();
            _monitor.Dispose();
            base.OnClosed(e);
        }
    }
}