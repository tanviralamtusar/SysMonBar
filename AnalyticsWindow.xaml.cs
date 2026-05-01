using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using Microsoft.Data.Sqlite;
using Color = System.Windows.Media.Color;
using ColorConverter = System.Windows.Media.ColorConverter;
using Point = System.Windows.Point;
using System.Runtime.Versioning;
using System.Globalization;
using System.Windows.Threading;


namespace SysMonBar
{
    public class PowerReading
    {
        public int Id { get; set; }
        public DateTime Timestamp { get; set; }
        public double PowerWatts { get; set; }
        public double CpuTemp { get; set; }
        public double GpuTemp { get; set; }
        public double CpuUsage { get; set; }
        public double GpuUsage { get; set; }
        public double RamUsageGb { get; set; }
        public double NetUp { get; set; }
        public double NetDown { get; set; }
    }

    public static class AnalyticsService
    {
        private static string GetConnectionString()
        {
            var dbPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "power_data.db");
            return $"Data Source={dbPath}";
        }

        public static void EnsureDb()
        {
            using var connection = new SqliteConnection(GetConnectionString());
            connection.Open();

            var command = connection.CreateCommand();
            command.CommandText = 
            @"
                CREATE TABLE IF NOT EXISTS PowerReadings (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    Timestamp TEXT NOT NULL,
                    PowerWatts REAL NOT NULL,
                    CpuTemp REAL NOT NULL,
                    GpuTemp REAL NOT NULL,
                    CpuUsage REAL NOT NULL DEFAULT 0,
                    GpuUsage REAL NOT NULL DEFAULT 0,
                    RamUsageGb REAL NOT NULL DEFAULT 0,
                    NetUp REAL NOT NULL DEFAULT 0,
                    NetDown REAL NOT NULL DEFAULT 0
                );
            ";
            command.ExecuteNonQuery();

            // Check for missing columns (migration)
            var columns = new List<string>();
            var checkCmd = connection.CreateCommand();
            checkCmd.CommandText = "PRAGMA table_info(PowerReadings);";
            using (var reader = checkCmd.ExecuteReader())
            {
                while (reader.Read()) columns.Add(reader["name"].ToString() ?? "");
            }

            string[] required = { "CpuUsage", "GpuUsage", "RamUsageGb", "NetUp", "NetDown" };
            foreach (var col in required)
            {
                if (!columns.Contains(col))
                {
                    var alterCmd = connection.CreateCommand();
                    alterCmd.CommandText = $"ALTER TABLE PowerReadings ADD COLUMN {col} REAL NOT NULL DEFAULT 0;";
                    try { alterCmd.ExecuteNonQuery(); } catch { }
                }
            }
        }

        public static void LogReading(HardwareStats stats)
        {
            try
            {
                using var connection = new SqliteConnection(GetConnectionString());
                connection.Open();
                var command = connection.CreateCommand();
                command.CommandText = 
                @"
                    INSERT INTO PowerReadings (Timestamp, PowerWatts, CpuTemp, GpuTemp, CpuUsage, GpuUsage, RamUsageGb, NetUp, NetDown)
                    VALUES ($ts, $power, $ctemp, $gtemp, $cusage, $gusage, $ram, $nup, $ndown)
                ";
                command.Parameters.AddWithValue("$ts", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                command.Parameters.AddWithValue("$power", stats.PowerWatts);
                command.Parameters.AddWithValue("$ctemp", stats.CpuTemp);
                command.Parameters.AddWithValue("$gtemp", stats.GpuTemp);
                command.Parameters.AddWithValue("$cusage", stats.CpuUsage);
                command.Parameters.AddWithValue("$gusage", stats.GpuUsage);
                command.Parameters.AddWithValue("$ram", stats.RamUsedGb);
                command.Parameters.AddWithValue("$nup", stats.NetUp);
                command.Parameters.AddWithValue("$ndown", stats.NetDown);
                command.ExecuteNonQuery();
            }
            catch { }
        }

        public static (int count, double avg, double max, double min, double kwh, double hours)
            GetStats(int hoursLimit)
        {
            try
            {
                using var connection = new SqliteConnection(GetConnectionString());
                connection.Open();
                var since = DateTime.Now.AddHours(-hoursLimit).ToString("yyyy-MM-dd HH:mm:ss");

                var command = connection.CreateCommand();
                command.CommandText = 
                @"
                    SELECT COUNT(*), AVG(PowerWatts), MAX(PowerWatts), MIN(PowerWatts)
                    FROM PowerReadings WHERE Timestamp > $since
                ";
                command.Parameters.AddWithValue("$since", since);

                using var reader = command.ExecuteReader();
                if (reader.Read() && !reader.IsDBNull(0))
                {
                    int count = reader.GetInt32(0);
                    double avg = reader.GetDouble(1);
                    double max = reader.GetDouble(2);
                    double min = reader.GetDouble(3);
                    double hoursOfData = count / 60.0;
                    double kwh = (avg * hoursOfData) / 1000.0;
                    return (count, avg, max, min, kwh, hoursOfData);
                }
            }
            catch { }
            return (0, 0, 0, 0, 0, 0);
        }

        public static List<(string label, double power, double cpuTemp, double gpuTemp, double cpuUsage, double gpuUsage, double ramGb, double net)> GetHourlyAverage(int hoursLimit)
        {
            var result = new List<(string, double, double, double, double, double, double, double)>();
            try
            {
                using var connection = new SqliteConnection(GetConnectionString());
                connection.Open();
                var since = DateTime.Now.AddHours(-hoursLimit).ToString("yyyy-MM-dd HH:mm:ss");

                var command = connection.CreateCommand();
                command.CommandText = 
                @"
                    SELECT strftime('%H', Timestamp) || 'h' as Hour,
                           AVG(PowerWatts), AVG(CpuTemp), AVG(GpuTemp), 
                           AVG(CpuUsage), AVG(GpuUsage), AVG(RamUsageGb), AVG(NetUp + NetDown)
                    FROM PowerReadings 
                    WHERE Timestamp > $since
                    GROUP BY Hour
                    ORDER BY Timestamp ASC
                ";
                command.Parameters.AddWithValue("$since", since);

                using var reader = command.ExecuteReader();
                while (reader.Read())
                {
                    result.Add((
                        reader.GetString(0),
                        reader.GetDouble(1),
                        reader.GetDouble(2),
                        reader.GetDouble(3),
                        reader.GetDouble(4),
                        reader.GetDouble(5),
                        reader.GetDouble(6),
                        reader.GetDouble(7)
                    ));
                }
            }
            catch { }
            return result;
        }
    }

