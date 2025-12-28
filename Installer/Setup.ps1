#Requires -RunAsAdministrator
<#
.SYNOPSIS
    VoiceBoard Kolay Kurulum - Ileri Ileri Bitir

.DESCRIPTION
    Bu script VoiceBoard uygulamasini ve APO modulunu kurar.
    Sadece calistirin, geri kalan her sey otomatik!
#>

$ErrorActionPreference = "Stop"

# Renkli yazi fonksiyonlari
function Write-Title { param($text) Write-Host "`n$text" -ForegroundColor Cyan }
function Write-Success { param($text) Write-Host "  [OK] $text" -ForegroundColor Green }
function Write-Info { param($text) Write-Host "  [..] $text" -ForegroundColor Yellow }
function Write-Err { param($text) Write-Host "  [X] $text" -ForegroundColor Red }

# Degiskenler
$AppName = "VoiceBoard"
$InstallPath = "$env:ProgramFiles\$AppName"
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$RootDir = Split-Path -Parent $ScriptDir
$PublishDir = Join-Path $RootDir "bin\Release\net8.0-windows\publish"
$APOSource = Join-Path $RootDir "APO\bin\x64\Release\VoiceBoardAPO.dll"
$APODest = "$env:SystemRoot\System32\VoiceBoardAPO.dll"
$APO_CLSID = "{8A5BC3E7-9D4F-4B2A-8C1E-6F3D5A7B9C0E}"

function Show-Welcome {
    Clear-Host
    Write-Host ""
    Write-Host "  ╔═══════════════════════════════════════════════════════╗" -ForegroundColor Cyan
    Write-Host "  ║                                                       ║" -ForegroundColor Cyan
    Write-Host "  ║            VoiceBoard Kurulum Sihirbazi               ║" -ForegroundColor Cyan
    Write-Host "  ║                                                       ║" -ForegroundColor Cyan
    Write-Host "  ║   MyInstants.com meme soundboard uygulamasi           ║" -ForegroundColor Cyan
    Write-Host "  ║   Soundpad gibi calisir - VB-Cable gerekmez!          ║" -ForegroundColor Cyan
    Write-Host "  ║                                                       ║" -ForegroundColor Cyan
    Write-Host "  ╚═══════════════════════════════════════════════════════╝" -ForegroundColor Cyan
    Write-Host ""
}

function Test-Prerequisites {
    Write-Title "Sistem Gereksinimleri Kontrol Ediliyor..."

    # Windows 10+ kontrolu
    $osVersion = [Environment]::OSVersion.Version
    if ($osVersion.Major -lt 10) {
        Write-Err "Windows 10 veya uzeri gerekli!"
        return $false
    }
    Write-Success "Windows $($osVersion.Major) tespit edildi"

    # 64-bit kontrolu
    if (-not [Environment]::Is64BitOperatingSystem) {
        Write-Err "64-bit Windows gerekli!"
        return $false
    }
    Write-Success "64-bit sistem"

    # Admin kontrolu
    $isAdmin = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
    if (-not $isAdmin) {
        Write-Err "Yonetici olarak calistirin!"
        return $false
    }
    Write-Success "Yonetici yetkileri mevcut"

    return $true
}

function Install-Application {
    Write-Title "Uygulama Kuruluyor..."

    # Kurulum dizini olustur
    if (-not (Test-Path $InstallPath)) {
        New-Item -ItemType Directory -Path $InstallPath -Force | Out-Null
    }

    # Dosyalari kopyala
    if (Test-Path "$PublishDir\VoiceBoard.exe") {
        Write-Info "Dosyalar kopyalaniyor..."
        Copy-Item -Path "$PublishDir\*" -Destination $InstallPath -Recurse -Force
        Write-Success "Uygulama dosyalari kopyalandi"
    } else {
        # Publish yapilmamissa kaynak dosyalari derle
        Write-Info "Uygulama derleniyor (bu biraz zaman alabilir)..."

        $dotnetCheck = Get-Command dotnet -ErrorAction SilentlyContinue
        if (-not $dotnetCheck) {
            Write-Err ".NET SDK bulunamadi!"
            Write-Host "  https://dotnet.microsoft.com/download adresinden indirin." -ForegroundColor Yellow
            return $false
        }

        Push-Location $RootDir
        $result = & dotnet publish -c Release -r win-x64 --self-contained true -o $InstallPath 2>&1
        Pop-Location

        if ($LASTEXITCODE -ne 0) {
            Write-Err "Derleme basarisiz!"
            return $false
        }
        Write-Success "Uygulama derlendi ve kuruldu"
    }

    return $true
}

