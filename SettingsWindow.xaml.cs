using System;
using System.IO;
using System.Text.Json;
using System.Windows;
using Microsoft.Web.WebView2.Core;
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
            InitializeAsync();
        }

        async void InitializeAsync()
        {
            await SettingsWebView.EnsureCoreWebView2Async(null);
            
            SettingsWebView.CoreWebView2.WebMessageReceived += CoreWebView2_WebMessageReceived;
            
            string htmlPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "SettingsWindow.html");
            if (File.Exists(htmlPath))
            {
                SettingsWebView.Source = new Uri(htmlPath);
                SettingsWebView.NavigationCompleted += SettingsWebView_NavigationCompleted;
            }
            else
            {
                System.Windows.MessageBox.Show("SettingsWindow.html not found in " + htmlPath, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SettingsWebView_NavigationCompleted(object sender, CoreWebView2NavigationCompletedEventArgs e)
        {
            if (e.IsSuccess)
            {
                var options = new JsonSerializerOptions { PropertyNamingPolicy = null };
                string json = JsonSerializer.Serialize(Settings, options);
                // The JS function loadSettings parses the string. Since ExecuteScriptAsync expects a JS string literal,
                // we should serialize it, then escape it properly or just pass it to a JS function.
                // An easier way is to just call `loadSettings('${json.Replace("'", "\\'")}')`.
                string script = $"loadSettings('{json.Replace("'", "\\'").Replace("\\", "\\\\")}');";
                SettingsWebView.ExecuteScriptAsync(script);
            }
        }

        private void CoreWebView2_WebMessageReceived(object sender, CoreWebView2WebMessageReceivedEventArgs e)
        {
            try
            {
                string message = e.TryGetWebMessageAsString();
                using var doc = JsonDocument.Parse(message);
                var root = doc.RootElement;
                
                string action = root.GetProperty("action").GetString();
                if (action == "CANCEL")
                {
                    DialogResult = false;
                    Close();
                }
                else if (action == "SAVE")
                {
                    var payload = root.GetProperty("payload");
                    var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                    Settings = JsonSerializer.Deserialize<AppSettings>(payload.GetRawText(), options);
                    
                    ToggleStartup(Settings.RunOnStartup);

                    Saved = true;
                    DialogResult = true;
                    Close();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to parse message: {ex.Message}");
            }
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
