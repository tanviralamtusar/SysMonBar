using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Management;
using System.Threading;
using LibreHardwareMonitor.Hardware;
using System.Runtime.Versioning;


namespace SysMonBar
{
    public class HardwareStats
    {
        public float CpuUsage { get; set; }
        public float RamUsedGb { get; set; }
        public float RamTotalGb { get; set; }
        public float GpuUsage { get; set; }
        public float PowerWatts { get; set; }
        public float CpuPower { get; set; }
        public float GpuPower { get; set; }
        public float CpuTemp { get; set; }
        public float GpuTemp { get; set; }
        public float CombinedTemp => Math.Max(CpuTemp, GpuTemp);
        public double NetUp { get; set; }
        public double NetDown { get; set; }
    }

    [SupportedOSPlatform("windows")]
    public class UpdateVisitor : IVisitor

    {
        public void VisitComputer(IComputer computer) => computer.Traverse(this);

        public void VisitHardware(IHardware hardware)
        {
            hardware.Update();
            foreach (IHardware subHardware in hardware.SubHardware)
                subHardware.Accept(this);
        }

        public void VisitSensor(ISensor sensor) { }

        public void VisitParameter(IParameter parameter) { }
    }

    [SupportedOSPlatform("windows")]
    public class HardwareMonitor : IDisposable

    {
        // Legacy counters
        private PerformanceCounter? _cpuCounter;
        private PerformanceCounter? _ramCounter;
        private float _totalRamGb;
        private List<PerformanceCounter> _netDownCounters = new();
        private List<PerformanceCounter> _netUpCounters = new();
        private List<PerformanceCounter> _gpuCounters = new();
        private DateTime _lastCounterUpdate = DateTime.MinValue;

        // LibreHardwareMonitor
        private Computer? _computer;
        
        // Background WMI fields
        private float _lastCpuTemp;
        private bool _isDisposed;
        private Thread? _wmiThread;
        private readonly UpdateVisitor _visitor = new();

        public HardwareMonitor()
        {
            // Initialize Legacy
            try { _cpuCounter = new PerformanceCounter("Processor Information", "% Processor Time", "_Total"); } catch { }
            try { _ramCounter = new PerformanceCounter("Memory", "Available MBytes"); } catch { }

            try
            {
                using var searcher = new ManagementObjectSearcher("SELECT TotalPhysicalMemory FROM Win32_ComputerSystem");
                foreach (ManagementObject obj in searcher.Get())
                {
                    if (ulong.TryParse(obj["TotalPhysicalMemory"]?.ToString(), out ulong bytes))
                    {
                        _totalRamGb = bytes / (1024f * 1024f * 1024f);
                        break;
                    }
                }
            }
            catch { _totalRamGb = 16f; }

            UpdateDynamicCounters();

            // Initialize LibreHardwareMonitor
            try
            {
                _computer = new Computer
                {
                    IsCpuEnabled = true,
                    IsGpuEnabled = true,
                    IsMemoryEnabled = true,
                    IsNetworkEnabled = false, // Use PerformanceCounters instead
                    IsMotherboardEnabled = false // Very heavy, disabling for 20MB target
                };
                _computer.Open();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"LibreHardwareMonitor failed to initialize: {ex.Message}");
            }

            // Start background thread for WMI fallback
            _wmiThread = new Thread(WmiLoop)
            {
                IsBackground = true,
                Name = "WMIPollingThread"
            };
            _wmiThread.Start();
        }

        private void WmiLoop()
        {
            while (!_isDisposed)
            {
                try
                {
                    using var searcher = new ManagementObjectSearcher(@"root\WMI", "SELECT CurrentTemperature FROM MSAcpi_ThermalZoneTemperature");
                    float maxTemp = 0;
                    foreach (ManagementObject obj in searcher.Get())
                    {
                        if (float.TryParse(obj["CurrentTemperature"]?.ToString(), out float tempK))
                        {
                            float tempC = (tempK - 2732f) / 10f;
                            // Only accept 'sane' temperatures. Values like 17C are often static dummy values in BIOS.
                            if (tempC > maxTemp && tempC > 25 && tempC < 150)
                                maxTemp = tempC;
                        }
                    }
                    if (maxTemp > 25)
                    {
                        _lastCpuTemp = maxTemp;
                        // Don't set _lastGpuTemp here; ACPI zones are almost never GPU-related.
                    }
                }
                catch { }

                for (int i = 0; i < 50 && !_isDisposed; i++) // Increased sleep to 5s
                {
                    Thread.Sleep(100);
                }
            }
        }

