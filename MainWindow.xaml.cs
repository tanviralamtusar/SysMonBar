using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;

namespace SysMonBar
{
    public partial class MainWindow : Window
    {
        private readonly HardwareMonitor _monitor;
        private readonly DispatcherTimer _updateTimer;
        private readonly DispatcherTimer _topMostTimer;
        private AppSettings _settings = new();
        private DateTime _lastLogTime = DateTime.Now;

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

        public MainWindow()
        {
            InitializeComponent();

            // Init database
            AnalyticsService.EnsureDb();

            _monitor = new HardwareMonitor();

            _updateTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _updateTimer.Tick += Timer_Tick;
            _updateTimer.Start();

            _topMostTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
            _topMostTimer.Tick += (s, e) => ForceTopMost();
            _topMostTimer.Start();

            Loaded += MainWindow_Loaded;
            SourceInitialized += MainWindow_SourceInitialized;
        }

        private void MainWindow_SourceInitialized(object? sender, EventArgs e)
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            int exStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
            exStyle |= WS_EX_TOOLWINDOW;
            SetWindowLong(hwnd, GWL_EXSTYLE, exStyle);
            ForceTopMost();
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            UpdatePosition();
        }

        private void UpdatePosition()
        {
            var workArea = SystemParameters.WorkArea;
            Left = workArea.Right - ActualWidth - 300;
            Top = workArea.Bottom - ActualHeight;
        }

        private void Window_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            UpdatePosition();
        }

        private void ForceTopMost()
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            if (hwnd != IntPtr.Zero)
                SetWindowPos(hwnd, HWND_TOPMOST, 0, 0, 0, 0,
                    SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE | SWP_SHOWWINDOW);
        }

        private System.Windows.Media.Brush GetBrush(string hex)
        {
            try
            {
                var color = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(hex);
                return new System.Windows.Media.SolidColorBrush(color);
            }
            catch
            {
                return new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.Gray);
            }
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

                // CPU
                if (_settings.ShowCpu) { CpuMetric.Visibility = Visibility.Visible; }
                else { CpuMetric.Visibility = Visibility.Collapsed; }
                CpuMetric.Value = stats.CpuUsage;
                CpuMetric.GraphWidth = _settings.GraphWidth;
                CpuMetric.Color = GetBrush(_settings.CpuColor);
                CpuMetric.DisplayMode = _settings.DisplayMode;
                CpuMetric.ToolTip = $"CPU: {stats.CpuUsage:F0}%";

                // RAM
                if (_settings.ShowRam) { RamMetric.Visibility = Visibility.Visible; }
                else { RamMetric.Visibility = Visibility.Collapsed; }
                RamMetric.Value = stats.RamUsedGb;
                RamMetric.MaxValue = stats.RamTotalGb;
                RamMetric.GraphWidth = _settings.GraphWidth;
                RamMetric.Color = GetBrush(_settings.RamColor);
                RamMetric.DisplayMode = _settings.DisplayMode;
                if (_settings.RamUnit == "MB")
                    RamMetric.ToolTip = $"RAM: {stats.RamUsedGb * 1024:F0}/{stats.RamTotalGb * 1024:F0} MB";
                else
                    RamMetric.ToolTip = $"RAM: {stats.RamUsedGb:F1}/{stats.RamTotalGb:F1} GB";

                // GPU
                if (_settings.ShowGpu) { GpuMetric.Visibility = Visibility.Visible; }
                else { GpuMetric.Visibility = Visibility.Collapsed; }
                GpuMetric.Value = stats.GpuUsage;
                GpuMetric.GraphWidth = _settings.GraphWidth;
                GpuMetric.Color = GetBrush(_settings.GpuColor);
                GpuMetric.DisplayMode = _settings.DisplayMode;
                GpuMetric.ToolTip = $"GPU: {stats.GpuUsage:F0}%";

                // Network
                if (_settings.ShowNet) { NetMetric.Visibility = Visibility.Visible; }
                else { NetMetric.Visibility = Visibility.Collapsed; }
                double netDown = stats.NetDown;
                double netUp = stats.NetUp;
                NetMetric.Value = netDown + netUp;
                NetMetric.MaxValue = 10;
                NetMetric.GraphWidth = _settings.GraphWidth;
                NetMetric.Color = GetBrush(_settings.NetColor);
                NetMetric.DisplayMode = _settings.DisplayMode;
                string netText = FormatNet(netDown, _settings.NetUnit) + " / " + FormatNet(netUp, _settings.NetUnit);
                NetMetric.ToolTip = $"↓{FormatNet(netDown, _settings.NetUnit)}  ↑{FormatNet(netUp, _settings.NetUnit)}";

                // Power
                if (_settings.ShowPower) { PowerText.Visibility = Visibility.Visible; }
                else { PowerText.Visibility = Visibility.Collapsed; }
                PowerText.Foreground = GetBrush(_settings.PowerColor);
                PowerText.Text = $"{stats.PowerWatts:F0}W";

                // Temp
                if (_settings.ShowTemp) { TempText.Visibility = Visibility.Visible; }
                else { TempText.Visibility = Visibility.Collapsed; }
                TempText.Foreground = GetBrush(_settings.TempColor);
                TempText.Text = $"{stats.CombinedTemp:F0}°C";
                TempText.ToolTip = $"CPU: {stats.CpuTemp:F0}°C | GPU: {stats.GpuTemp:F0}°C";

                // Log to database every 60 seconds
                if ((DateTime.Now - _lastLogTime).TotalSeconds >= 60)
                {
                    AnalyticsService.LogReading(stats);
                    _lastLogTime = DateTime.Now;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Update error: {ex.Message}");
            }
        }

        private string FormatNet(double bytesPerSec, string unit)
        {
            return unit switch
            {
                "mbps" => $"{bytesPerSec * 8 / 1024 / 1024:F2} Mbps",
                "KB/s" => $"{bytesPerSec / 1024:F1} KB/s",
                "MB/s" => $"{bytesPerSec / 1024 / 1024:F2} MB/s",
                _ => $"{bytesPerSec * 8 / 1024:F1} kbps"
            };
        }

        public void OpenSettings()
        {
            var dlg = new SettingsWindow(_settings);
            if (dlg.ShowDialog() == true && dlg.Saved)
            {
                _settings = dlg.Settings;
            }
        }

        public void OpenAnalytics()
        {
            var dlg = new AnalyticsWindow();
            dlg.ShowDialog();
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