import sys
import winreg
from PyQt6.QtWidgets import (QDialog, QVBoxLayout, QCheckBox, QLabel, 
                             QColorDialog, QPushButton, QHBoxLayout, QComboBox, QGroupBox, QGridLayout, QMessageBox)
from PyQt6.QtCore import Qt

class SettingsDialog(QDialog):
    def __init__(self, current_settings, on_save_callback=None):
        super().__init__()
        self.setWindowTitle("SysMonBar Settings")
        self.setWindowFlags(self.windowFlags() & ~Qt.WindowType.WindowContextHelpButtonHint)
        self.settings = dict(current_settings) if current_settings else {}
        self.on_save_callback = on_save_callback
        self.toggles = {}
        self.color_buttons = {}
        self.display_combos = {}
        self.result_settings = None
        self.init_ui()

    def init_ui(self):
        layout = QVBoxLayout()
        
        modules_group = QGroupBox("Modules")
        grid = QGridLayout()
        
        grid.addWidget(QLabel("Show"), 0, 0)
        grid.addWidget(QLabel("Color"), 0, 1)
        grid.addWidget(QLabel("Type"), 0, 2)
        
        modules = [
            ("CPU", "show_cpu", "color_cpu", "#3498db", "display_cpu"),
            ("RAM", "show_ram", "color_ram", "#9b59b6", "display_ram"),
            ("GPU", "show_gpu", "color_gpu", "#2ecc71", "display_gpu"),
            ("Network", "show_net", "color_net", "#1abc9c", "display_net"),
            ("Power", "show_power", "color_power", "#e67e22", "display_power"),
            ("Temp", "show_temp", "color_temp", "#e74c3c", "display_temp"),
        ]

        for i, (name, show_key, color_key, default_col, display_key) in enumerate(modules):
            row = i + 1
            
            cb = QCheckBox(name)
            cb.setChecked(bool(self.settings.get(show_key, True)))
            self.toggles[show_key] = cb
            grid.addWidget(cb, row, 0)
            
            col_btn = QPushButton()
            current_col = str(self.settings.get(color_key, default_col))
            col_btn.setStyleSheet(f"background-color: {current_col}; border: 1px solid gray; min-width: 40px;")
            col_btn.clicked.connect(lambda checked, b=col_btn, k=color_key: self.pick_color(b, k))
            self.color_buttons[color_key] = {"btn": col_btn, "value": current_col}
            grid.addWidget(col_btn, row, 1)
            
            if name in ["Power", "Temp"]:
                lbl = QLabel("text")
                grid.addWidget(lbl, row, 2)
            else:
                display_combo = QComboBox()
                display_combo.addItems(["bar", "graph"])
                display_combo.setCurrentText(str(self.settings.get(display_key, "bar")))
                self.display_combos[display_key] = display_combo
                grid.addWidget(display_combo, row, 2)

        modules_group.setLayout(grid)
        layout.addWidget(modules_group)

        general_group = QGroupBox("General")
        gen_layout = QVBoxLayout()
        
        units_layout = QHBoxLayout()
        units_layout.addWidget(QLabel("RAM Unit:"))
        self.unit_combo = QComboBox()
        self.unit_combo.addItems(["GB", "MB"])
        self.unit_combo.setCurrentText(str(self.settings.get("unit", "GB")))
        units_layout.addWidget(self.unit_combo)
        units_layout.addStretch()
        gen_layout.addLayout(units_layout)
        
        net_layout = QHBoxLayout()
        net_layout.addWidget(QLabel("Network:"))
        self.net_unit_combo = QComboBox()
        self.net_unit_combo.addItems(["kbps", "mbps", "KBps", "MBps"])
        self.net_unit_combo.setCurrentText(str(self.settings.get("net_unit", "kbps")))
        net_layout.addWidget(self.net_unit_combo)
        net_layout.addStretch()
        gen_layout.addLayout(net_layout)
        
        self.startup_cb = QCheckBox("Run on Startup")
        self.startup_cb.setChecked(self.check_startup_status())
        gen_layout.addWidget(self.startup_cb)
        
        general_group.setLayout(gen_layout)
        layout.addWidget(general_group)

        btn_layout = QHBoxLayout()
        save_btn = QPushButton("Save && Apply")
        save_btn.clicked.connect(self.on_save)
        cancel_btn = QPushButton("Cancel")
        cancel_btn.clicked.connect(self.on_cancel)
        btn_layout.addWidget(save_btn)
        btn_layout.addWidget(cancel_btn)
        layout.addLayout(btn_layout)

        self.setLayout(layout)
        self.setMinimumWidth(350)

    def pick_color(self, btn, key):
        try:
            color = QColorDialog.getColor()
            if color.isValid():
                hex_col = color.name()
                btn.setStyleSheet(f"background-color: {hex_col}; border: 1px solid gray; min-width: 40px;")
                self.color_buttons[key]["value"] = hex_col
        except Exception as e:
            print(f"Color pick error: {e}")

    def check_startup_status(self):
        try:
            key = winreg.OpenKey(winreg.HKEY_CURRENT_USER, r"Software\Microsoft\Windows\CurrentVersion\Run", 0, winreg.KEY_READ)
            winreg.QueryValueEx(key, "SysMonBar")
            winreg.CloseKey(key)
            return True
        except:
            return False

    def toggle_startup(self, enable):
        try:
            key = winreg.OpenKey(winreg.HKEY_CURRENT_USER, r"Software\Microsoft\Windows\CurrentVersion\Run", 0, winreg.KEY_ALL_ACCESS)
            if enable:
                import os
                path = f'"{sys.executable}" "{os.path.abspath(sys.argv[0])}"'
                winreg.SetValueEx(key, "SysMonBar", 0, winreg.REG_SZ, path)
            else:
                try:
                    winreg.DeleteValue(key, "SysMonBar")
                except:
                    pass
            winreg.CloseKey(key)
        except Exception as e:
            print(f"Startup toggle error: {e}")

    def on_cancel(self):
        self.result_settings = None
        self.hide()

    def on_save(self):
        try:
            new_settings = {}
            
            for key, cb in self.toggles.items():
                new_settings[key] = cb.isChecked()
                
            for key, data in self.color_buttons.items():
                new_settings[key] = data["value"]
            
            for key, combo in self.display_combos.items():
                new_settings[key] = combo.currentText()
            
            new_settings["unit"] = self.unit_combo.currentText()
            new_settings["net_unit"] = self.net_unit_combo.currentText()
            
            self.toggle_startup(self.startup_cb.isChecked())
            
            self.result_settings = new_settings
            
            # Call callback if provided
            if self.on_save_callback:
                self.on_save_callback(new_settings)
            
            QMessageBox.information(self, "Settings", "✓ Settings Saved!")
            self.hide()
            
        except Exception as e:
            QMessageBox.critical(self, "Error", f"Failed to save: {e}")
            print(f"Save error: {e}")
            import traceback
            traceback.print_exc()
    
    def get_result(self):
        return self.result_settings
