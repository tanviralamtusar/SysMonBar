from PyQt6.QtWidgets import (QDialog, QVBoxLayout, QHBoxLayout, QLabel, 
                             QPushButton, QComboBox, QGroupBox, QGridLayout,
                             QFrame, QScrollArea, QWidget, QDoubleSpinBox)
from PyQt6.QtCore import Qt, QSettings
from PyQt6.QtGui import QPainter, QColor, QPen, QFont
from analytics import get_analytics

# World currencies with symbols
CURRENCIES = [
    ("BDT", "৳", "Bangladeshi Taka"),
    ("USD", "$", "US Dollar"),
    ("EUR", "€", "Euro"),
    ("GBP", "£", "British Pound"),
    ("JPY", "¥", "Japanese Yen"),
    ("CNY", "¥", "Chinese Yuan"),
    ("INR", "₹", "Indian Rupee"),
    ("PKR", "₨", "Pakistani Rupee"),
    ("AED", "د.إ", "UAE Dirham"),
    ("SAR", "﷼", "Saudi Riyal"),
    ("AUD", "$", "Australian Dollar"),
    ("CAD", "$", "Canadian Dollar"),
    ("CHF", "Fr", "Swiss Franc"),
    ("HKD", "$", "Hong Kong Dollar"),
    ("SGD", "$", "Singapore Dollar"),
    ("MYR", "RM", "Malaysian Ringgit"),
    ("THB", "฿", "Thai Baht"),
    ("IDR", "Rp", "Indonesian Rupiah"),
    ("PHP", "₱", "Philippine Peso"),
    ("VND", "₫", "Vietnamese Dong"),
    ("KRW", "₩", "South Korean Won"),
    ("TWD", "NT$", "Taiwan Dollar"),
    ("RUB", "₽", "Russian Ruble"),
    ("TRY", "₺", "Turkish Lira"),
    ("ZAR", "R", "South African Rand"),
    ("BRL", "R$", "Brazilian Real"),
    ("MXN", "$", "Mexican Peso"),
    ("ARS", "$", "Argentine Peso"),
    ("CLP", "$", "Chilean Peso"),
    ("COP", "$", "Colombian Peso"),
    ("PEN", "S/", "Peruvian Sol"),
    ("NGN", "₦", "Nigerian Naira"),
    ("EGP", "£", "Egyptian Pound"),
    ("KES", "KSh", "Kenyan Shilling"),
    ("GHS", "₵", "Ghanaian Cedi"),
    ("MAD", "د.م.", "Moroccan Dirham"),
    ("ILS", "₪", "Israeli Shekel"),
    ("PLN", "zł", "Polish Zloty"),
    ("CZK", "Kč", "Czech Koruna"),
    ("HUF", "Ft", "Hungarian Forint"),
    ("SEK", "kr", "Swedish Krona"),
    ("NOK", "kr", "Norwegian Krone"),
    ("DKK", "kr", "Danish Krone"),
    ("NZD", "$", "New Zealand Dollar"),
    ("UAH", "₴", "Ukrainian Hryvnia"),
    ("RON", "lei", "Romanian Leu"),
    ("BGN", "лв", "Bulgarian Lev"),
    ("HRK", "kn", "Croatian Kuna"),
    ("RSD", "дин", "Serbian Dinar"),
    ("ISK", "kr", "Icelandic Krona"),
    ("LKR", "Rs", "Sri Lankan Rupee"),
    ("NPR", "Rs", "Nepalese Rupee"),
    ("MMK", "K", "Myanmar Kyat"),
    ("KHR", "៛", "Cambodian Riel"),
    ("LAK", "₭", "Lao Kip"),
    ("MNT", "₮", "Mongolian Tugrik"),
    ("KZT", "₸", "Kazakhstani Tenge"),
    ("UZS", "лв", "Uzbekistan Som"),
    ("QAR", "﷼", "Qatari Riyal"),
    ("KWD", "د.ك", "Kuwaiti Dinar"),
    ("BHD", "ب.د", "Bahraini Dinar"),
    ("OMR", "﷼", "Omani Rial"),
    ("JOD", "د.ا", "Jordanian Dinar"),
    ("LBP", "ل.ل", "Lebanese Pound"),
    ("IQD", "ع.د", "Iraqi Dinar"),
    ("IRR", "﷼", "Iranian Rial"),
    ("AFN", "؋", "Afghan Afghani"),
]


