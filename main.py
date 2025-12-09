import sys
import os
from PyQt6.QtWidgets import (QApplication, QMainWindow, QWidget, QHBoxLayout, QVBoxLayout,
                             QLabel, QSystemTrayIcon, QMenu, QProgressBar, QSizePolicy)
from PyQt6.QtCore import Qt, pyqtSignal, QSettings, QTimer
from PyQt6.QtGui import QAction, QColor, QIcon, QPainter, QPen, QFont
from monitor import SystemMonitor
from settings import SettingsDialog
from analytics_window import AnalyticsWindow
from collections import deque


class MetricWidget(QWidget):
    """Widget that shows EITHER a bar OR a graph"""
    
    def __init__(self, name, color, display_type="bar"):
        super().__init__()
        self.name = name
        self.color = color
        self.display_type = display_type
        self.visible_flag = True
        self.current_value = 0
        self.max_value = 100
        self.text = ""
        self.history = deque(maxlen=30)
        
        self.setFixedWidth(12)
        self.setFixedHeight(30)
        self.setToolTip(name)
        
    def set_display_type(self, dtype):
        self.display_type = dtype
        self.update()
        
    def update_data(self, value, max_val=100, text=None):
        if not self.visible_flag:
            self.hide()
            return
        self.show()
        
        self.current_value = value
        self.max_value = max_val if max_val > 0 else 100
        self.text = text or ""
        
        pct = min(100, int((value / self.max_value) * 100)) if self.max_value > 0 else 0
        self.history.append(pct)
        
        if text:
            self.setToolTip(f"{self.name}: {text}")
        
        self.update()
        
    def paintEvent(self, event):
        painter = QPainter(self)
        painter.setRenderHint(QPainter.RenderHint.Antialiasing)
        
        painter.fillRect(self.rect(), QColor("#2b2b2b"))
        
        w = self.width()
        h = self.height()
        color = QColor(self.color)
        
        if self.display_type == "graph":
            if len(self.history) >= 2:
                pen = QPen(color, 1.5)
                painter.setPen(pen)
                
                points = list(self.history)
                num = len(points)
                x_step = w / 29
                
                for i in range(num - 1):
                    x1 = i * x_step
                    y1 = h - (points[i] / 100.0 * h)
                    x2 = (i + 1) * x_step
                    y2 = h - (points[i + 1] / 100.0 * h)
                    painter.drawLine(int(x1), int(y1), int(x2), int(y2))
        else:
            pct = min(100, int((self.current_value / self.max_value) * 100)) if self.max_value > 0 else 0
            bar_height = int(h * pct / 100)
            bar_width = 8
            bar_x = (w - bar_width) // 2
            
            painter.fillRect(bar_x, h - bar_height, bar_width, bar_height, color)


class TextWidget(QWidget):
    """Text-only widget for power/temp display"""
    
    def __init__(self, name, color):
        super().__init__()
        self.name = name
        self.color = color
        self.visible_flag = True
        self.display_text = "0"
        
        self.setFixedWidth(35)
        self.setFixedHeight(30)
        self.setToolTip(name)
        
    def update_data(self, value, max_val=100, text=None):
        if not self.visible_flag:
            self.hide()
            return
        self.show()
        
        self.display_text = text or f"{int(value)}"
        self.setToolTip(f"{self.name}: {self.display_text}")
        self.update()
        
    def set_display_type(self, dtype):
        pass  # Always text
        
    def paintEvent(self, event):
        painter = QPainter(self)
        painter.setRenderHint(QPainter.RenderHint.Antialiasing)
        
        painter.fillRect(self.rect(), QColor("#2b2b2b"))
        
        painter.setPen(QColor(self.color))
        font = QFont("Segoe UI", 8, QFont.Weight.Bold)
        painter.setFont(font)
        painter.drawText(self.rect(), Qt.AlignmentFlag.AlignCenter, self.display_text)


