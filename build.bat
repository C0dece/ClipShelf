@echo off
chcp 65001 >nul 2>&1

set CSC=C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe

if not exist "dist" mkdir dist

echo Compiling ClipShelf...
set DOTNET_DIR=C:\Windows\Microsoft.NET\Framework64\v4.0.30319
if not exist "%DOTNET_DIR%\csc.exe" set DOTNET_DIR=C:\Windows\Microsoft.NET\Framework\v4.0.30319

%CSC% /target:winexe /optimize+ /platform:anycpu /reference:"%DOTNET_DIR%\System.Xaml.dll" /reference:"%DOTNET_DIR%\WPF\WindowsBase.dll" /reference:"%DOTNET_DIR%\WPF\PresentationCore.dll" /win32icon:assets\app.ico /out:dist\ClipShelf.exe src\*.cs

if %errorlevel% neq 0 (
    echo Build FAILED.
    exit /b 1
)

echo.
echo Build OK: dist\ClipShelf.exe
echo.

:: Provide PasteImageAsFile.exe for backwards compatibility
copy /Y dist\ClipShelf.exe dist\PasteImageAsFile.exe >nul 2>&1

:: Copy icon next to exe for runtime loading
copy /Y assets\app.ico dist\app.ico >nul
copy /Y assets\app_icon.png dist\app_icon.png >nul 2>&1

echo Done.
