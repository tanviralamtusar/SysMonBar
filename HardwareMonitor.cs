using System;
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
        }

        private void UpdateSubHardware(IHardware hardware)
        {
            foreach (var sub in hardware.SubHardware)
            {
                sub.Update();
                UpdateSubHardware(sub);
            }
        }

        public HardwareStats GetStats()
        {
            var stats = new HardwareStats();
            float cpuPower = 0;
            float gpuPower = 0;

            foreach (var hardware in _computer.Hardware)
            {
                hardware.Update();
                UpdateSubHardware(hardware);

                var hwType = hardware.HardwareType;

                // ── CPU ──
                if (hwType == HardwareType.Cpu)
                {
                    foreach (var sensor in hardware.Sensors)
                    {
                        var val = sensor.Value ?? 0;
                        if (val <= 0) continue;

                        if (sensor.SensorType == SensorType.Load &&
                            sensor.Name.Contains("Total", StringComparison.OrdinalIgnoreCase))
                        {
                            stats.CpuUsage = val;
                        }
                        else if (sensor.SensorType == SensorType.Power)
                        {
                            // Take the highest power reading (Package > individual cores)
                            if (sensor.Name.Contains("Package", StringComparison.OrdinalIgnoreCase) || val > cpuPower)
                                cpuPower = val;
                        }
                        else if (sensor.SensorType == SensorType.Temperature)
                        {
                            // Prefer Tctl/Tdie (AMD) or Package (Intel), fallback to any
                            if (sensor.Name.Contains("Tctl", StringComparison.OrdinalIgnoreCase) ||
                                sensor.Name.Contains("Package", StringComparison.OrdinalIgnoreCase) ||
                                stats.CpuTemp == 0)
                            {
                                stats.CpuTemp = val;
                            }
                        }
                    }
                }

                // ── GPU ──
                if (hwType == HardwareType.GpuNvidia ||
                    hwType == HardwareType.GpuAmd ||
                    hwType == HardwareType.GpuIntel)
                {
                    foreach (var sensor in hardware.Sensors)
                    {
                        var val = sensor.Value ?? 0;
                        if (val <= 0) continue;

                        if (sensor.SensorType == SensorType.Load)
                        {
                            // Prefer "D3D 3D" (actual usage), fallback to "GPU Core"
                            if (sensor.Name.Contains("D3D 3D", StringComparison.OrdinalIgnoreCase))
                                stats.GpuUsage = val;
                            else if (sensor.Name.Contains("Core", StringComparison.OrdinalIgnoreCase) && stats.GpuUsage == 0)
                                stats.GpuUsage = val;
                        }
                        else if (sensor.SensorType == SensorType.Temperature &&
                                 sensor.Name.Contains("Core", StringComparison.OrdinalIgnoreCase))
                        {
                            stats.GpuTemp = val;
                        }
                        else if (sensor.SensorType == SensorType.Power)
                        {
                            if (val > gpuPower) gpuPower = val;
                        }
                    }
                }

                // ── RAM ──
                if (hwType == HardwareType.Memory)
                {
                    float used = 0, available = 0;
                    foreach (var sensor in hardware.Sensors)
                    {
                        if (sensor.SensorType == SensorType.Data)
                        {
                            if (sensor.Name.Contains("Used", StringComparison.OrdinalIgnoreCase))
                                used = sensor.Value ?? 0;
                            else if (sensor.Name.Contains("Available", StringComparison.OrdinalIgnoreCase))
                                available = sensor.Value ?? 0;
                        }
                    }
                    stats.RamUsedGb = used;
                    stats.RamTotalGb = used + available;
                }

                // ── Network ──
                if (hwType == HardwareType.Network)
                {
                    foreach (var sensor in hardware.Sensors)
                    {
                        if (sensor.SensorType == SensorType.Throughput)
                        {
                            var val = sensor.Value ?? 0;
                            if (sensor.Name.Contains("Upload", StringComparison.OrdinalIgnoreCase))
                                stats.NetUp += val;
                            else if (sensor.Name.Contains("Download", StringComparison.OrdinalIgnoreCase))
                                stats.NetDown += val;
                        }
                    }
                }
            }

            // Combine power
            stats.PowerWatts = cpuPower + gpuPower;

            // Fallback: estimate power from CPU usage if sensors report 0
            if (stats.PowerWatts < 1)
                stats.PowerWatts = 15 + (stats.CpuUsage / 100f) * 50f;

            return stats;
        }

        public void Dispose()
        {
            _computer.Close();
        }
    }
}
