@echo off
echo ================================================
echo SysMonBar Build Script (PyInstaller)
echo ================================================
echo.

cd /d "%~dp0"

echo Activating virtual environment...
call .venv311\Scripts\activate.bat

echo.
echo Installing dependencies...
pip install pillow

echo.
echo Cleaning old builds...
rmdir /s /q build 2>nul
rmdir /s /q dist 2>nul
del *.spec 2>nul

echo.
echo Building with PyInstaller...
pyinstaller --onedir --windowed --name=SysMonBar --add-data="icon.png;." --add-data="LibreHardwareMonitorLib.dll;." --hidden-import=clr_loader --hidden-import=pythonnet --collect-all pythonnet --collect-all clr_loader main.py

echo.
echo ================================================
if exist "dist\SysMonBar\SysMonBar.exe" (
    echo BUILD SUCCESSFUL!
    echo.
    echo Your app is in: dist\SysMonBar\
    echo.
    echo NOTE: Distribute the entire SysMonBar folder
    explorer "dist\SysMonBar"
) else (
    echo Build may have failed. Check for errors above.
)
echo ================================================
pause
