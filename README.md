# SysMonBar 📊 - System Monitor Bar

A lightweight Windows system monitor that sits on your taskbar, showing real-time stats for CPU, RAM, GPU, Network, Power, and Temperature.

Built natively in **C# / WPF** for maximum performance and minimal resource usage.

![SysMonBar Preview](icon.png)

## Features

- 🖥️ **CPU Usage** - Real-time CPU load with bar display
- 💾 **RAM Usage** - Memory consumption in GB
- 🎮 **GPU Usage** - GPU load via LibreHardwareMonitor
- 🌐 **Network** - Upload/Download throughput
- ⚡ **Power** - CPU/GPU power consumption in watts
- 🌡️ **Temperature** - Combined CPU/GPU temperature
- 🔔 **System Tray** - Runs quietly with tray icon and context menu

## Requirements

- Windows 10/11
- .NET 8.0 SDK (for development)
- Run as **Administrator** for full sensor access

## Build & Run

```bash
# Clone the repository
git clone https://github.com/YOUR_USERNAME/SysMonBar.git
cd SysMonBar

# Build
dotnet build

# Run (requires Administrator)
dotnet run
```

## Publish as EXE

```bash
dotnet publish -c Release -r win-x64 --self-contained -p:PublishSingleFile=true
```

The EXE will be in `bin/Release/net8.0-windows/win-x64/publish/`.

## Project Structure

| File | Description |
|------|-------------|
| `MainWindow.xaml/.cs` | Main transparent bar UI |
| `MetricControl.xaml/.cs` | Reusable bar widget for metrics |
| `HardwareMonitor.cs` | Hardware sensor reading via LibreHardwareMonitor |
| `App.xaml/.cs` | Application entry, system tray icon |
| `app.manifest` | Admin privilege request |

## Tech Stack

- **Language:** C# 12
- **Framework:** .NET 8.0 / WPF
- **Hardware:** LibreHardwareMonitorLib (NuGet)
- **Database:** Entity Framework Core + SQLite (for analytics)

## License

MIT License

## Credits

- [LibreHardwareMonitor](https://github.com/LibreHardwareMonitor/LibreHardwareMonitor) for hardware sensor access
