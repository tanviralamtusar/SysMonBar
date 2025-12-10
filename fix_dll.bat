@echo off
echo Copying required DLLs...

cd /d "%~dp0"

REM Copy Python DLL from venv
copy ".venv311\Scripts\python311.dll" "build_output\main.dist\" /Y 2>nul
copy ".venv311\Scripts\python3.dll" "build_output\main.dist\" /Y 2>nul

REM Also try from Python installation
for %%I in (python.exe) do (
    set PYTHON_PATH=%%~dp$PATH:I
)
if defined PYTHON_PATH (
    copy "%PYTHON_PATH%python311.dll" "build_output\main.dist\" /Y 2>nul
    copy "%PYTHON_PATH%python3.dll" "build_output\main.dist\" /Y 2>nul
)

REM Copy from system Python if available
copy "C:\Python311\python311.dll" "build_output\main.dist\" /Y 2>nul
copy "%LOCALAPPDATA%\Programs\Python\Python311\python311.dll" "build_output\main.dist\" /Y 2>nul
copy "%LOCALAPPDATA%\Programs\Python\Python311\python3.dll" "build_output\main.dist\" /Y 2>nul

echo.
echo Done! Try running main.exe now.
explorer "build_output\main.dist"
pause
