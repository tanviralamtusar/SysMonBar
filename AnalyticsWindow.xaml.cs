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
        }

        public static void LogReading(double watts, double cpuTemp, double gpuTemp)
        {
            try
            {
                using var db = new AppDbContext();
                db.PowerReadings.Add(new PowerReading
                {
                    PowerWatts = watts,
                    CpuTemp = cpuTemp,
                    GpuTemp = gpuTemp
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

        public static List<(string label, double value)> GetHourlyAverage(int hours)
        {
            using var db = new AppDbContext();
            var since = DateTime.Now.AddHours(-hours);
            return db.PowerReadings
                .Where(r => r.Timestamp > since)
                .AsEnumerable()
                .GroupBy(r => r.Timestamp.ToString("HH") + "h")
                .Select(g => (g.Key, g.Average(r => r.PowerWatts)))
                .OrderBy(x => x.Key)
                .ToList();
        }
    }

    // ── Analytics Window ──
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

        private void DrawChart(int hours)
        {
            ChartCanvas.Children.Clear();
            var data = AnalyticsService.GetHourlyAverage(hours);
            if (data.Count < 2) return;

            double w = ChartCanvas.ActualWidth > 0 ? ChartCanvas.ActualWidth : 480;
            double h = ChartCanvas.ActualHeight > 0 ? ChartCanvas.ActualHeight : 140;
            double maxVal = data.Max(d => d.value);
            if (maxVal < 1) maxVal = 100;

            // Draw line chart
            var polyline = new Polyline
            {
                Stroke = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#3498db")),
                StrokeThickness = 2
            };

            for (int i = 0; i < data.Count; i++)
            {
                double x = (i / (double)(data.Count - 1)) * (w - 20) + 10;
                double y = h - (data[i].value / maxVal * (h - 20)) - 10;
                polyline.Points.Add(new Point(x, y));
            }

            ChartCanvas.Children.Add(polyline);

            // Axis labels
            var maxLabel = new TextBlock { Text = $"{maxVal:F0}W", Foreground = new SolidColorBrush(Colors.Gray), FontSize = 10 };
            Canvas.SetLeft(maxLabel, 2); Canvas.SetTop(maxLabel, 2);
            ChartCanvas.Children.Add(maxLabel);

            var minLabel = new TextBlock { Text = "0W", Foreground = new SolidColorBrush(Colors.Gray), FontSize = 10 };
            Canvas.SetLeft(minLabel, 2); Canvas.SetTop(minLabel, h - 16);
            ChartCanvas.Children.Add(minLabel);
        }
    }
}
