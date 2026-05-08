using System;
using System.Diagnostics;
using System.IO;
using Microsoft.Win32;
using System.Runtime.Versioning;

namespace SysMonBar
{
    [SupportedOSPlatform("windows")]
    public static class StartupManager
    {
        private const string TaskName = "SysMonBarStartup";

        /// <summary>
        /// Checks if the startup task is registered in Task Scheduler.
        /// Also checks legacy registry for migration purposes.
        /// </summary>
        public static bool IsEnabled()
        {
            return IsTaskRegistered() || IsRegistrySet();
        }

        public static bool IsTaskRegistered()
        {
            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = "schtasks.exe",
                    Arguments = $"/query /tn \"{TaskName}\"",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };

                using var process = Process.Start(startInfo);
                process?.WaitForExit();
                return process?.ExitCode == 0;
            }
            catch { return false; }
        }

        private static bool IsRegistrySet()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", false);
                return key?.GetValue("SysMonBar") != null;
            }
            catch { return false; }
        }

        public static void Toggle(bool enable)
        {
            // Always clean up legacy registry entry
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true);
                if (key?.GetValue("SysMonBar") != null)
                {
                    key.DeleteValue("SysMonBar", false);
                }
            }
            catch { }

            if (enable)
            {
                RegisterTask();
            }
            else
            {
                UnregisterTask();
            }
        }

        private static void RegisterTask()
        {
            try
            {
                var exePath = Process.GetCurrentProcess().MainModule?.FileName;
                if (exePath == null) return;

                // Create task: 
                // /sc onlogon: Run at logon
                // /tn: Task name
                // /tr: Task run (path to exe)
                // /rl highest: Run with highest privileges (needed for admin app)
                // /f: Force creation (overwrite existing)
                var arguments = $"/create /tn \"{TaskName}\" /tr \"\\\"{exePath}\\\"\" /sc onlogon /rl highest /f";
                
                var startInfo = new ProcessStartInfo
                {
                    FileName = "schtasks.exe",
                    Arguments = arguments,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var process = Process.Start(startInfo);
                process?.WaitForExit();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to register task: {ex.Message}");
            }
        }

        private static void UnregisterTask()
        {
            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = "schtasks.exe",
                    Arguments = $"/delete /tn \"{TaskName}\" /f",
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var process = Process.Start(startInfo);
                process?.WaitForExit();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to unregister task: {ex.Message}");
            }
        }
    }
}