        private void UpdateDynamicCounters()
        {
            if ((DateTime.Now - _lastCounterUpdate).TotalSeconds < 10)
                return;

            _lastCounterUpdate = DateTime.Now;

            try
            {
                var netCategory = new PerformanceCounterCategory("Network Interface");
                var instances = netCategory.GetInstanceNames();
                
                DisposeCounters(_netDownCounters);
                DisposeCounters(_netUpCounters);
                _netDownCounters.Clear();
                _netUpCounters.Clear();

                foreach (var instance in instances)
                {
                    if (instance.Contains("Loopback", StringComparison.OrdinalIgnoreCase)) continue;
                    try
                    {
                        _netDownCounters.Add(new PerformanceCounter("Network Interface", "Bytes Received/sec", instance));
                        _netUpCounters.Add(new PerformanceCounter("Network Interface", "Bytes Sent/sec", instance));
                    }
                    catch { }
                }
            }
            catch { }

            try
            {
                var gpuCategory = new PerformanceCounterCategory("GPU Engine");
                var instances = gpuCategory.GetInstanceNames();

                DisposeCounters(_gpuCounters);
                _gpuCounters.Clear();

                foreach (var instance in instances)
                {
                    if (instance.Contains("engtype_3D", StringComparison.OrdinalIgnoreCase))
                    {
                        try { _gpuCounters.Add(new PerformanceCounter("GPU Engine", "Utilization Percentage", instance)); } catch { }
                    }
                }
            }
            catch { }
        }

        private void DisposeCounters(List<PerformanceCounter> counters)
        {
            foreach (var c in counters)
            {
                try { c.Dispose(); } catch { }
            }
        }

        public HardwareStats GetStats()
        {
            UpdateDynamicCounters();
            var stats = new HardwareStats { RamTotalGb = _totalRamGb };


            if (_computer != null)
            {
                try
                {
                    _computer.Accept(_visitor);

                    // Track sensor priorities: higher = better/more reliable source
                    int cpuTempPriority = 0;
                    int gpuTempPriority = 0;
                    int cpuPowerPriority = 0;
                    int gpuPowerPriority = 0;
                    float ramAvailableGb = 0;

                    void ProcessHardware(IHardware hw)
                    {
                        foreach (ISensor sensor in hw.Sensors)
                        {
                            if (sensor.Value == null) continue;
                            string name = sensor.Name;
                            float value = sensor.Value.Value;

                            if (hw.HardwareType == HardwareType.Cpu)
                            {
                                if (sensor.SensorType == SensorType.Load && name == "CPU Total")
                                {
                                    stats.CpuUsage = value;
                                }
                                else if (sensor.SensorType == SensorType.Temperature)
                                {
                                    int priority = 0;
                                    if (name == "Core Average" || name == "CCDs Average (Tdie)") priority = 10;
                                    else if (name == "CPU Package" || name == "Package") priority = 9;
                                    else if (name == "Core (Tctl/Tdie)") priority = 8;
                                    else if (name == "Core (Tdie)") priority = 7;
                                    else if (name == "Core (Tctl)") priority = 6;
                                    else if (name == "CPU Cores") priority = 5;
                                    else if (name == "Core Max" || name == "CCDs Max (Tdie)") priority = 4;
                                    else if (name.StartsWith("Core #") || name.StartsWith("P-Core") || name.StartsWith("E-Core")) priority = 3;

                                    if (priority > cpuTempPriority && value > 0 && value < 150)
                                    {
                                        stats.CpuTemp = value;
                                        cpuTempPriority = priority;
                                    }
                                    else if (cpuTempPriority == 0 && (name.Contains("CPU") || name.Contains("Temperature")) && value > 0)
                                    {
                                        // Absolute last resort for LHM sensors
                                        stats.CpuTemp = value;
                                    }
                                }
                                else if (sensor.SensorType == SensorType.Power)
                                {
                                    int priority = 0;
                                    if (name == "CPU Package" || name == "Package") priority = 10;
                                    else if (name == "CPU Cores") priority = 5;

                                    if (priority > cpuPowerPriority)
                                    {
                                        stats.CpuPower = value;
                                        cpuPowerPriority = priority;
                                    }
                                }
                            }
                            else if (hw.HardwareType == HardwareType.GpuNvidia || hw.HardwareType == HardwareType.GpuAmd || hw.HardwareType == HardwareType.GpuIntel)
                            {
                                if (sensor.SensorType == SensorType.Load && (name.Contains("Core") || name.Contains("GPU Value")))
                                {
                                    stats.GpuUsage = value;
                                }
                                else if (sensor.SensorType == SensorType.Temperature)
                                {
                                    int priority = 0;
                                    if (name == "GPU Core" || name == "Core") priority = 10;
                                    else if (name == "GPU Hot Spot" || name == "Hot Spot") priority = 5;
                                    else if (name.Contains("Temperature") || name.Contains("GPU Value")) priority = 2; // Broad fallback

                                    if (priority > gpuTempPriority && value > 0 && value < 150)
                                    {
                                        stats.GpuTemp = value;
                                        gpuTempPriority = priority;
                                    }
                                }
                                else if (sensor.SensorType == SensorType.Power)
                                {
                                    int priority = 0;
                                    if (name == "GPU Package" || name == "Board Power") priority = 10;
                                    else if (name == "GPU Power" || name == "Total Power") priority = 8;
                                    else if (name == "GPU Core" || name == "Power Draw") priority = 5;

                                    if (priority > gpuPowerPriority)
                                    {
                                        stats.GpuPower = value;
                                        gpuPowerPriority = priority;
                                    }
                                }
                            }
                            else if (hw.HardwareType == HardwareType.Memory)
                            {
                                if (sensor.SensorType == SensorType.Data)
                                {
                                    if (name == "Memory Used") stats.RamUsedGb = value;
                                    else if (name == "Memory Available") ramAvailableGb = value;
                                }
                            }
                            else if (hw.HardwareType == HardwareType.Network)
                            {
                                if (sensor.SensorType == SensorType.Throughput)
                                {
                                    if (name.Contains("Upload")) stats.NetUp += value;
                                    else if (name.Contains("Download")) stats.NetDown += value;
                                }
                            }
                            else if (hw.HardwareType == HardwareType.Motherboard)
                            {
                                // Fallbacks for systems where CPU doesn't report itself
                                if (sensor.SensorType == SensorType.Temperature && name.Contains("CPU"))
                                {
                                    if (cpuTempPriority < 2)
                                    {
                                        stats.CpuTemp = value;
                                        cpuTempPriority = 2;
                                    }
                                }
                                else if (sensor.SensorType == SensorType.Power && (name.Contains("CPU") || name.Contains("Vcore")))
                                {
                                    if (cpuPowerPriority < 2)
                                    {
                                        stats.CpuPower = value;
                                        cpuPowerPriority = 2;
                                    }
                                }
                            }
                        }

                        foreach (IHardware subHardware in hw.SubHardware)
                        {
                            ProcessHardware(subHardware);
                        }
                    }

                    foreach (IHardware hardware in _computer.Hardware)
                    {
                        ProcessHardware(hardware);
                    }

                    if (stats.RamUsedGb > 0 && ramAvailableGb > 0)
                        stats.RamTotalGb = stats.RamUsedGb + ramAvailableGb;
                    
                    stats.PowerWatts = stats.CpuPower + stats.GpuPower;


                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"LibreHardwareMonitor update failed: {ex.Message}");
                }
            }

