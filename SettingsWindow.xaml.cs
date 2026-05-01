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
        public string RamUnit { get; set; } = "GB";
        public string NetUnit { get; set; } = "kbps";
        public bool RunOnStartup { get; set; } = false;
        public string DisplayMode { get; set; } = "Graph";
        public double GraphWidth { get; set; } = 36;
    }

    public partial class SettingsWindow : Window
    {
        public AppSettings Settings { get; private set; }
        public bool Saved { get; private set; }

        public SettingsWindow(AppSettings current)
        {
            InitializeComponent();
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