class ChartWidget(QWidget):
    """Simple bar/line chart widget"""
    
    def __init__(self):
        super().__init__()
        self.data = []
        self.chart_type = "bar"
        self.color = QColor("#3498db")
        self.setMinimumHeight(150)
        self.setMinimumWidth(400)
    
    def set_data(self, data, chart_type="bar", color="#3498db"):
        self.data = data
        self.chart_type = chart_type
        self.color = QColor(color)
        self.update()
    
    def paintEvent(self, event):
        if not self.data:
            return
            
        painter = QPainter(self)
        painter.setRenderHint(QPainter.RenderHint.Antialiasing)
        
        painter.fillRect(self.rect(), QColor("#1e1e1e"))
        
        w = self.width() - 60
        h = self.height() - 40
        x_offset = 50
        y_offset = 10
        
        if not self.data:
            return
        
        values = [d[1] for d in self.data]
        max_val = max(values) if values and max(values) > 0 else 100
        
        if self.chart_type == "bar":
            bar_width = max(2, w // len(self.data) - 2)
            
            for i, (label, val) in enumerate(self.data):
                bar_height = int((val / max_val) * h) if max_val > 0 else 0
                x = x_offset + i * (bar_width + 2)
                y = y_offset + h - bar_height
                
                painter.fillRect(x, y, bar_width, bar_height, self.color)
                
        else:
            pen = QPen(self.color, 2)
            painter.setPen(pen)
            
            for i in range(len(self.data) - 1):
                x1 = x_offset + int(i * w / (len(self.data) - 1))
                y1 = y_offset + h - int((self.data[i][1] / max_val) * h) if max_val > 0 else y_offset + h
                x2 = x_offset + int((i + 1) * w / (len(self.data) - 1))
                y2 = y_offset + h - int((self.data[i + 1][1] / max_val) * h) if max_val > 0 else y_offset + h
                
                painter.drawLine(x1, y1, x2, y2)
        
        painter.setPen(QPen(QColor("#666"), 1))
        painter.drawLine(x_offset, y_offset, x_offset, y_offset + h)
        painter.drawLine(x_offset, y_offset + h, x_offset + w, y_offset + h)
        
        painter.setPen(QColor("#aaa"))
        font = QFont("Segoe UI", 7)
        painter.setFont(font)
        painter.drawText(5, y_offset + 10, f"{int(max_val)}W")
        painter.drawText(5, y_offset + h, "0W")


class AnalyticsWindow(QDialog):
    """Analytics window for power consumption"""
    
    def __init__(self, parent=None):
        super().__init__(parent)
        self.setWindowTitle("Power Analytics")
        self.setMinimumSize(520, 520)
        self.analytics = get_analytics()
        self.settings = QSettings("MyCompany", "SysMonBar")
        self.init_ui()
        self.load_data("24h")
    
    def init_ui(self):
        layout = QVBoxLayout()
        
        # Period selector
        period_layout = QHBoxLayout()
        period_layout.addWidget(QLabel("Period:"))
        self.period_combo = QComboBox()
        self.period_combo.addItems(["24 Hours", "7 Days", "30 Days", "All Time"])
        self.period_combo.currentTextChanged.connect(self.on_period_changed)
        period_layout.addWidget(self.period_combo)
        period_layout.addStretch()
        
        refresh_btn = QPushButton("🔄 Refresh")
        refresh_btn.clicked.connect(lambda: self.load_data(self.get_period_key()))
        period_layout.addWidget(refresh_btn)
        
        layout.addLayout(period_layout)
        
        # Stats group
        stats_group = QGroupBox("Power Consumption Stats")
        stats_layout = QGridLayout()
        
        self.stat_labels = {}
        stats = [
            ("total_kwh", "Total kWh:", "0.00 kWh"),
            ("avg_power", "Average:", "0 W"),
            ("max_power", "Max:", "0 W"),
            ("min_power", "Min:", "0 W"),
            ("duration", "Duration:", "0 hours"),
            ("readings", "Readings:", "0"),
        ]
        
        for i, (key, label, default) in enumerate(stats):
            row = i // 2
            col = (i % 2) * 2
            
            lbl = QLabel(label)
            lbl.setStyleSheet("font-weight: bold;")
            stats_layout.addWidget(lbl, row, col)
            
            val_lbl = QLabel(default)
            val_lbl.setStyleSheet("color: #3498db; font-size: 14px;")
            self.stat_labels[key] = val_lbl
            stats_layout.addWidget(val_lbl, row, col + 1)
        
        stats_group.setLayout(stats_layout)
        layout.addWidget(stats_group)
        
        # Chart
        chart_group = QGroupBox("Power Over Time")
        chart_layout = QVBoxLayout()
        self.chart = ChartWidget()
        chart_layout.addWidget(self.chart)
        chart_group.setLayout(chart_layout)
        layout.addWidget(chart_group)
        
        # Cost calculation group
        cost_group = QGroupBox("Electricity Cost Calculator")
        cost_layout = QGridLayout()
        
        # Currency selector
        cost_layout.addWidget(QLabel("Currency:"), 0, 0)
        self.currency_combo = QComboBox()
        for code, symbol, name in CURRENCIES:
            self.currency_combo.addItem(f"{symbol} {code} - {name}", (code, symbol))
        # Load saved currency
        saved_currency = self.settings.value("analytics_currency", "BDT", type=str)
        for i, (code, _, _) in enumerate(CURRENCIES):
            if code == saved_currency:
                self.currency_combo.setCurrentIndex(i)
                break
        self.currency_combo.currentIndexChanged.connect(self.update_cost)
        cost_layout.addWidget(self.currency_combo, 0, 1, 1, 2)
        
        # Rate input
        cost_layout.addWidget(QLabel("Rate per kWh:"), 1, 0)
        self.rate_spinbox = QDoubleSpinBox()
        self.rate_spinbox.setRange(0.001, 10000)
        self.rate_spinbox.setDecimals(2)
        self.rate_spinbox.setValue(self.settings.value("analytics_rate", 8.0, type=float))
        self.rate_spinbox.valueChanged.connect(self.update_cost)
        cost_layout.addWidget(self.rate_spinbox, 1, 1)
        
        save_rate_btn = QPushButton("Save")
        save_rate_btn.clicked.connect(self.save_rate_settings)
        cost_layout.addWidget(save_rate_btn, 1, 2)
        
        # Estimated cost display
        cost_layout.addWidget(QLabel("Estimated Cost:"), 2, 0)
        self.cost_label = QLabel("৳0.00")
        self.cost_label.setStyleSheet("color: #e67e22; font-size: 18px; font-weight: bold;")
        cost_layout.addWidget(self.cost_label, 2, 1, 1, 2)
        
        cost_group.setLayout(cost_layout)
        layout.addWidget(cost_group)
        
        # Close button
        close_btn = QPushButton("Close")
        close_btn.clicked.connect(self.close)
        layout.addWidget(close_btn)
        
        self.setLayout(layout)
        self.setStyleSheet("""
            QDialog {
                background-color: #2b2b2b;
                color: #fff;
            }
            QGroupBox {
                border: 1px solid #444;
                border-radius: 5px;
                margin-top: 10px;
                padding-top: 10px;
                color: #fff;
            }
            QGroupBox::title {
                subcontrol-origin: margin;
                left: 10px;
                padding: 0 5px;
            }
            QPushButton {
                background-color: #3498db;
                color: white;
                border: none;
                padding: 8px 16px;
                border-radius: 4px;
            }
            QPushButton:hover {
                background-color: #2980b9;
            }
            QComboBox, QDoubleSpinBox {
                background-color: #333;
                color: white;
                border: 1px solid #555;
                padding: 5px;
            }
            QLabel {
                color: #ddd;
            }
        """)
    
    def save_rate_settings(self):
        """Save currency and rate settings"""
        data = self.currency_combo.currentData()
        if data:
            self.settings.setValue("analytics_currency", data[0])
        self.settings.setValue("analytics_rate", self.rate_spinbox.value())
    
    def get_period_key(self):
        text = self.period_combo.currentText()
        if "24" in text:
            return "24h"
        elif "7" in text:
            return "7d"
        elif "30" in text:
            return "30d"
        else:
            return "all"
    
    def on_period_changed(self, text):
        self.load_data(self.get_period_key())
    
    def update_cost(self):
        """Update the cost display based on current settings"""
        try:
            kwh = float(self.stat_labels["total_kwh"].text().split()[0])
        except:
            kwh = 0
        
        rate = self.rate_spinbox.value()
        data = self.currency_combo.currentData()
        symbol = data[1] if data else "$"
        
        cost = kwh * rate
        self.cost_label.setText(f"{symbol}{cost:.2f}")
    
    def load_data(self, period):
        """Load and display data for the selected period"""
        if period == "24h":
            hours = 24
        elif period == "7d":
            hours = 7 * 24
        elif period == "30d":
            hours = 30 * 24
        else:
            hours = 365 * 24
        
        stats = self.analytics.get_stats(hours)
        
        self.stat_labels["total_kwh"].setText(f"{stats['kwh']:.2f} kWh")
        self.stat_labels["avg_power"].setText(f"{stats['avg_power']:.1f} W")
        self.stat_labels["max_power"].setText(f"{stats['max_power']:.1f} W")
        self.stat_labels["min_power"].setText(f"{stats['min_power']:.1f} W")
        self.stat_labels["duration"].setText(f"{stats['hours']:.1f} hours")
        self.stat_labels["readings"].setText(f"{stats['count']}")
        
        # Update cost
        self.update_cost()
        
        # Get chart data
        if period in ["24h"]:
            chart_data = self.analytics.get_hourly_average(hours)
            self.chart.set_data([(d[0][-5:-3] + "h", d[1] or 0) for d in chart_data], "line", "#3498db")
        else:
            chart_data = self.analytics.get_daily_average(hours // 24 if hours < 365 * 24 else 90)
            self.chart.set_data([(d[0][-5:], d[1] or 0) for d in chart_data], "bar", "#2ecc71")
