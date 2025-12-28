# VoiceBoard - Meme Ses Karti

MyInstants.com entegrasyonlu, oyun ici ses efektleri icin Windows masaustu uygulamasi.

**Soundpad gibi calisir!** APO teknolojisi sayesinde VB-Cable gerektirmez.

---

## Kolay Kurulum (Ileri Ileri Bitir)

### Yontem 1: Kurulum Sihirbazi (En Kolay)

1. **Installer** klasorundeki **Setup.ps1** dosyasina sag tiklayin
2. **"PowerShell ile Calistir"** secin
3. Ekrandaki adimlari takip edin
4. Bilgisayari yeniden baslatin
5. Masaustundeki **VoiceBoard** ikonuna tiklayin

**Hepsi bu kadar!** Hic bir ayar yapmaniza gerek yok.

### Yontem 2: Inno Setup Installer (Gelismis)

Profesyonel bir kurulum deneyimi icin:

1. [Inno Setup](https://jrsoftware.org/isinfo.php) kurun
2. `build.bat` dosyasini calistirin
3. Olusturulan `Installer\Output\VoiceBoard_Setup.exe` dosyasini calistirin

---

## Ozellikler

- **MyInstants Entegrasyonu**: Trend sesleri goruntule, arama yap, dogrudan indir
- **Global Kisayol Tuslari**: Oyun icindeyken bile calisan hotkey destegi
- **APO Modu**: Soundpad gibi dogrudan mikrofon enjeksiyonu
- **Tek Tikla Kurulum**: Ileri Ileri Bitir - Ayar gerekmez!
- **Ses Yonetimi**: Her ses icin ayri ses seviyesi ayari

---

## Nasil Calisir?

```
╔════════════════════════════════════════════════════════════╗
║                     VoiceBoard                              ║
║                                                             ║
║  [Ses Dosyasi] --> [APO] --> [Mikrofon Stream] --> Discord ║
║                       │                                     ║
║                 Windows Audio                               ║
║                 Engine icinde                               ║
║                                                             ║
║  * VB-Cable GEREKMEZ                                       ║
║  * Mikrofon secimi GEREKMEZ                                ║
║  * Discord ayari GEREKMEZ                                  ║
╚════════════════════════════════════════════════════════════╝
```

---

## Kullanim

### Ses Ekleme
1. Sol panelde arama yapin veya **"Trend Sesler"** butonuna tiklayin
2. Istediginiz sese cift tiklayin

### Kisayol Tusu Atama
1. Sag panelde sese sag tiklayin
2. **"Kisayol Ata"** secin
3. Tus kombinasyonunu basin (orn: Ctrl+F1)

### Ses Calma
- Atanmis kisayol tusuna basin (oyun icinde bile calisir!)

---

## Kaldirma

1. **Installer** klasorundeki **Uninstall.ps1** dosyasini yonetici olarak calistirin
2. Bilgisayari yeniden baslatin

---

## Teknik Detaylar

| Bilesen | Teknoloji |
|---------|-----------|
| Framework | .NET 8.0 Windows Forms |
| Ses Kutuphanesi | NAudio |
| Web Scraping | HtmlAgilityPack |
| Audio Injection | Windows APO API |
| Veri Depolama | JSON |

---

## Proje Yapisi

```
VoiceBoard/
├── Installer/
│   ├── Setup.ps1           # Kolay kurulum scripti
│   ├── VoiceBoard.iss      # Inno Setup script
│   └── Install-VoiceBoardAPO.ps1
├── APO/                    # C++ APO DLL
├── Services/               # Is mantigi
├── Models/                 # Veri modelleri
├── build.bat               # Derleme scripti
└── VoiceBoard.csproj       # Proje dosyasi
```

## Lisans

MIT
