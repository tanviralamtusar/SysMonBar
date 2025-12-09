@echo off
echo ================================================
echo SysMonBar Build Script
echo ================================================
echo.

cd /d "%~dp0"

echo Activating virtual environment...
call .venv311\Scripts\activate.bat

echo.
echo Installing Nuitka and dependencies...
pip install nuitka ordered-set zstandard

echo.
echo Building with Nuitka (this may take 5-10 minutes)...
echo NOTE: Nuitka will download MinGW C compiler on first run
python -m nuitka --standalone --mingw64 --enable-plugin=pyqt6 --windows-console-mode=disable --windows-icon-from-ico=icon.ico --include-data-files=icon.ico=icon.ico --include-data-files=LibreHardwareMonitorLib.dll=LibreHardwareMonitorLib.dll --output-dir=build_output main.py

echo.
echo ================================================
if exist "build_output\main.dist\main.exe" (
    echo BUILD SUCCESSFUL!
    echo.
    echo Your app is in: build_output\main.dist\
    echo Rename main.exe to SysMonBar.exe if desired.
    echo.
    explorer "build_output\main.dist"
) else (
    echo Build may have failed. Check for errors above.
)
echo ================================================
pause
