using System;
using System.IO;
using System.Text.Json;
using System.Windows;
using Microsoft.Win32;

namespace SysMonBar
{
    public class AppSettings
    {
        public bool ShowCpu { get; set; } = true;
        public bool ShowRam { get; set; } = true;
        public bool ShowGpu { get; set; } = true;
        public bool ShowNet { get; set; } = true;
        public bool ShowPower { get; set; } = true;
        public bool ShowTemp { get; set; } = true;
        
        public string CpuColor { get; set; } = "#3498db";
        public string RamColor { get; set; } = "#9b59b6";
        public string GpuColor { get; set; } = "#2ecc71";
        public string NetColor { get; set; } = "#1abc9c";
        public string PowerColor { get; set; } = "#e67e22";
        public string TempColor { get; set; } = "#e74c3c";

        public string RamUnit { get; set; } = "GB";
        public string NetUnit { get; set; } = "kbps";
        public bool RunOnStartup { get; set; } = false;
        public string DisplayMode { get; set; } = "Graph";
        public double GraphWidth { get; set; } = 36;

        public bool LockPosition { get; set; } = false;
        public double? WindowLeft { get; set; }
        public double? WindowTop { get; set; }

        private static string GetSettingsFilePath()
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var dir = Path.Combine(appData, "SysMonBar");
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            return Path.Combine(dir, "settings.json");
        }

        public static AppSettings Load()
        {
            try
            {
                var file = GetSettingsFilePath();
                if (File.Exists(file))
                {
                    var json = File.ReadAllText(file);
                    return JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to load settings: {ex.Message}");
            }
            return new AppSettings();
        }

        public void Save()
        {
            try
            {
                var file = GetSettingsFilePath();
                var json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(file, json);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to save settings: {ex.Message}");
            }
        }
    }

    public class ColorOption
    {
        public string Name { get; set; } = string.Empty;
        public string Hex { get; set; } = string.Empty;
    }

    public partial class SettingsWindow : Window
    {
        public List<ColorOption> AvailableColors { get; } = new List<ColorOption>
        {
            new ColorOption { Name = "Blue", Hex = "#3498db" },
            new ColorOption { Name = "Purple", Hex = "#9b59b6" },
            new ColorOption { Name = "Green", Hex = "#2ecc71" },
            new ColorOption { Name = "Cyan", Hex = "#1abc9c" },
            new ColorOption { Name = "Orange", Hex = "#e67e22" },
            new ColorOption { Name = "Red", Hex = "#e74c3c" },
            new ColorOption { Name = "Yellow", Hex = "#f1c40f" },
            new ColorOption { Name = "Pink", Hex = "#e84393" },
            new ColorOption { Name = "White", Hex = "#ffffff" },
            new ColorOption { Name = "Gray", Hex = "#95a5a6" }
        };

        public AppSettings Settings { get; private set; }
        public bool Saved { get; private set; }

        public SettingsWindow(AppSettings current)
        {
            InitializeComponent();
            this.DataContext = this;
            Settings = current;
            Settings.RunOnStartup = CheckStartupStatus();
            LoadSettingsToUI();
        }

        private void LoadSettingsToUI()
        {
            chkCpu.IsChecked = Settings.ShowCpu;
            chkRam.IsChecked = Settings.ShowRam;
            chkGpu.IsChecked = Settings.ShowGpu;
            chkNet.IsChecked = Settings.ShowNet;
            chkPower.IsChecked = Settings.ShowPower;
            chkTemp.IsChecked = Settings.ShowTemp;
            chkStartup.IsChecked = Settings.RunOnStartup;
            chkLockPosition.IsChecked = Settings.LockPosition;

            cbCpuColor.SelectedValue = Settings.CpuColor;
            cbRamColor.SelectedValue = Settings.RamColor;
            cbGpuColor.SelectedValue = Settings.GpuColor;
            cbNetColor.SelectedValue = Settings.NetColor;
            cbPowerColor.SelectedValue = Settings.PowerColor;
            cbTempColor.SelectedValue = Settings.TempColor;

            rbRamGB.IsChecked = Settings.RamUnit == "GB";
            rbRamMB.IsChecked = Settings.RamUnit == "MB";

            rbNetKbps.IsChecked = Settings.NetUnit == "kbps";
            rbNetMbps.IsChecked = Settings.NetUnit == "mbps" || Settings.NetUnit == "Mbps";
            rbNetKBs.IsChecked = Settings.NetUnit == "KB/s";
            rbNetMBs.IsChecked = Settings.NetUnit == "MB/s";

            rbDispGraph.IsChecked = Settings.DisplayMode == "Graph";
            rbDispBar.IsChecked = Settings.DisplayMode == "Bar";
            rbDispText.IsChecked = Settings.DisplayMode == "Text";

            slGraphWidth.Value = Settings.GraphWidth > 0 ? Settings.GraphWidth : 36;
        }

