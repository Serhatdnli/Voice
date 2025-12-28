# VoiceBoard - Meme Ses Kartı

MyInstants.com entegrasyonlu, oyun içi ses efektleri için Windows masaüstü uygulaması.

## Özellikler

- **MyInstants Entegrasyonu**: Trend sesleri görüntüle, arama yap, doğrudan indir
- **Global Kısayol Tuşları**: Oyun içindeyken bile çalışan hotkey desteği
- **Sanal Mikrofon Desteği**: VB-Cable veya Virtual Audio Cable ile sesleri mikrofon olarak oynat
- **Ses Yönetimi**: Her ses için ayrı ses seviyesi ayarı
- **Otomatik Kayıt**: Ayarlar ve ses kütüphanesi otomatik kaydedilir

## Gereksinimler

- Windows 10/11
- .NET 8.0 Runtime
- VB-Cable veya Virtual Audio Cable (mikrofon çıkışı için)

## Kurulum

1. [VB-Cable](https://vb-audio.com/Cable/) veya [Virtual Audio Cable](https://vac.muzychenko.net/en/) kurun
2. Projeyi derleyin veya release'ten indirin:
   ```bash
   dotnet build -c Release
   ```
3. Uygulamayı çalıştırın
4. Ayarlardan sanal kablo cihazını seçin
5. Oyun/Discord ses ayarlarından mikrofon olarak "CABLE Output" seçin

## Kullanım

### Ses Ekleme
1. Sol panelde arama yapın veya "Trend Sesler" butonuna tıklayın
2. İstediğiniz sese çift tıklayın veya sağ tık > "İndir ve Ekle"

### Kısayol Tuşu Atama
1. Sağ panelde sese sağ tıklayın
2. "Kısayol Ata" seçin
3. İstediğiniz tuş kombinasyonunu basın (örn: Ctrl+F1)

### Ses Çalma
- Listeden çift tıklayın veya atanmış kısayol tuşuna basın
- Oyun içindeyken de çalışır!

## Teknik Detaylar

- **Framework**: .NET 8.0 Windows Forms
- **Ses Kütüphanesi**: NAudio
- **Web Scraping**: HtmlAgilityPack
- **Veri Depolama**: JSON (Newtonsoft.Json)

## Yapı

```
VoiceBoard/
├── Models/
│   ├── SoundItem.cs      # Ses modeli
│   ├── SearchResult.cs   # Arama sonucu modeli
│   └── AppSettings.cs    # Uygulama ayarları
├── Services/
│   ├── MyInstantsService.cs    # MyInstants API
│   ├── AudioService.cs         # Ses çalma servisi
│   ├── GlobalHotkeyService.cs  # Hotkey yönetimi
│   └── SettingsService.cs      # Ayar kaydetme/yükleme
├── MainForm.cs           # Ana form
├── HotkeyDialog.cs       # Kısayol atama dialogu
├── VolumeDialog.cs       # Ses ayarı dialogu
└── Program.cs            # Giriş noktası
```

## Lisans

MIT
