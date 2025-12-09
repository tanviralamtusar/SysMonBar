# SysMonBar 📊

A lightweight Windows system monitor that sits on your taskbar, showing real-time stats for CPU, RAM, GPU, Network, Power, and Temperature.

![SysMonBar Preview](icon.png)

## Features

- 🖥️ **CPU Usage** - Real-time CPU load with bar/graph display
- 💾 **RAM Usage** - Memory consumption in GB or MB
- 🎮 **GPU Usage** - GPU load via LibreHardwareMonitor
- 🌐 **Network** - Upload/Download speeds (kbps, mbps, KB/s, MB/s)
- ⚡ **Power** - CPU/GPU power consumption in watts
- 🌡️ **Temperature** - Combined CPU/GPU temperature
- 📈 **Analytics** - Track power consumption over 24h, 7d, 30d with charts
- 💰 **Cost Calculator** - Estimate electricity costs in 70+ currencies

## Requirements

- Windows 10/11
- Python 3.11 (for development)
- [LibreHardwareMonitorLib.dll](https://github.com/LibreHardwareMonitor/LibreHardwareMonitor) - for hardware monitoring

## Installation

### From Release (Recommended)
1. Download the latest release
2. Extract to a folder
3. Run `SysMonBar.exe` as Administrator

### From Source
```bash
# Clone the repository
git clone https://github.com/YOUR_USERNAME/SysMonBar.git
cd SysMonBar

# Create virtual environment (Python 3.11)
py -3.11 -m venv .venv
.venv\Scripts\activate

# Install dependencies
pip install PyQt6 psutil wmi pywin32 pythonnet

# Run the app
python main.py
```

## Usage

- **Right-click** on the bar to access Settings or Analytics
- **System tray icon** also provides quick access
- Run as **Administrator** for full sensor access (power, temps)

## Settings

- Toggle visibility for each metric
- Customize colors
- Choose bar or graph display
- Select network speed units
- Enable/disable run on startup

## Building EXE

```bash
pip install pyinstaller
pyinstaller --onefile --windowed --icon=icon.png --name=SysMonBar --add-data="icon.png;." --add-data="LibreHardwareMonitorLib.dll;." main.py
```

The EXE will be in the `dist` folder.

## Files

| File | Description |
|------|-------------|
| `main.py` | Main application and UI |
| `monitor.py` | System monitoring thread |
| `settings.py` | Settings dialog |
| `analytics.py` | SQLite database for power data |
| `analytics_window.py` | Analytics charts and stats |
| `LibreHardwareMonitorLib.dll` | Hardware monitoring library |

## License

MIT License

## Credits

- [LibreHardwareMonitor](https://github.com/LibreHardwareMonitor/LibreHardwareMonitor) for hardware sensor access
- [PyQt6](https://www.riverbankcomputing.com/software/pyqt/) for the GUI framework
