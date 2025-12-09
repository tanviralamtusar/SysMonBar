import sqlite3
import os
import time
from datetime import datetime, timedelta

class PowerAnalytics:
    """Stores and retrieves power consumption data"""
    
    def __init__(self):
        db_path = os.path.join(os.path.dirname(__file__), "power_data.db")
        self.conn = sqlite3.connect(db_path, check_same_thread=False)
        self.create_tables()
    
    def create_tables(self):
        cursor = self.conn.cursor()
        cursor.execute('''
            CREATE TABLE IF NOT EXISTS power_readings (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                timestamp DATETIME DEFAULT CURRENT_TIMESTAMP,
                power_watts REAL,
                cpu_temp REAL,
                gpu_temp REAL
            )
        ''')
        self.conn.commit()
    
    def log_reading(self, power_watts, cpu_temp=0, gpu_temp=0):
        """Log a power reading"""
        cursor = self.conn.cursor()
        cursor.execute('''
            INSERT INTO power_readings (power_watts, cpu_temp, gpu_temp)
            VALUES (?, ?, ?)
        ''', (power_watts, cpu_temp, gpu_temp))
        self.conn.commit()
    
    def get_readings(self, hours=24):
        """Get readings for the last N hours"""
        cursor = self.conn.cursor()
        since = datetime.now() - timedelta(hours=hours)
        cursor.execute('''
            SELECT timestamp, power_watts, cpu_temp, gpu_temp
            FROM power_readings
            WHERE timestamp > ?
            ORDER BY timestamp
        ''', (since.strftime('%Y-%m-%d %H:%M:%S'),))
        return cursor.fetchall()
    
    def get_stats(self, hours=24):
        """Get statistics for the last N hours"""
        cursor = self.conn.cursor()
        since = datetime.now() - timedelta(hours=hours)
        
        cursor.execute('''
            SELECT 
                COUNT(*) as count,
                AVG(power_watts) as avg_power,
                MAX(power_watts) as max_power,
                MIN(power_watts) as min_power,
                SUM(power_watts) as total_power
            FROM power_readings
            WHERE timestamp > ?
        ''', (since.strftime('%Y-%m-%d %H:%M:%S'),))
        
        row = cursor.fetchone()
        if row and row[0] > 0:
            count, avg_power, max_power, min_power, total_power = row
            # Calculate kWh: (sum of watts) * (interval in hours) / count
            # Assuming 1 reading per minute, each reading represents ~1 minute
            hours_of_data = count / 60  # readings per minute -> hours
            kwh = (avg_power * hours_of_data) / 1000 if hours_of_data > 0 else 0
            
            return {
                "count": count,
                "avg_power": avg_power or 0,
                "max_power": max_power or 0,
                "min_power": min_power or 0,
                "kwh": kwh,
                "hours": hours_of_data
            }
        return {
            "count": 0, "avg_power": 0, "max_power": 0, 
            "min_power": 0, "kwh": 0, "hours": 0
        }
    
    def get_hourly_average(self, hours=24):
        """Get hourly average power for charting"""
        cursor = self.conn.cursor()
        since = datetime.now() - timedelta(hours=hours)
        
        cursor.execute('''
            SELECT 
                strftime('%Y-%m-%d %H:00', timestamp) as hour,
                AVG(power_watts) as avg_power
            FROM power_readings
            WHERE timestamp > ?
            GROUP BY hour
            ORDER BY hour
        ''', (since.strftime('%Y-%m-%d %H:%M:%S'),))
        
        return cursor.fetchall()
    
    def get_daily_average(self, days=30):
        """Get daily average power for charting"""
        cursor = self.conn.cursor()
        since = datetime.now() - timedelta(days=days)
        
        cursor.execute('''
            SELECT 
                DATE(timestamp) as day,
                AVG(power_watts) as avg_power,
                SUM(power_watts) / 60 / 1000 as kwh  -- assuming 1 reading/min
            FROM power_readings
            WHERE timestamp > ?
            GROUP BY day
            ORDER BY day
        ''', (since.strftime('%Y-%m-%d %H:%M:%S'),))
        
        return cursor.fetchall()
    
    def cleanup_old_data(self, days=90):
        """Delete data older than N days"""
        cursor = self.conn.cursor()
        cutoff = datetime.now() - timedelta(days=days)
        cursor.execute('''
            DELETE FROM power_readings
            WHERE timestamp < ?
        ''', (cutoff.strftime('%Y-%m-%d %H:%M:%S'),))
        self.conn.commit()
    
    def close(self):
        self.conn.close()


# Global instance
_analytics = None

def get_analytics():
    global _analytics
    if _analytics is None:
        _analytics = PowerAnalytics()
    return _analytics