            // --- Fallbacks ---

            // CPU Fallback
            if (stats.CpuUsage <= 0 && _cpuCounter != null)
            {
                try { stats.CpuUsage = _cpuCounter.NextValue(); } catch { }
            }

            // RAM Fallback
            if (stats.RamUsedGb <= 0 && _ramCounter != null)
            {
                try 
                { 
                    float availMb = _ramCounter.NextValue();
                    stats.RamUsedGb = Math.Max(0, _totalRamGb - (availMb / 1024f));
                } catch { }
            }

            // Network Fallback
            if (stats.NetDown <= 0 && stats.NetUp <= 0)
            {
                try
                {
                    stats.NetDown = _netDownCounters.Sum(c => { try { return c.NextValue(); } catch { return 0; } });
                    stats.NetUp = _netUpCounters.Sum(c => { try { return c.NextValue(); } catch { return 0; } });
                } catch { }
            }

            // GPU Fallback
            if (stats.GpuUsage <= 0)
            {
                try
                {
                    stats.GpuUsage = _gpuCounters.Sum(c => { try { return c.NextValue(); } catch { return 0; } });
                    if (stats.GpuUsage > 100) stats.GpuUsage = 100;
                } catch { }
            }

            // Temperature Fallback
            if (stats.CpuTemp <= 0) stats.CpuTemp = _lastCpuTemp;
            
            // Estimation Fallback (Last resort)
            if (stats.PowerWatts < 1)
                stats.PowerWatts = 15f + (stats.CpuUsage / 100f) * 50f + (stats.GpuUsage / 100f) * 30f;

            if (stats.CpuTemp < 1)
                stats.CpuTemp = 42f + (stats.CpuUsage / 100f) * 45f;
                
            if (stats.GpuTemp < 1)
                stats.GpuTemp = 45f + (stats.GpuUsage / 100f) * 40f;

            return stats;
        }

        public void Dispose()
        {
            _isDisposed = true;
            try { _computer?.Close(); } catch { }
            try { _cpuCounter?.Dispose(); } catch { }
            try { _ramCounter?.Dispose(); } catch { }
            DisposeCounters(_netDownCounters);
            DisposeCounters(_netUpCounters);
            DisposeCounters(_gpuCounters);
        }
    }
}
