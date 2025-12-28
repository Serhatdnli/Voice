#Requires -RunAsAdministrator
<#
.SYNOPSIS
    Installs VoiceBoard APO for microphone audio injection.

.DESCRIPTION
    This script:
    1. Copies VoiceBoardAPO.dll to System32
    2. Registers the DLL as a COM component
    3. Configures the APO for your default microphone

.NOTES
    Requires Administrator privileges.
    After installation, restart your computer for changes to take effect.
#>

param(
    [switch]$Uninstall
)

$ErrorActionPreference = "Stop"

# Paths
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$DllSource = Join-Path $ScriptDir "VoiceBoardAPO.dll"
$DllDest = "$env:SystemRoot\System32\VoiceBoardAPO.dll"

# APO CLSID
$APO_CLSID = "{8A5BC3E7-9D4F-4B2A-8C1E-6F3D5A7B9C0E}"

# SFX/MFX GUIDs for capture devices
$PKEY_FX_PreMixEffectClsid = "{D04E05A6-594B-4fb6-A80D-01AF5EED7D1D},5"
$PKEY_FX_StreamEffectClsid = "{D04E05A6-594B-4fb6-A80D-01AF5EED7D1D},5"

function Write-Header {
    Write-Host ""
    Write-Host "========================================" -ForegroundColor Cyan
    Write-Host "   VoiceBoard APO Installer" -ForegroundColor Cyan
    Write-Host "========================================" -ForegroundColor Cyan
    Write-Host ""
}

function Test-IsAdmin {
    $currentPrincipal = New-Object Security.Principal.WindowsPrincipal([Security.Principal.WindowsIdentity]::GetCurrent())
    return $currentPrincipal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
}

function Get-DefaultMicrophone {
    $devices = Get-ItemProperty -Path "HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\MMDevices\Audio\Capture\*\Properties" -ErrorAction SilentlyContinue

    foreach ($dev in Get-ChildItem "HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\MMDevices\Audio\Capture") {
        try {
            $props = Get-ItemProperty -Path "$($dev.PSPath)\Properties"
            $name = $props."{a45c254e-df1c-4efd-8020-67d146a850e0},2"
            $state = (Get-ItemProperty -Path $dev.PSPath).DeviceState

            if ($state -eq 1) { # Active device
                return @{
                    Path = $dev.PSPath
                    Name = $name
                    Id = $dev.PSChildName
                }
            }
        } catch {
            continue
        }
    }

    return $null
}

function Get-AllMicrophones {
    $mics = @()

    foreach ($dev in Get-ChildItem "HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\MMDevices\Audio\Capture") {
        try {
            $props = Get-ItemProperty -Path "$($dev.PSPath)\Properties" -ErrorAction SilentlyContinue
            $name = $props."{a45c254e-df1c-4efd-8020-67d146a850e0},2"
            $state = (Get-ItemProperty -Path $dev.PSPath).DeviceState

            if ($state -eq 1 -and $name) {
                $mics += @{
                    Path = $dev.PSPath
                    Name = $name
                    Id = $dev.PSChildName
                }
            }
        } catch {
            continue
        }
    }

    return $mics
}

