# SysMonBar 📊 - Ultra-Lightweight System Monitor

**SysMonBar** is a sleek, professional-grade Windows system monitor designed to live discreetly on your taskbar. It provides real-time insights into your hardware performance while consuming almost zero resources.

![SysMonBar Icon](icon.png)

## 🚀 Key Features

- 🖥️ **CPU Usage** - Real-time load monitoring.
- 💾 **RAM Consumption** - Track memory usage in GB.
- 🎮 **GPU Performance** - Monitor load and temperatures.
- 🌐 **Network Throughput** - Live Upload/Download speeds.
- ⚡ **Power Draw** - Real-time wattage (Watts) for CPU and GPU.
- 🌡️ **Thermals** - Stay on top of CPU and GPU temperatures.
- 📊 **Analytics** - View historical usage and power cost estimation.
- 🍃 **Ultra-Lightweight** - Optimized to run at just **20-30MB RAM**.

---

## 📥 Installation (GitHub Release)

If you just want to use the app without building it from source, follow these steps:

1.  Go to the **[Releases](https://github.com/tanviralamtusar/SysMonBar/releases)** page.
2.  Download the latest `SysMonBar_Installer.exe`.
3.  Run the installer. 
    > **Note:** The app requires **Administrator Privileges** to read hardware sensors and performance counters.
4.  Once installed, the bar will appear at the bottom of your screen, and you'll find the icon in your **System Tray** (near the clock).

---

## ⚙️ Configuration

- **Right-click** the SysMonBar icon in the system tray to access **Settings** or **Analytics**.
- **Settings**: Change colors, toggle metrics, and adjust transparency.
- **Analytics**: View power consumption graphs and set your local electricity rate to estimate monthly costs.

---

## 🛠️ Development (For Builders)

If you want to contribute or build the app yourself:

### Prerequisites
- Windows 10/11
- .NET 8.0 SDK

### Build & Run
```powershell
# Clone the repository
git clone https://github.com/tanviralamtusar/SysMonBar.git
cd SysMonBar

# Build the project
dotnet build

# Run (as Administrator)
dotnet run
```

### Self-Contained Release Build
To create the single-file executable used in the installer:
```powershell
dotnet publish -c Release -o publish
```

---

## 🧠 Tech Stack

- **Framework:** .NET 8.0 / WPF
- **Hardware Engine:** [LibreHardwareMonitorLib](https://github.com/LibreHardwareMonitor/LibreHardwareMonitor)
- **Database:** Raw SQLite (Microsoft.Data.Sqlite) for high-performance logging.
- **Optimizations:** Workstation GC, Manual Working Set trimming, and native sensor polling.

## 📄 License
MIT License
