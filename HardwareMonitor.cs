using System;
using System.Collections.Generic;
using System.Linq;
using LibreHardwareMonitor.Hardware;

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
        private readonly Computer _computer;
        private DateTime _lastTime;

        public HardwareMonitor()
        {
            _computer = new Computer
            {
                IsCpuEnabled = true,
                IsGpuEnabled = true,
                IsMemoryEnabled = true,
                IsNetworkEnabled = true
            };
            _computer.Open();
            _lastTime = DateTime.Now;
        }

        public HardwareStats GetStats()
        {
            var stats = new HardwareStats();
            var currentTime = DateTime.Now;
            var timeDiff = (currentTime - _lastTime).TotalSeconds;

            foreach (var hardware in _computer.Hardware)
            {
                hardware.Update();

                // CPU
                if (hardware.HardwareType == HardwareType.Cpu)
                {
                    foreach (var sensor in hardware.Sensors)
                    {
                        if (sensor.SensorType == SensorType.Load && sensor.Name == "CPU Total")
                            stats.CpuUsage = sensor.Value ?? 0;
                        else if (sensor.SensorType == SensorType.Power && (sensor.Name == "CPU Package" || sensor.Name == "Package"))
                            stats.PowerWatts += sensor.Value ?? 0;
                        else if (sensor.SensorType == SensorType.Temperature && (sensor.Name == "Core (Tctl/Tdie)" || sensor.Name == "CPU Package"))
                            stats.CpuTemp = sensor.Value ?? 0;
                    }
                }

                // GPU
                if (hardware.HardwareType == HardwareType.GpuNvidia || hardware.HardwareType == HardwareType.GpuAmd || hardware.HardwareType == HardwareType.GpuIntel)
                {
                    foreach (var sensor in hardware.Sensors)
                    {
                        if (sensor.SensorType == SensorType.Load && (sensor.Name == "GPU Core" || sensor.Name == "D3D 3D"))
                            stats.GpuUsage = sensor.Value ?? 0;
                        else if (sensor.SensorType == SensorType.Temperature && sensor.Name == "GPU Core")
                            stats.GpuTemp = sensor.Value ?? 0;
                        else if (sensor.SensorType == SensorType.Power && sensor.Name == "GPU Package")
                            stats.PowerWatts += sensor.Value ?? 0;
                    }
                }

                // RAM
                if (hardware.HardwareType == HardwareType.Memory)
                {
                    foreach (var sensor in hardware.Sensors)
                    {
                        if (sensor.Name == "Memory Used")
                            stats.RamUsedGb = sensor.Value ?? 0;
                        else if (sensor.Name == "Memory Available")
                        {
                            // We'll calculate total later or from other sensors
                        }
                    }
                    // Simple fallback for total RAM if needed, but usually we can get it from sensor
                    stats.RamTotalGb = stats.RamUsedGb + (hardware.Sensors.FirstOrDefault(s => s.Name == "Memory Available")?.Value ?? 0);
                }

                // Network
                if (hardware.HardwareType == HardwareType.Network)
                {
                    foreach (var sensor in hardware.Sensors)
                    {
                        if (sensor.SensorType == SensorType.Throughput)
                        {
                            if (sensor.Name == "Upload Speed") stats.NetUp += sensor.Value ?? 0;
                            if (sensor.Name == "Download Speed") stats.NetDown += sensor.Value ?? 0;
                        }
                    }
                }
            }

            _lastTime = currentTime;
            return stats;
        }

        public void Dispose()
        {
            _computer.Close();
        }
    }
}
