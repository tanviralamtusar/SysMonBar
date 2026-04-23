using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;

namespace SysMonBar
{
    public partial class MainWindow : Window
    {
        private readonly HardwareMonitor _monitor;
        private readonly DispatcherTimer _timer;

        public MainWindow()
        {
            InitializeComponent();
            _monitor = new HardwareMonitor();

            _timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1)
            };
            _timer.Tick += Timer_Tick;
            _timer.Start();

            Loaded += MainWindow_Loaded;
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            UpdatePosition();
        }

        private void UpdatePosition()
        {
            var workArea = SystemParameters.WorkArea;
            Left = workArea.Right - Width - 300;
            Top = workArea.Bottom - Height - 2;
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
            _timer.Stop();
            _monitor.Dispose();
            base.OnClosed(e);
        }
    }
}