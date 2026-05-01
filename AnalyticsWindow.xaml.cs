using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using Microsoft.EntityFrameworkCore;
using Color = System.Windows.Media.Color;
using ColorConverter = System.Windows.Media.ColorConverter;
using Point = System.Windows.Point;
using System.Runtime.Versioning;


namespace SysMonBar
{
    // ── EF Core Database ──
    public class PowerReading
    {
        public int Id { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.Now;
        public double PowerWatts { get; set; }
        public double CpuTemp { get; set; }
        public double GpuTemp { get; set; }
        public double CpuUsage { get; set; }
        public double GpuUsage { get; set; }
        public double RamUsageGb { get; set; }
        public double NetUp { get; set; }
        public double NetDown { get; set; }
    }

    public class AppDbContext : DbContext
    {
        public DbSet<PowerReading> PowerReadings => Set<PowerReading>();

        protected override void OnConfiguring(DbContextOptionsBuilder options)
        {
            var dbPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "power_data.db");
            options.UseSqlite($"Data Source={dbPath}");
        }
    }

    public static class AnalyticsService
    {
        public static void EnsureDb()
        {
            using var db = new AppDbContext();
            db.Database.EnsureCreated();

            // Try adding columns safely
            try { db.Database.ExecuteSqlRaw("ALTER TABLE PowerReadings ADD COLUMN CpuUsage REAL NOT NULL DEFAULT 0;"); } catch { }
            try { db.Database.ExecuteSqlRaw("ALTER TABLE PowerReadings ADD COLUMN GpuUsage REAL NOT NULL DEFAULT 0;"); } catch { }
            try { db.Database.ExecuteSqlRaw("ALTER TABLE PowerReadings ADD COLUMN RamUsageGb REAL NOT NULL DEFAULT 0;"); } catch { }
            try { db.Database.ExecuteSqlRaw("ALTER TABLE PowerReadings ADD COLUMN NetUp REAL NOT NULL DEFAULT 0;"); } catch { }
            try { db.Database.ExecuteSqlRaw("ALTER TABLE PowerReadings ADD COLUMN NetDown REAL NOT NULL DEFAULT 0;"); } catch { }
        }

        public static void LogReading(HardwareStats stats)
        {
            try
            {
                using var db = new AppDbContext();
                db.PowerReadings.Add(new PowerReading
                {
                    PowerWatts = stats.PowerWatts,
                    CpuTemp = stats.CpuTemp,
                    GpuTemp = stats.GpuTemp,
                    CpuUsage = stats.CpuUsage,
                    GpuUsage = stats.GpuUsage,
                    RamUsageGb = stats.RamUsedGb,
                    NetUp = stats.NetUp,
                    NetDown = stats.NetDown
                });
                db.SaveChanges();
            }
            catch { /* silently ignore */ }
        }

        public static (int count, double avg, double max, double min, double kwh, double hours)
            GetStats(int hours)
        {
            using var db = new AppDbContext();
            var since = DateTime.Now.AddHours(-hours);
            var readings = db.PowerReadings.Where(r => r.Timestamp > since).ToList();

            if (readings.Count == 0)
                return (0, 0, 0, 0, 0, 0);

            double avg = readings.Average(r => r.PowerWatts);
            double max = readings.Max(r => r.PowerWatts);
            double min = readings.Min(r => r.PowerWatts);
            double hoursOfData = readings.Count / 60.0;
            double kwh = (avg * hoursOfData) / 1000.0;

            return (readings.Count, avg, max, min, kwh, hoursOfData);
        }

        public static List<(string label, double power, double cpuTemp, double gpuTemp, double cpuUsage, double gpuUsage, double ramGb, double net)> GetHourlyAverage(int hours)
        {
            using var db = new AppDbContext();
            var since = DateTime.Now.AddHours(-hours);
            return db.PowerReadings
                .Where(r => r.Timestamp > since)
                .AsEnumerable()
                .GroupBy(r => r.Timestamp.ToString("HH") + "h")
                .Select(g => (
                    g.Key, 
                    g.Average(r => r.PowerWatts),
                    g.Average(r => r.CpuTemp),
                    g.Average(r => r.GpuTemp),
                    g.Average(r => r.CpuUsage),
                    g.Average(r => r.GpuUsage),
                    g.Average(r => r.RamUsageGb),
                    g.Average(r => r.NetUp + r.NetDown)
                ))
                .OrderBy(x => x.Item1)
                .ToList();
        }
    }

    // ── Analytics Window ──
    [SupportedOSPlatform("windows")]
    public partial class AnalyticsWindow : Window

    {
        public AnalyticsWindow()
        {
            InitializeComponent();
            AnalyticsService.EnsureDb();
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

            TxtKwh.Text = $"{kwh:F2} kWh";
            TxtAvg.Text = $"{avg:F1} W";
            TxtMax.Text = $"{max:F1} W";
            TxtMin.Text = $"{min:F1} W";
            TxtDuration.Text = $"{duration:F1} hours";
            TxtReadings.Text = $"{count}";

            UpdateCost(kwh);
            DrawChart(hours);
        }

        private void UpdateCost(double kwh)
        {
            if (double.TryParse(TxtRate.Text, out double rate))
                TxtCost.Text = $"Estimated: ৳{kwh * rate:F2}";
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
    }
}
