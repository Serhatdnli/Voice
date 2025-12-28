# VoiceBoard - Meme Ses Karti

MyInstants.com entegrasyonlu, oyun ici ses efektleri icin Windows masaustu uygulamasi.

**Soundpad gibi calisir!** APO teknolojisi sayesinde VB-Cable gerektirmez.

## Ozellikler

- **MyInstants Entegrasyonu**: Trend sesleri goruntule, arama yap, dogrudan indir
- **Global Kisayol Tuslari**: Oyun icindeyken bile calisan hotkey destegi
- **APO Modu**: Soundpad gibi dogrudan mikrofon enjeksiyonu (VB-Cable gerekmez!)
- **VB-Cable Fallback**: APO kurulmadiysa VB-Cable ile calisir
- **Ses Yonetimi**: Her ses icin ayri ses seviyesi ayari
- **Otomatik Kayit**: Ayarlar ve ses kutuphanesi otomatik kaydedilir

## Kurulum

### Yontem 1: APO Modu (Onerilen - Soundpad gibi)

1. Projeyi derleyin:
   ```bash
   dotnet build -c Release
   ```

2. APO'yu yoneticikent calistirarak kurun:
   ```powershell
   # PowerShell'i yonetici olarak acin
   cd Installer
   .\Install-VoiceBoardAPO.ps1
   ```

3. Bilgisayari yeniden baslatin

4. Uygulamayi calistirin - VB-Cable gerekmez!

### Yontem 2: VB-Cable Modu (Fallback)

APO kurmak istemiyorsaniz:

1. [VB-Cable](https://vb-audio.com/Cable/) kurun
2. Uygulamayi calistirin
3. Mikrofon ve Sanal Kablo secin
4. Discord/oyunda mikrofon olarak "CABLE Output" secin

## Nasil Calisir?

### APO Modu (Soundpad gibi)
```
[Uygulama] --> [APO DLL] --> [Windows Audio Engine] --> [Discord/Oyun]
                  ^
            Mikrofon stream'ine
            dogrudan enjekte
```
- VB-Cable gerekmez
- Mikrofon secimi gerekmez
- Discord'da ayar degisikligi gerekmez

### VB-Cable Modu (Fallback)
```
[Mikrofon] --> [Uygulama] --> [VB-Cable] --> [Discord/Oyun]
[Ses Dosyasi] ----^
```

## Kullanim

### Ses Ekleme
1. Sol panelde arama yapin veya "Trend Sesler" butonuna tiklayin
2. Istediginiz sese cift tiklayin veya sag tik > "Indir ve Ekle"

### Kisayol Tusu Atama
1. Sag panelde sese sag tiklayin
2. "Kisayol Ata" secin
3. Istediginiz tus kombinasyonunu basin (orn: Ctrl+F1)

### Ses Calma
- Listeden cift tiklayin veya atanmis kisayol tusuna basin
- Oyun icindeyken de calisir!

## Teknik Detaylar

### APO (Audio Processing Object)
- Windows'un resmi ses isleme API'si
- User-mode'da calisir (kernel driver degil)
- Mikrofon capture stream'ine enjekte olur
- Soundpad ile ayni teknolojiyi kullanir

### Kullanilan Teknolojiler
- **Framework**: .NET 8.0 Windows Forms
- **Ses Kutuphanesi**: NAudio
- **Web Scraping**: HtmlAgilityPack
- **Veri Depolama**: JSON (Newtonsoft.Json)
- **APO**: Windows Audio Processing Object API

## Proje Yapisi

```
VoiceBoard/
├── APO/                      # C++ APO DLL projesi
│   ├── VoiceBoardAPO.cpp     # APO implementasyonu
│   ├── VoiceBoardAPO.h       # Header
│   └── dllmain.cpp           # DLL entry point
├── Installer/
│   └── Install-VoiceBoardAPO.ps1  # APO kurulum scripti
├── Models/
│   ├── SoundItem.cs
│   ├── SearchResult.cs
│   └── AppSettings.cs
├── Services/
│   ├── MyInstantsService.cs  # MyInstants API
│   ├── AudioService.cs       # Ses calma (APO + VB-Cable)
│   ├── APOService.cs         # APO iletisimi
│   ├── GlobalHotkeyService.cs
│   └── SettingsService.cs
├── MainForm.cs
├── HotkeyDialog.cs
├── VolumeDialog.cs
└── Program.cs
```

## APO Kaldirma

```powershell
# PowerShell'i yonetici olarak acin
cd Installer
.\Install-VoiceBoardAPO.ps1 -Uninstall
```

## Lisans

MIT