class SysMonBar(QMainWindow):
    def __init__(self):
        super().__init__()
        
        self.settings_store = QSettings("MyCompany", "SysMonBar")
        self.load_settings()
        self.init_ui()
        
        self.monitor_thread = SystemMonitor()
        self.monitor_thread.stats_updated.connect(self.update_ui)
        self.monitor_thread.start()

    def load_settings(self):
        self.settings = {
            "show_cpu": self.settings_store.value("show_cpu", True, type=bool),
            "show_ram": self.settings_store.value("show_ram", True, type=bool),
            "show_gpu": self.settings_store.value("show_gpu", True, type=bool),
            "show_net": self.settings_store.value("show_net", True, type=bool),
            "show_power": self.settings_store.value("show_power", True, type=bool),
            "show_temp": self.settings_store.value("show_temp", True, type=bool),
            "color_cpu": self.settings_store.value("color_cpu", "#3498db", type=str),
            "color_ram": self.settings_store.value("color_ram", "#9b59b6", type=str),
            "color_gpu": self.settings_store.value("color_gpu", "#2ecc71", type=str),
            "color_net": self.settings_store.value("color_net", "#1abc9c", type=str),
            "color_power": self.settings_store.value("color_power", "#e67e22", type=str),
            "color_temp": self.settings_store.value("color_temp", "#e74c3c", type=str),
            "unit": self.settings_store.value("unit", "GB", type=str),
            "net_unit": self.settings_store.value("net_unit", "kbps", type=str),
            "display_cpu": self.settings_store.value("display_cpu", "bar", type=str),
            "display_ram": self.settings_store.value("display_ram", "bar", type=str),
            "display_gpu": self.settings_store.value("display_gpu", "bar", type=str),
            "display_net": self.settings_store.value("display_net", "bar", type=str),
        }

    def init_ui(self):
        self.setWindowFlags(
            Qt.WindowType.FramelessWindowHint | 
            Qt.WindowType.WindowStaysOnTopHint |
            Qt.WindowType.ToolTip
        )
        self.setAttribute(Qt.WidgetAttribute.WA_TranslucentBackground)
        
        self.top_timer = QTimer(self)
        self.top_timer.timeout.connect(self.raise_)
        self.top_timer.start(500)
        
        self.update_position()

        self.central_widget = QWidget()
        self.central_widget.setStyleSheet("background-color: transparent;")
        self.setCentralWidget(self.central_widget)
        
        self.central_widget.setContextMenuPolicy(Qt.ContextMenuPolicy.CustomContextMenu)
        self.central_widget.customContextMenuRequested.connect(self.show_context_menu)
        
        self.layout = QHBoxLayout(self.central_widget)
        self.layout.setContentsMargins(2, 2, 2, 2)
        self.layout.setSpacing(2)
        
        # Metric widgets
        self.cpu_widget = MetricWidget("CPU", self.settings["color_cpu"], self.settings["display_cpu"])
        self.ram_widget = MetricWidget("RAM", self.settings["color_ram"], self.settings["display_ram"])
        self.gpu_widget = MetricWidget("GPU", self.settings["color_gpu"], self.settings["display_gpu"])
        self.net_widget = MetricWidget("NET", self.settings["color_net"], self.settings["display_net"])
        
        # Text widgets for power and temp
        self.power_widget = TextWidget("Power", self.settings["color_power"])
        self.temp_widget = TextWidget("Temp", self.settings["color_temp"])
        
        self.layout.addWidget(self.cpu_widget)
        self.layout.addWidget(self.ram_widget)
        self.layout.addWidget(self.gpu_widget)
        self.layout.addWidget(self.net_widget)
        self.layout.addWidget(self.power_widget)
        self.layout.addWidget(self.temp_widget)
        
        self.apply_visibility()

        self.tray_icon = QSystemTrayIcon(self)
        self.tray_icon.setToolTip("SysMonBar")
        
        icon_path = os.path.join(os.path.dirname(__file__), "icon.png")
        if os.path.exists(icon_path):
            self.tray_icon.setIcon(QIcon(icon_path))
            self.setWindowIcon(QIcon(icon_path))
        
        tray_menu = QMenu()
        tray_menu.addAction("📊 Analytics", self.open_analytics)
        tray_menu.addAction("Settings", self.open_settings)
        tray_menu.addSeparator()
        tray_menu.addAction("Exit", self.close_app)
        self.tray_icon.setContextMenu(tray_menu)
        self.tray_icon.show()

    def update_position(self):
        screen = QApplication.primaryScreen()
        geo = screen.geometry()
        bar_h = 38
        bar_w = 130  # Wider for temp widget
        self.setGeometry(geo.width() - bar_w - 300, geo.height() - bar_h - 2, bar_w, bar_h)

    def update_ui(self, stats):
        try:
            self.cpu_widget.update_data(stats['cpu'], 100, f"{stats['cpu']:.0f}%")
            
            unit = self.settings["unit"]
            if unit == "GB":
                ram_text = f"{stats['ram_used_gb']:.1f}/{stats['ram_total_gb']:.1f}GB"
            else:
                ram_text = f"{stats['ram_used_gb']*1024:.0f}/{stats['ram_total_gb']*1024:.0f}MB"
            self.ram_widget.update_data(stats['ram_used_gb'], stats['ram_total_gb'], ram_text)
            
            self.gpu_widget.update_data(stats['gpu'], 100, f"{stats['gpu']:.0f}%")
            
            net_unit = self.settings.get("net_unit", "kbps")
            down = stats['net_down']
            up = stats['net_up']
            
            def fmt(b, u):
                if u == "kbps": return f"{b*8/1024:.1f}kbps"
                elif u == "mbps": return f"{b*8/1024/1024:.2f}mbps"
                elif u == "KBps": return f"{b/1024:.1f}KB/s"
                elif u == "MBps": return f"{b/1024/1024:.2f}MB/s"
                return f"{b}B/s"
            
            net_text = f"↓{fmt(down, net_unit)} ↑{fmt(up, net_unit)}"
            self.net_widget.update_data(down + up, 10*1024*1024, net_text)
            
            # Power
            power = stats.get('power', 0) or 0
            self.power_widget.update_data(power, 150, f"{int(power)}W")
            
            # Temperature (combined - shows max of CPU/GPU)
            temp = stats.get('temp', 0) or 0
            cpu_temp = stats.get('cpu_temp', 0) or 0
            gpu_temp = stats.get('gpu_temp', 0) or 0
            temp_text = f"{int(temp)}°C"
            tooltip = f"CPU: {int(cpu_temp)}°C | GPU: {int(gpu_temp)}°C"
            self.temp_widget.update_data(temp, 100, temp_text)
            self.temp_widget.setToolTip(tooltip)
            
        except Exception as e:
            print(f"Update error: {e}")

    def apply_visibility(self):
        self.cpu_widget.visible_flag = self.settings.get("show_cpu", True)
        self.ram_widget.visible_flag = self.settings.get("show_ram", True)
        self.gpu_widget.visible_flag = self.settings.get("show_gpu", True)
        self.net_widget.visible_flag = self.settings.get("show_net", True)
        self.power_widget.visible_flag = self.settings.get("show_power", True)
        self.temp_widget.visible_flag = self.settings.get("show_temp", True)
        
        for w in [self.cpu_widget, self.ram_widget, self.gpu_widget, self.net_widget, self.power_widget, self.temp_widget]:
            if not w.visible_flag:
                w.hide()
            else:
                w.show()

    def update_display_types(self):
        self.cpu_widget.set_display_type(self.settings.get("display_cpu", "bar"))
        self.ram_widget.set_display_type(self.settings.get("display_ram", "bar"))
        self.gpu_widget.set_display_type(self.settings.get("display_gpu", "bar"))
        self.net_widget.set_display_type(self.settings.get("display_net", "bar"))

    def update_colors(self):
        self.cpu_widget.color = self.settings.get("color_cpu", "#3498db")
        self.ram_widget.color = self.settings.get("color_ram", "#9b59b6")
        self.gpu_widget.color = self.settings.get("color_gpu", "#2ecc71")
        self.net_widget.color = self.settings.get("color_net", "#1abc9c")
        self.power_widget.color = self.settings.get("color_power", "#e67e22")
        self.temp_widget.color = self.settings.get("color_temp", "#e74c3c")

    def open_settings(self):
        dlg = SettingsDialog(self.settings, self.apply_settings)
        dlg.exec()

    def apply_settings(self, new_settings):
        try:
            self.settings.update(new_settings)
            for k, v in new_settings.items():
                self.settings_store.setValue(k, v)
            
            self.apply_visibility()
            self.update_colors()
            self.update_display_types()
            
        except Exception as e:
            print(f"Apply settings error: {e}")
            import traceback
            traceback.print_exc()
        
    def show_context_menu(self, pos):
        menu = QMenu()
        menu.addAction("📊 Analytics", self.open_analytics)
        menu.addAction("⚙️ Settings", self.open_settings)
        menu.addSeparator()
        menu.addAction("❌ Exit", self.close_app)
        menu.exec(self.central_widget.mapToGlobal(pos))

    def open_analytics(self):
        dlg = AnalyticsWindow(self)
        dlg.exec()

    def close_app(self):
        self.monitor_thread.stop()
        self.monitor_thread.wait()
        QApplication.quit()


if __name__ == "__main__":
    app = QApplication(sys.argv)
    window = SysMonBar()
    window.show()
    sys.exit(app.exec())
