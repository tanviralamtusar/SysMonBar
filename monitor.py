import time
import psutil
import pythoncom
import os
import ctypes
import sys
from PyQt6.QtCore import QThread, pyqtSignal

def is_admin():
    try:
        return ctypes.windll.shell32.IsUserAnAdmin()
    except:
        return False

LHM_AVAILABLE = False
Computer = None

try:
    import clr
    dll_path = os.path.join(os.path.dirname(__file__), "LibreHardwareMonitorLib.dll")
    if os.path.exists(dll_path):
        clr.AddReference(dll_path)
        from LibreHardwareMonitor.Hardware import Computer as LHMComputer
        Computer = LHMComputer
        LHM_AVAILABLE = True
        print("✓ LibreHardwareMonitor DLL loaded!")
        if is_admin():
            print("✓ Running as Administrator - full sensor access!")
        else:
            print("⚠ Not running as admin - some sensors may show 0")
    else:
        print(f"⚠ DLL not found at: {dll_path}")
except Exception as e:
    print(f"⚠ LibreHardwareMonitor not available: {e}")
    LHM_AVAILABLE = False


class SystemMonitor(QThread):
    stats_updated = pyqtSignal(dict)

    def __init__(self):
        super().__init__()
        self.running = True
        self.computer = None
        
        if LHM_AVAILABLE and Computer:
            try:
                self.computer = Computer()
                self.computer.IsCpuEnabled = True
                self.computer.IsGpuEnabled = True
                self.computer.IsMemoryEnabled = True
                self.computer.Open()
                print("✓ LibreHardwareMonitor Computer opened!")
            except Exception as e:
                print(f"⚠ LHM init error: {e}")
                self.computer = None

    def run(self):
        pythoncom.CoInitialize()
        
        try:
            from analytics import get_analytics
            self.analytics = get_analytics()
            self.last_log_time = time.time()
        except Exception as e:
            print(f"Analytics init error: {e}")
            self.analytics = None
            self.last_log_time = 0
        
        net_io_start = psutil.net_io_counters()
        last_time = time.time()

        while self.running:
            try:
                current_time = time.time()
                time_diff = current_time - last_time
                
                if time_diff < 1.0:
                    time.sleep(1.0 - time_diff)
                    current_time = time.time()
                    time_diff = current_time - last_time
                
                cpu_usage = psutil.cpu_percent(interval=None)
                ram = psutil.virtual_memory()
                
                net_io_now = psutil.net_io_counters()
                if time_diff > 0:
                    net_up = (net_io_now.bytes_sent - net_io_start.bytes_sent) / time_diff
                    net_down = (net_io_now.bytes_recv - net_io_start.bytes_recv) / time_diff
                else:
                    net_up = 0
                    net_down = 0
                    
                net_io_start = net_io_now
                last_time = current_time

                gpu_usage = 0
                power_watts = 0
                cpu_temp = 0
                gpu_temp = 0
                
                if self.computer and LHM_AVAILABLE:
                    try:
                        for hardware in self.computer.Hardware:
                            hardware.Update()
                            hw_type_str = str(hardware.HardwareType)
                            
                            try:
                                sensors = list(hardware.Sensors)
                            except:
                                sensors = []
                            
                            for sensor in sensors:
                                try:
                                    s_type = str(sensor.SensorType)
                                    name = str(sensor.Name)
                                    val = sensor.Value
                                    
                                    # CPU metrics
                                    if "Cpu" in hw_type_str:
                                        if "Power" in s_type and val and val > 0:
                                            if "Package" in name or val > power_watts:
                                                power_watts = float(val)
                                        if "Temperature" in s_type and val and val > 0:
                                            if cpu_temp == 0 or "Tctl" in name:
                                                cpu_temp = float(val)
                                    
                                    # GPU metrics
                                    if "Gpu" in hw_type_str:
                                        if "Load" in s_type and val is not None:
                                            if "D3D 3D" in name:
                                                gpu_usage = float(val)
                                            elif "GPU Core" in name and gpu_usage == 0:
                                                gpu_usage = float(val)
                                        if "Temperature" in s_type and val and val > 0:
                                            gpu_temp = float(val)
                                except:
                                    pass
                                    
                    except Exception as e:
                        pass  # Silently ignore LHM errors
                
                if power_watts == 0:
                    power_watts = 15 + (cpu_usage / 100) * 50

                if cpu_temp > 0 and gpu_temp > 0:
                    combined_temp = max(cpu_temp, gpu_temp)
                elif cpu_temp > 0:
                    combined_temp = cpu_temp
                elif gpu_temp > 0:
                    combined_temp = gpu_temp
                else:
                    combined_temp = 0

                if self.analytics and (time.time() - self.last_log_time) >= 60:
                    try:
                        self.analytics.log_reading(power_watts, cpu_temp, gpu_temp)
                        self.last_log_time = time.time()
                    except:
                        pass

                stats = {
                    "cpu": cpu_usage,
                    "ram_percent": ram.percent,
                    "ram_used_gb": ram.used / (1024**3),
                    "ram_total_gb": ram.total / (1024**3),
                    "net_up": net_up,
                    "net_down": net_down,
                    "gpu": gpu_usage,
                    "power": power_watts,
                    "cpu_temp": cpu_temp,
                    "gpu_temp": gpu_temp,
                    "temp": combined_temp,
                }

                self.stats_updated.emit(stats)
                
            except Exception as e:
                print(f"Monitor error: {e}")
                time.sleep(1)

        if self.computer:
            try:
                self.computer.Close()
            except:
                pass
        pythoncom.CoUninitialize()

    def stop(self):
        self.running = False
