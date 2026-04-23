using System;
using System.IO;
using System.Threading;
using LibreHardwareMonitor.Hardware;

try
{
    var computer = new Computer
    {
        IsCpuEnabled = true,
        IsGpuEnabled = true,
        IsMemoryEnabled = true,
        IsMotherboardEnabled = true,
        IsControllerEnabled = true
    };
    computer.Open();

    string outPath = @"d:\Coding\PC Usage Meter\sensor_dump2.txt";
    using var writer = new StreamWriter(outPath);

    // Warmup updates
    for (int i = 0; i < 3; i++)
    {
        foreach (var hw in computer.Hardware)
            hw.Update();
        Thread.Sleep(1000);
    }

    void Dump(IHardware hw, int indent = 0)
    {
        hw.Update();
        var pad = new string(' ', indent * 2);
        writer.WriteLine($"{pad}=== [{hw.HardwareType}] {hw.Name} ===");

        foreach (var s in hw.Sensors)
        {
            if (s.SensorType == SensorType.Temperature || s.SensorType == SensorType.Power)
                writer.WriteLine($"{pad}  {s.SensorType,-14} | {s.Name,-30} | {s.Value}");
        }

        foreach (var sub in hw.SubHardware)
            Dump(sub, indent + 1);
    }

    foreach (var hw in computer.Hardware)
        Dump(hw);

    computer.Close();
}
catch (Exception ex)
{
    File.WriteAllText(@"d:\Coding\PC Usage Meter\sensor_dump_error.txt", ex.ToString());
}
