@echo off
chcp 65001 >nul
echo ========================================
echo   VoiceBoard Build Script
echo ========================================
echo.

:: Degiskenleri ayarla
set SOLUTION_DIR=%~dp0
set PUBLISH_DIR=%SOLUTION_DIR%bin\Release\net8.0-windows\publish
set APO_DIR=%SOLUTION_DIR%APO\bin\x64\Release
set INSTALLER_DIR=%SOLUTION_DIR%Installer

:: .NET SDK kontrolu
echo [1/4] .NET SDK kontrol ediliyor...
dotnet --version >nul 2>&1
if errorlevel 1 (
    echo HATA: .NET SDK bulunamadi!
    echo https://dotnet.microsoft.com/download adresinden indirin.
    pause
    exit /b 1
)
echo      .NET SDK bulundu!
echo.

:: C# projesini derle ve yayinla
echo [2/4] C# projesi derleniyor...
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o "%PUBLISH_DIR%"
if errorlevel 1 (
    echo HATA: C# projesi derlenemedi!
    pause
    exit /b 1
)
echo      C# projesi derlendi!
echo.

:: APO DLL derleme (Visual Studio gerekli)
echo [3/4] APO DLL kontrol ediliyor...
if exist "%APO_DIR%\VoiceBoardAPO.dll" (
    echo      APO DLL mevcut!
) else (
    echo      APO DLL bulunamadi.
    echo      Visual Studio ile APO\VoiceBoardAPO.vcxproj dosyasini derleyin.
    echo      veya devam etmek icin herhangi bir tusa basin...
    pause >nul
)
echo.

:: Installer olustur (Inno Setup gerekli)
echo [4/4] Installer olusturuluyor...
if exist "C:\Program Files (x86)\Inno Setup 6\ISCC.exe" (
    "C:\Program Files (x86)\Inno Setup 6\ISCC.exe" "%INSTALLER_DIR%\VoiceBoard.iss"
    if errorlevel 1 (
        echo HATA: Installer olusturulamadi!
    ) else (
        echo      Installer olusturuldu: Installer\Output\VoiceBoard_Setup.exe
    )
) else (
    echo      Inno Setup bulunamadi.
    echo      https://jrsoftware.org/isinfo.php adresinden indirin.
)
echo.

echo ========================================
echo   Build tamamlandi!
echo ========================================
echo.
echo Dosyalar:
echo   - Uygulama: %PUBLISH_DIR%\VoiceBoard.exe
echo   - Installer: %INSTALLER_DIR%\Output\VoiceBoard_Setup.exe
echo.
pause
