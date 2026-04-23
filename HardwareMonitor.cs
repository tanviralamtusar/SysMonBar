using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Management;
using System.Threading;

namespace SysMonBar
{
    public class HardwareStats
    {
        public float CpuUsage { get; set; }
        public float RamUsedGb { get; set; }
        public float RamTotalGb { get; set; }
        public float GpuUsage { get; set; }
        public float PowerWatts { get; set; }
        public float CpuTemp { get; set; }
        public float GpuTemp { get; set; }
        public float CombinedTemp => Math.Max(CpuTemp, GpuTemp);
        public double NetUp { get; set; }
        public double NetDown { get; set; }
    }

    public class HardwareMonitor : IDisposable
    {
        private PerformanceCounter? _cpuCounter;
        private PerformanceCounter? _ramCounter;
        private float _totalRamGb;
        
        private List<PerformanceCounter> _netDownCounters = new();
        private List<PerformanceCounter> _netUpCounters = new();
        private List<PerformanceCounter> _gpuCounters = new();

        private DateTime _lastCounterUpdate = DateTime.MinValue;

        // Background WMI fields
        private float _lastCpuTemp;
        private float _lastGpuTemp;
        private bool _isDisposed;
        private Thread? _wmiThread;

        public HardwareMonitor()
        {
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
            catch { _totalRamGb = 16f; } // Fallback

            UpdateDynamicCounters();

            // Start background thread for WMI to avoid freezing UI
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
                            if (tempC > maxTemp && tempC < 150)
                                maxTemp = tempC;
                        }
                    }
                    if (maxTemp > 0)
                    {
                        _lastCpuTemp = maxTemp;
                        _lastGpuTemp = maxTemp; // Usually APUs share temp
                    }
                }
                catch { }

                // Wait before next poll
                for (int i = 0; i < 20 && !_isDisposed; i++)
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

            // Update Network Counters
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

            // Update GPU Counters
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

            // CPU
            try { if (_cpuCounter != null) stats.CpuUsage = _cpuCounter.NextValue(); } catch { }

            // RAM
            try 
            { 
                if (_ramCounter != null) 
                {
                    float availMb = _ramCounter.NextValue();
                    float availGb = availMb / 1024f;
                    stats.RamUsedGb = Math.Max(0, _totalRamGb - availGb);
                }
            } 
            catch { }

            // Network
            try
            {
                stats.NetDown = _netDownCounters.Sum(c => { try { return c.NextValue(); } catch { return 0; } });
                stats.NetUp = _netUpCounters.Sum(c => { try { return c.NextValue(); } catch { return 0; } });
            }
            catch { }

            // GPU
            try
            {
                stats.GpuUsage = _gpuCounters.Sum(c => { try { return c.NextValue(); } catch { return 0; } });
                if (stats.GpuUsage > 100) stats.GpuUsage = 100;
            }
            catch { }

            // Temperature (from WMI thread)
            stats.CpuTemp = _lastCpuTemp;
            stats.GpuTemp = _lastGpuTemp;

            // Fallback estimation
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
            try { _cpuCounter?.Dispose(); } catch { }
            try { _ramCounter?.Dispose(); } catch { }
            DisposeCounters(_netDownCounters);
            DisposeCounters(_netUpCounters);
            DisposeCounters(_gpuCounters);
        }
    }
}