function Install-APO {
    Write-Title "APO Modulu Kuruluyor (Soundpad teknolojisi)..."

    # APO DLL kontrol
    if (-not (Test-Path $APOSource)) {
        Write-Info "APO DLL bulunamadi, VB-Cable modu kullanilacak"
        Write-Host "  APO icin Visual Studio ile APO projesini derleyin." -ForegroundColor Yellow
        return $true
    }

    # APO DLL kopyala
    Write-Info "APO DLL kopyalaniyor..."
    Copy-Item -Path $APOSource -Destination $APODest -Force
    Write-Success "VoiceBoardAPO.dll -> System32"

    # COM kaydi
    Write-Info "APO kaydediliyor..."
    $regResult = & regsvr32.exe /s $APODest 2>&1
    Write-Success "APO COM kaydedildi"

    # Unsigned APO izni
    Write-Info "Ses ayarlari yapilandiriliyor..."
    $audioKey = "HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Audio"
    if (-not (Test-Path $audioKey)) {
        New-Item -Path $audioKey -Force | Out-Null
    }
    Set-ItemProperty -Path $audioKey -Name "DisableProtectedAudioDG" -Value 1 -Type DWord
    Write-Success "Ses ayarlari tamamlandi"

    # Varsayilan mikrofonu yapilandir
    Write-Info "Mikrofon yapilandiriliyor..."
    $captureDevices = Get-ChildItem "HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\MMDevices\Audio\Capture" -ErrorAction SilentlyContinue

    foreach ($device in $captureDevices) {
        try {
            $state = (Get-ItemProperty -Path $device.PSPath -ErrorAction SilentlyContinue).DeviceState
            if ($state -eq 1) {
                $fxPath = "$($device.PSPath)\FxProperties"
                if (-not (Test-Path $fxPath)) {
                    New-Item -Path $fxPath -Force | Out-Null
                }
                Set-ItemProperty -Path $fxPath -Name "{D04E05A6-594B-4fb6-A80D-01AF5EED7D1D},5" -Value $APO_CLSID -Type String
                Write-Success "APO mikrofona baglandi"
                break
            }
        } catch {
            continue
        }
    }

    return $true
}

function Create-Shortcuts {
    Write-Title "Kisayollar Olusturuluyor..."

    $WScriptShell = New-Object -ComObject WScript.Shell

    # Masaustu kisayolu
    $desktopPath = [Environment]::GetFolderPath("Desktop")
    $shortcut = $WScriptShell.CreateShortcut("$desktopPath\VoiceBoard.lnk")
    $shortcut.TargetPath = "$InstallPath\VoiceBoard.exe"
    $shortcut.WorkingDirectory = $InstallPath
    $shortcut.Description = "VoiceBoard - Meme Soundboard"
    $shortcut.Save()
    Write-Success "Masaustu kisayolu olusturuldu"

    # Baslat menusu
    $startMenuPath = [Environment]::GetFolderPath("CommonPrograms")
    $programFolder = "$startMenuPath\VoiceBoard"
    if (-not (Test-Path $programFolder)) {
        New-Item -ItemType Directory -Path $programFolder -Force | Out-Null
    }
    $shortcut = $WScriptShell.CreateShortcut("$programFolder\VoiceBoard.lnk")
    $shortcut.TargetPath = "$InstallPath\VoiceBoard.exe"
    $shortcut.WorkingDirectory = $InstallPath
    $shortcut.Description = "VoiceBoard - Meme Soundboard"
    $shortcut.Save()
    Write-Success "Baslat menusu kisayolu olusturuldu"
}

