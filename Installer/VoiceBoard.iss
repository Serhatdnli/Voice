; VoiceBoard Installer Script
; Inno Setup 6.x required

#define MyAppName "VoiceBoard"
#define MyAppVersion "1.0.0"
#define MyAppPublisher "VoiceBoard"
#define MyAppURL "https://github.com/Serhatdnli/Voice"
#define MyAppExeName "VoiceBoard.exe"

[Setup]
AppId={{8A5BC3E7-9D4F-4B2A-8C1E-6F3D5A7B9C0E}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
AllowNoIcons=yes
OutputDir=Output
OutputBaseFilename=VoiceBoard_Setup
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=admin
ArchitecturesInstallIn64BitMode=x64
MinVersion=10.0
LicenseFile=..\LICENSE

; Turkce dil destegi
[Languages]
Name: "turkish"; MessagesFile: "compiler:Languages\Turkish.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[CustomMessages]
turkish.InstallingAPO=Ses enjeksiyon modulu kuruluyor...
turkish.RestartRequired=Kurulum tamamlandi! Degisikliklerin etkin olmasi icin bilgisayarinizi yeniden baslatmaniz gerekiyor.
turkish.RestartNow=Simdi yeniden baslat
turkish.RestartLater=Daha sonra yeniden baslat
english.InstallingAPO=Installing audio injection module...
english.RestartRequired=Installation complete! You need to restart your computer for changes to take effect.
english.RestartNow=Restart now
english.RestartLater=Restart later

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked
Name: "installapo"; Description: "APO Modunu Kur (Soundpad gibi - VB-Cable gerekmez)"; GroupDescription: "Ses Enjeksiyon:"; Flags: checkedonce

[Files]
; Ana uygulama dosyalari
Source: "..\bin\Release\net8.0-windows\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

; APO DLL (64-bit)
Source: "..\APO\bin\x64\Release\VoiceBoardAPO.dll"; DestDir: "{sys}"; Flags: ignoreversion 64bit; Tasks: installapo

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\{cm:UninstallProgram,{#MyAppName}}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
; APO'yu kaydet
Filename: "regsvr32.exe"; Parameters: "/s ""{sys}\VoiceBoardAPO.dll"""; Flags: runhidden; Tasks: installapo; StatusMsg: "{cm:InstallingAPO}"

; Uygulamayi baslat
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent

[UninstallRun]
; APO'yu kaldir
Filename: "regsvr32.exe"; Parameters: "/u /s ""{sys}\VoiceBoardAPO.dll"""; Flags: runhidden

[UninstallDelete]
Type: files; Name: "{sys}\VoiceBoardAPO.dll"

[Registry]
; APO icin gerekli ayarlar
Root: HKLM; Subkey: "SOFTWARE\Microsoft\Windows\CurrentVersion\Audio"; ValueType: dword; ValueName: "DisableProtectedAudioDG"; ValueData: "1"; Flags: uninsdeletevalue; Tasks: installapo

; Uygulama ayarlari
Root: HKCU; Subkey: "Software\VoiceBoard"; Flags: uninsdeletekey

[Code]
var
  RestartPage: TNewNotebookPage;
  RestartNowRadio, RestartLaterRadio: TRadioButton;

// .NET 8 Runtime kontrolu
function IsDotNet8Installed(): Boolean;
var
  ResultCode: Integer;
begin
  Result := Exec('dotnet', '--list-runtimes', '', SW_HIDE, ewWaitUntilTerminated, ResultCode) and (ResultCode = 0);
end;

// Kurulum oncesi kontroller
function InitializeSetup(): Boolean;
var
  ErrorCode: Integer;
begin
  Result := True;

  // Windows 10+ kontrolu
  if not IsWin64 then
  begin
    MsgBox('VoiceBoard sadece 64-bit Windows 10/11 uzerinde calisir.', mbError, MB_OK);
    Result := False;
    Exit;
  end;
end;

// APO kurulumu icin mikrofon secimi
procedure ConfigureAPOForMicrophone();
var
  RegPath: String;
  I: Integer;
  DeviceState: Cardinal;
  Names: TArrayOfString;
begin
  // Varsayilan mikrofonu bul ve APO'yu bagla
  if RegGetSubkeyNames(HKEY_LOCAL_MACHINE,
    'SOFTWARE\Microsoft\Windows\CurrentVersion\MMDevices\Audio\Capture', Names) then
  begin
    for I := 0 to GetArrayLength(Names) - 1 do
    begin
      RegPath := 'SOFTWARE\Microsoft\Windows\CurrentVersion\MMDevices\Audio\Capture\' + Names[I];

      // Aktif cihaz mi kontrol et
      if RegQueryDWordValue(HKEY_LOCAL_MACHINE, RegPath, 'DeviceState', DeviceState) then
      begin
        if DeviceState = 1 then // Aktif
        begin
          // FxProperties olustur ve APO'yu ata
          RegWriteStringValue(HKEY_LOCAL_MACHINE,
            RegPath + '\FxProperties',
            '{D04E05A6-594B-4fb6-A80D-01AF5EED7D1D},5',
            '{8A5BC3E7-9D4F-4B2A-8C1E-6F3D5A7B9C0E}');
          Break;
        end;
      end;
    end;
  end;
end;

procedure CurStepChanged(CurStep: TSetupStep);
begin
  if CurStep = ssPostInstall then
  begin
    // APO kurulduysa mikrofonu yapilandir
    if IsTaskSelected('installapo') then
    begin
      ConfigureAPOForMicrophone();
    end;
  end;
end;

// Kurulum sonrasi yeniden baslatma sorusu
function NeedRestart(): Boolean;
begin
  Result := IsTaskSelected('installapo');
end;