        private void SaveSettingsFromUI()
        {
            Settings.ShowCpu = chkCpu.IsChecked ?? true;
            Settings.ShowRam = chkRam.IsChecked ?? true;
            Settings.ShowGpu = chkGpu.IsChecked ?? true;
            Settings.ShowNet = chkNet.IsChecked ?? true;
            Settings.ShowPower = chkPower.IsChecked ?? true;
            Settings.ShowTemp = chkTemp.IsChecked ?? true;
            Settings.RunOnStartup = chkStartup.IsChecked ?? false;
            Settings.LockPosition = chkLockPosition.IsChecked ?? false;

            if (cbCpuColor.SelectedValue != null) Settings.CpuColor = cbCpuColor.SelectedValue.ToString()!;
            if (cbRamColor.SelectedValue != null) Settings.RamColor = cbRamColor.SelectedValue.ToString()!;
            if (cbGpuColor.SelectedValue != null) Settings.GpuColor = cbGpuColor.SelectedValue.ToString()!;
            if (cbNetColor.SelectedValue != null) Settings.NetColor = cbNetColor.SelectedValue.ToString()!;
            if (cbPowerColor.SelectedValue != null) Settings.PowerColor = cbPowerColor.SelectedValue.ToString()!;
            if (cbTempColor.SelectedValue != null) Settings.TempColor = cbTempColor.SelectedValue.ToString()!;

            if (rbRamGB.IsChecked == true) Settings.RamUnit = "GB";
            else if (rbRamMB.IsChecked == true) Settings.RamUnit = "MB";

            if (rbNetKbps.IsChecked == true) Settings.NetUnit = "kbps";
            else if (rbNetMbps.IsChecked == true) Settings.NetUnit = "Mbps";
            else if (rbNetKBs.IsChecked == true) Settings.NetUnit = "KB/s";
            else if (rbNetMBs.IsChecked == true) Settings.NetUnit = "MB/s";

            if (rbDispGraph.IsChecked == true) Settings.DisplayMode = "Graph";
            else if (rbDispBar.IsChecked == true) Settings.DisplayMode = "Bar";
            else if (rbDispText.IsChecked == true) Settings.DisplayMode = "Text";

            Settings.GraphWidth = slGraphWidth.Value;
        }

        private void slGraphWidth_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (lblGraphWidth != null)
            {
                lblGraphWidth.Text = $"{(int)e.NewValue}px";
            }
        }

        private void btnSave_Click(object sender, RoutedEventArgs e)
        {
            SaveSettingsFromUI();
            ToggleStartup(Settings.RunOnStartup);

            Saved = true;
            DialogResult = true;
            Close();
        }

        private void btnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private bool CheckStartupStatus()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(
                    @"Software\Microsoft\Windows\CurrentVersion\Run", false);
                return key?.GetValue("SysMonBar") != null;
            }
            catch { return false; }
        }

        private void ToggleStartup(bool enable)
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(
                    @"Software\Microsoft\Windows\CurrentVersion\Run", true);
                if (key == null) return;

                if (enable)
                {
                    var exePath = System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName;
                    if (exePath != null)
                        key.SetValue("SysMonBar", $"\"{exePath}\"");
                }
                else
                {
                    key.DeleteValue("SysMonBar", false);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Startup toggle error: {ex.Message}");
            }
        }
    }
}
