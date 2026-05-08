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
                var result = RunSchtasks($"/query /tn \"{TaskName}\"");
                return result.ExitCode == 0;
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
            string? tempXml = null;
            try
            {
                var exePath = Process.GetCurrentProcess().MainModule?.FileName;
                if (exePath == null) return;

                var exeDir = Path.GetDirectoryName(exePath) ?? "";
                var userName = $"{Environment.UserDomainName}\\{Environment.UserName}";

                // Use XML task definition to avoid all quoting/escaping issues with schtasks CLI
                var taskXml = $@"<?xml version=""1.0"" encoding=""UTF-16""?>
<Task version=""1.2"" xmlns=""http://schemas.microsoft.com/windows/2004/02/mit/task"">
  <RegistrationInfo>
    <Description>Start SysMonBar at user logon</Description>
  </RegistrationInfo>
  <Triggers>
    <LogonTrigger>
      <Enabled>true</Enabled>
      <UserId>{SecurityElement(userName)}</UserId>
    </LogonTrigger>
  </Triggers>
  <Principals>
    <Principal id=""Author"">
      <UserId>{SecurityElement(userName)}</UserId>
      <LogonType>InteractiveToken</LogonType>
      <RunLevel>HighestAvailable</RunLevel>
    </Principal>
  </Principals>
  <Settings>
    <MultipleInstancesPolicy>IgnoreNew</MultipleInstancesPolicy>
    <DisallowStartIfOnBatteries>false</DisallowStartIfOnBatteries>
    <StopIfGoingOnBatteries>false</StopIfGoingOnBatteries>
    <AllowHardTerminate>true</AllowHardTerminate>
    <StartWhenAvailable>false</StartWhenAvailable>
    <RunOnlyIfNetworkAvailable>false</RunOnlyIfNetworkAvailable>
    <AllowStartOnDemand>true</AllowStartOnDemand>
    <Enabled>true</Enabled>
    <Hidden>false</Hidden>
    <RunOnlyIfIdle>false</RunOnlyIfIdle>
    <ExecutionTimeLimit>PT0S</ExecutionTimeLimit>
    <Priority>7</Priority>
  </Settings>
  <Actions Context=""Author"">
    <Exec>
      <Command>{SecurityElement(exePath)}</Command>
      <WorkingDirectory>{SecurityElement(exeDir)}</WorkingDirectory>
    </Exec>
  </Actions>
</Task>";

                tempXml = Path.Combine(Path.GetTempPath(), $"SysMonBar_task_{Guid.NewGuid():N}.xml");
                File.WriteAllText(tempXml, taskXml, System.Text.Encoding.Unicode);

                var result = RunSchtasks($"/create /tn \"{TaskName}\" /xml \"{tempXml}\" /f");

                if (result.ExitCode != 0)
                {
                    LogError($"schtasks /create failed (exit {result.ExitCode}): {result.Error}");
                }
            }
            catch (Exception ex)
            {
                LogError($"Failed to register task: {ex}");
            }
            finally
            {
                if (tempXml != null)
                {
                    try { File.Delete(tempXml); } catch { }
                }
            }
        }

        private static void UnregisterTask()
        {
            try
            {
                var result = RunSchtasks($"/delete /tn \"{TaskName}\" /f");
                if (result.ExitCode != 0 && !result.Error.Contains("cannot find", StringComparison.OrdinalIgnoreCase))
                {
                    LogError($"schtasks /delete failed (exit {result.ExitCode}): {result.Error}");
                }
            }
            catch (Exception ex)
            {
                LogError($"Failed to unregister task: {ex}");
            }
        }

        private static (int ExitCode, string Output, string Error) RunSchtasks(string arguments)
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "schtasks.exe",
                Arguments = arguments,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            using var process = Process.Start(startInfo)!;
            var output = process.StandardOutput.ReadToEnd();
            var error = process.StandardError.ReadToEnd();
            process.WaitForExit();

            return (process.ExitCode, output, error);
        }

        /// <summary>
        /// Escapes a string for safe inclusion in XML content.
        /// </summary>
        private static string SecurityElement(string value)
        {
            return System.Security.SecurityElement.Escape(value) ?? value;
        }

        private static void LogError(string message)
        {
            Debug.WriteLine(message);
            try
            {
                var logPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "startup_error.txt");
                File.AppendAllText(logPath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}{Environment.NewLine}");
            }
            catch { }
        }
    }
}