function Install-APO {
    Write-Header

    # Check admin
    if (-not (Test-IsAdmin)) {
        Write-Host "ERROR: This script requires Administrator privileges!" -ForegroundColor Red
        Write-Host "Please right-click and select 'Run as Administrator'" -ForegroundColor Yellow
        exit 1
    }

    # Check DLL exists
    if (-not (Test-Path $DllSource)) {
        Write-Host "ERROR: VoiceBoardAPO.dll not found!" -ForegroundColor Red
        Write-Host "Expected location: $DllSource" -ForegroundColor Yellow
        exit 1
    }

    Write-Host "Step 1: Copying DLL to System32..." -ForegroundColor Yellow
    Copy-Item -Path $DllSource -Destination $DllDest -Force
    Write-Host "  Done!" -ForegroundColor Green

    Write-Host ""
    Write-Host "Step 2: Registering COM component..." -ForegroundColor Yellow
    $result = & regsvr32.exe /s $DllDest
    Write-Host "  Done!" -ForegroundColor Green

    Write-Host ""
    Write-Host "Step 3: Enabling unsigned APOs..." -ForegroundColor Yellow

    # Allow unsigned APOs
    $audioKey = "HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Audio"
    if (-not (Test-Path $audioKey)) {
        New-Item -Path $audioKey -Force | Out-Null
    }
    Set-ItemProperty -Path $audioKey -Name "DisableProtectedAudioDG" -Value 1 -Type DWord
    Write-Host "  Done!" -ForegroundColor Green

    Write-Host ""
    Write-Host "Step 4: Selecting microphone..." -ForegroundColor Yellow

    $mics = Get-AllMicrophones
    if ($mics.Count -eq 0) {
        Write-Host "  WARNING: No active microphones found!" -ForegroundColor Yellow
        Write-Host "  You can run this installer again after connecting a microphone." -ForegroundColor Yellow
    } else {
        Write-Host ""
        Write-Host "Available microphones:" -ForegroundColor Cyan
        for ($i = 0; $i -lt $mics.Count; $i++) {
            Write-Host "  [$i] $($mics[$i].Name)" -ForegroundColor White
        }

        Write-Host ""
        $selection = Read-Host "Select microphone number (0-$($mics.Count - 1))"
        $micIndex = [int]$selection

        if ($micIndex -ge 0 -and $micIndex -lt $mics.Count) {
            $selectedMic = $mics[$micIndex]
            Write-Host ""
            Write-Host "Configuring APO for: $($selectedMic.Name)" -ForegroundColor Yellow

            # Create FxProperties key if not exists
            $fxPath = "$($selectedMic.Path)\FxProperties"
            if (-not (Test-Path $fxPath)) {
                New-Item -Path $fxPath -Force | Out-Null
            }

            # Set APO CLSID for stream effects
            Set-ItemProperty -Path $fxPath -Name $PKEY_FX_StreamEffectClsid -Value $APO_CLSID -Type String

            Write-Host "  Done!" -ForegroundColor Green
        } else {
            Write-Host "  Invalid selection. Skipping microphone configuration." -ForegroundColor Yellow
        }
    }

    Write-Host ""
    Write-Host "========================================" -ForegroundColor Green
    Write-Host "   Installation Complete!" -ForegroundColor Green
    Write-Host "========================================" -ForegroundColor Green
    Write-Host ""
    Write-Host "IMPORTANT: Please restart your computer for changes to take effect!" -ForegroundColor Yellow
    Write-Host ""

    $restart = Read-Host "Restart now? (Y/N)"
    if ($restart -eq "Y" -or $restart -eq "y") {
        Restart-Computer -Force
    }
}

function Uninstall-APO {
    Write-Header

    if (-not (Test-IsAdmin)) {
        Write-Host "ERROR: This script requires Administrator privileges!" -ForegroundColor Red
        exit 1
    }

    Write-Host "Uninstalling VoiceBoard APO..." -ForegroundColor Yellow

    # Unregister DLL
    if (Test-Path $DllDest) {
        & regsvr32.exe /u /s $DllDest
        Remove-Item -Path $DllDest -Force -ErrorAction SilentlyContinue
    }

    # Remove registry entries
    Remove-Item -Path "HKLM:\SOFTWARE\Classes\CLSID\$APO_CLSID" -Recurse -Force -ErrorAction SilentlyContinue
    Remove-Item -Path "HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Audio\AudioProcessingObjects\$APO_CLSID" -Recurse -Force -ErrorAction SilentlyContinue

    # Remove from all microphones
    foreach ($dev in Get-ChildItem "HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\MMDevices\Audio\Capture" -ErrorAction SilentlyContinue) {
        $fxPath = "$($dev.PSPath)\FxProperties"
        if (Test-Path $fxPath) {
            Remove-ItemProperty -Path $fxPath -Name $PKEY_FX_StreamEffectClsid -ErrorAction SilentlyContinue
        }
    }

    Write-Host ""
    Write-Host "Uninstallation complete!" -ForegroundColor Green
    Write-Host "Please restart your computer." -ForegroundColor Yellow
}

# Main
if ($Uninstall) {
    Uninstall-APO
} else {
    Install-APO
}