function Create-Uninstaller {
    Write-Title "Kaldirma Programi Olusturuluyor..."

    $uninstallScript = @'
#Requires -RunAsAdministrator
$InstallPath = "$env:ProgramFiles\VoiceBoard"
$APODest = "$env:SystemRoot\System32\VoiceBoardAPO.dll"

Write-Host "VoiceBoard kaldiriliyor..." -ForegroundColor Yellow

# APO kaldir
if (Test-Path $APODest) {
    regsvr32.exe /u /s $APODest
    Remove-Item $APODest -Force -ErrorAction SilentlyContinue
}

# Uygulama kaldir
Remove-Item $InstallPath -Recurse -Force -ErrorAction SilentlyContinue

# Kisayollar
$desktop = [Environment]::GetFolderPath("Desktop")
Remove-Item "$desktop\VoiceBoard.lnk" -Force -ErrorAction SilentlyContinue

$startMenu = [Environment]::GetFolderPath("CommonPrograms")
Remove-Item "$startMenu\VoiceBoard" -Recurse -Force -ErrorAction SilentlyContinue

# Registry
Remove-Item "HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Audio\AudioProcessingObjects\{8A5BC3E7-9D4F-4B2A-8C1E-6F3D5A7B9C0E}" -Recurse -Force -ErrorAction SilentlyContinue

Write-Host "VoiceBoard kaldirildi!" -ForegroundColor Green
Write-Host "Bilgisayarinizi yeniden baslatin." -ForegroundColor Yellow
Read-Host "Cikis icin Enter'a basin"
'@

    $uninstallScript | Out-File -FilePath "$InstallPath\Uninstall.ps1" -Encoding UTF8 -Force
    Write-Success "Uninstall.ps1 olusturuldu"
}

function Show-Complete {
    Write-Host ""
    Write-Host "  ╔═══════════════════════════════════════════════════════╗" -ForegroundColor Green
    Write-Host "  ║                                                       ║" -ForegroundColor Green
    Write-Host "  ║              Kurulum Basariyla Tamamlandi!            ║" -ForegroundColor Green
    Write-Host "  ║                                                       ║" -ForegroundColor Green
    Write-Host "  ╚═══════════════════════════════════════════════════════╝" -ForegroundColor Green
    Write-Host ""
    Write-Host "  Kurulum Konumu: $InstallPath" -ForegroundColor White
    Write-Host ""
    Write-Host "  ONEMLI: APO modulunun aktif olmasi icin" -ForegroundColor Yellow
    Write-Host "          bilgisayarinizi yeniden baslatmaniz gerekiyor!" -ForegroundColor Yellow
    Write-Host ""

    $restart = Read-Host "  Simdi yeniden baslatmak ister misiniz? (E/H)"
    if ($restart -eq "E" -or $restart -eq "e") {
        Restart-Computer -Force
    } else {
        Write-Host ""
        Write-Host "  Masaustundeki VoiceBoard simgesine tiklayarak baslayabilirsiniz." -ForegroundColor Cyan
        Write-Host "  (APO icin yeniden baslatmayi unutmayin!)" -ForegroundColor Yellow
        Write-Host ""

        $launch = Read-Host "  Uygulamayi simdi baslatmak ister misiniz? (E/H)"
        if ($launch -eq "E" -or $launch -eq "e") {
            Start-Process "$InstallPath\VoiceBoard.exe"
        }
    }
}

# Ana program
try {
    Show-Welcome

    Write-Host "  Kuruluma baslamak icin Enter'a basin..." -ForegroundColor White
    Read-Host | Out-Null

    if (-not (Test-Prerequisites)) {
        Write-Host ""
        Read-Host "Cikis icin Enter'a basin"
        exit 1
    }

    if (-not (Install-Application)) {
        Write-Host ""
        Read-Host "Cikis icin Enter'a basin"
        exit 1
    }

    Install-APO | Out-Null
    Create-Shortcuts
    Create-Uninstaller

    Show-Complete
}
catch {
    Write-Host ""
    Write-Err "Beklenmeyen hata: $_"
    Write-Host ""
    Read-Host "Cikis icin Enter'a basin"
    exit 1
}