    // ── Analytics Window ──
    [SupportedOSPlatform("windows")]
    public partial class AnalyticsWindow : Window
    {
        private double _currentKwh;
        private double _currentAvgWatts;
        private readonly DispatcherTimer _refreshTimer;

        public AnalyticsWindow()
        {
            InitializeComponent();
            AnalyticsService.EnsureDb();

            _refreshTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(60) };
            _refreshTimer.Tick += (s, e) => LoadData(GetHours());
            _refreshTimer.Start();

            // LoadData(24) will be called after Window_Loaded sets the rate
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            var settings = AppSettings.Load();
            TxtRate.Text = settings.ElectricityRate.ToString(CultureInfo.InvariantCulture);
            LoadData(24);
        }

        private void Period_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (!IsLoaded) return;
            LoadData(GetHours());
        }

        private void Refresh_Click(object sender, RoutedEventArgs e) => LoadData(GetHours());
        private void Close_Click(object sender, RoutedEventArgs e) => Close();

        private int GetHours()
        {
            var item = CmbPeriod.SelectedItem as ComboBoxItem;
            var text = item?.Content.ToString() ?? "";
            if (text.Contains("7")) return 7 * 24;
            if (text.Contains("30")) return 30 * 24;
            return 24;
        }

        private void LoadData(int hours)
        {
            var (count, avg, max, min, kwh, duration) = AnalyticsService.GetStats(hours);

            _currentKwh = kwh;
            _currentAvgWatts = avg;

            TxtKwh.Text = $"{kwh:F2} kWh";
            TxtAvg.Text = $"{avg:F1} W";
            TxtMax.Text = $"{max:F1} W";
            TxtMin.Text = $"{min:F1} W";
            TxtDuration.Text = $"{duration:F1} hours";
            TxtReadings.Text = $"{count}";

            UpdateCost();
            DrawChart(hours);
        }

        private void TxtRate_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!IsLoaded) return;
            
            if (double.TryParse(TxtRate.Text, NumberStyles.Any, CultureInfo.InvariantCulture, out double rate))
            {
                var settings = AppSettings.Load();
                settings.ElectricityRate = rate;
                settings.Save();
            }
            
            UpdateCost();
        }

        private void UpdateCost()
        {
            if (double.TryParse(TxtRate.Text, NumberStyles.Any, CultureInfo.InvariantCulture, out double rate))
            {
                double periodCost = _currentKwh * rate;
                double monthlyKwh = (_currentAvgWatts * 24 * 30) / 1000.0;
                double monthlyCost = monthlyKwh * rate;

                TxtCost.Text = $"Period: ৳{periodCost:F2}\nEst. Monthly: ৳{monthlyCost:F0}";
            }
            else
            {
                TxtCost.Text = "Invalid Rate";
            }
        }

        private void DrawLine(Canvas canvas, List<double> values, Color color, string unit)
        {
            canvas.Children.Clear();
            if (values.Count < 2) return;

            double w = canvas.ActualWidth > 0 ? canvas.ActualWidth : 480;
            double h = canvas.ActualHeight > 0 ? canvas.ActualHeight : 100;
            double maxVal = values.Max();
            if (maxVal < 1) maxVal = 10;

            var polyline = new Polyline
            {
                Stroke = new SolidColorBrush(color),
                StrokeThickness = 2
            };

            for (int i = 0; i < values.Count; i++)
            {
                double x = (i / (double)(values.Count - 1)) * (w - 20) + 10;
                double y = h - (values[i] / maxVal * (h - 20)) - 10;
                polyline.Points.Add(new Point(x, y));
            }

            canvas.Children.Add(polyline);

            var maxLabel = new TextBlock { Text = $"{maxVal:F0}{unit}", Foreground = new SolidColorBrush(Colors.Gray), FontSize = 10 };
            Canvas.SetLeft(maxLabel, 2); Canvas.SetTop(maxLabel, 2);
            canvas.Children.Add(maxLabel);

            var minLabel = new TextBlock { Text = $"0{unit}", Foreground = new SolidColorBrush(Colors.Gray), FontSize = 10 };
            Canvas.SetLeft(minLabel, 2); Canvas.SetTop(minLabel, h - 16);
            canvas.Children.Add(minLabel);
        }

        private void DrawMultiLine(Canvas canvas, List<double> values1, Color color1, List<double> values2, Color color2, string unit)
        {
            canvas.Children.Clear();
            if (values1.Count < 2 || values2.Count < 2) return;

            double w = canvas.ActualWidth > 0 ? canvas.ActualWidth : 480;
            double h = canvas.ActualHeight > 0 ? canvas.ActualHeight : 100;
            double maxVal = Math.Max(values1.Max(), values2.Max());
            if (maxVal < 1) maxVal = 10;

            Action<List<double>, Color> drawLine = (vals, col) => 
            {
                var polyline = new Polyline { Stroke = new SolidColorBrush(col), StrokeThickness = 2 };
                for (int i = 0; i < vals.Count; i++)
                {
                    double x = (i / (double)(vals.Count - 1)) * (w - 20) + 10;
                    double y = h - (vals[i] / maxVal * (h - 20)) - 10;
                    polyline.Points.Add(new Point(x, y));
                }
                canvas.Children.Add(polyline);
            };

            drawLine(values1, color1);
            drawLine(values2, color2);

            var maxLabel = new TextBlock { Text = $"{maxVal:F0}{unit}", Foreground = new SolidColorBrush(Colors.Gray), FontSize = 10 };
            Canvas.SetLeft(maxLabel, 2); Canvas.SetTop(maxLabel, 2);
            canvas.Children.Add(maxLabel);

            var minLabel = new TextBlock { Text = $"0{unit}", Foreground = new SolidColorBrush(Colors.Gray), FontSize = 10 };
            Canvas.SetLeft(minLabel, 2); Canvas.SetTop(minLabel, h - 16);
            canvas.Children.Add(minLabel);
        }

        private void DrawChart(int hours)
        {
            var data = AnalyticsService.GetHourlyAverage(hours);
            if (data.Count < 2) 
            {
                PowerCanvas.Children.Clear();
                TempCanvas.Children.Clear();
                UsageCanvas.Children.Clear();
                RamCanvas.Children.Clear();
                NetCanvas.Children.Clear();
                return;
            }

            DrawLine(PowerCanvas, data.Select(d => d.power).ToList(), (Color)ColorConverter.ConvertFromString("#f1c40f"), "W");
            DrawMultiLine(TempCanvas, data.Select(d => d.cpuTemp).ToList(), (Color)ColorConverter.ConvertFromString("#e74c3c"), data.Select(d => d.gpuTemp).ToList(), (Color)ColorConverter.ConvertFromString("#2ecc71"), "°C");
            DrawMultiLine(UsageCanvas, data.Select(d => d.cpuUsage).ToList(), (Color)ColorConverter.ConvertFromString("#3498db"), data.Select(d => d.gpuUsage).ToList(), (Color)ColorConverter.ConvertFromString("#9b59b6"), "%");
            DrawLine(RamCanvas, data.Select(d => d.ramGb).ToList(), (Color)ColorConverter.ConvertFromString("#1abc9c"), "GB");
            DrawLine(NetCanvas, data.Select(d => d.net / 1024.0).ToList(), (Color)ColorConverter.ConvertFromString("#e67e22"), "KB/s");
        }
        protected override void OnClosed(EventArgs e)
        {
            _refreshTimer.Stop();
            App.TrimMemory();
            base.OnClosed(e);
        }
    }
}
