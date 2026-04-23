using System;
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
    }

    public partial class SettingsWindow : Window
    {
        public AppSettings Settings { get; private set; }
        public bool Saved { get; private set; }

        public SettingsWindow(AppSettings current)
        {
            InitializeComponent();
            Settings = current;
            LoadSettings();
        }

        private void LoadSettings()
        {
            ChkCpu.IsChecked = Settings.ShowCpu;
            ChkRam.IsChecked = Settings.ShowRam;
            ChkGpu.IsChecked = Settings.ShowGpu;
            ChkNet.IsChecked = Settings.ShowNet;
            ChkPower.IsChecked = Settings.ShowPower;
            ChkTemp.IsChecked = Settings.ShowTemp;
            ChkStartup.IsChecked = CheckStartupStatus();

            // Set combo selections
            foreach (System.Windows.Controls.ComboBoxItem item in CmbUnit.Items)
                if (item.Content.ToString() == Settings.RamUnit) { CmbUnit.SelectedItem = item; break; }
            foreach (System.Windows.Controls.ComboBoxItem item in CmbNetUnit.Items)
                if (item.Content.ToString() == Settings.NetUnit) { CmbNetUnit.SelectedItem = item; break; }
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

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            Settings.ShowCpu = ChkCpu.IsChecked == true;
            Settings.ShowRam = ChkRam.IsChecked == true;
            Settings.ShowGpu = ChkGpu.IsChecked == true;
            Settings.ShowNet = ChkNet.IsChecked == true;
            Settings.ShowPower = ChkPower.IsChecked == true;
            Settings.ShowTemp = ChkTemp.IsChecked == true;

            var unitItem = CmbUnit.SelectedItem as System.Windows.Controls.ComboBoxItem;
            Settings.RamUnit = unitItem?.Content.ToString() ?? "GB";

            var netItem = CmbNetUnit.SelectedItem as System.Windows.Controls.ComboBoxItem;
            Settings.NetUnit = netItem?.Content.ToString() ?? "kbps";

            ToggleStartup(ChkStartup.IsChecked == true);

            Saved = true;
            DialogResult = true;
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
