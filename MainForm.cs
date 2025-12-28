using VoiceBoard.Models;
using VoiceBoard.Services;

namespace VoiceBoard;

public partial class MainForm : Form
{
    private readonly MyInstantsService _myInstantsService;
    private readonly AudioService _audioService;
    private readonly SettingsService _settingsService;
    private GlobalHotkeyService? _hotkeyService;

    private AppSettings _settings;
    private readonly Dictionary<string, int> _registeredHotkeys = new();
    private CancellationTokenSource? _searchCts;

    // UI Controls
    private TextBox _searchBox = null!;
    private Button _searchButton = null!;
    private Button _trendingButton = null!;
    private ListView _searchResultsList = null!;
    private ListView _soundsList = null!;
    private ComboBox _virtualCableCombo = null!;
    private ComboBox _microphoneCombo = null!;
    private CheckBox _playToSpeakersCheck = null!;
    private CheckBox _playToVirtualCableCheck = null!;
    private CheckBox _micPassthroughCheck = null!;
    private TrackBar _volumeSlider = null!;
    private TrackBar _micVolumeSlider = null!;
    private Label _statusLabel = null!;
    private Label _passthroughStatusLabel = null!;
    private ProgressBar _downloadProgress = null!;
    private Button _stopButton = null!;

    public MainForm()
    {
        _myInstantsService = new MyInstantsService();
        _audioService = new AudioService();
        _settingsService = new SettingsService();
        _settings = _settingsService.Load();

        InitializeComponent();
        SetupUI();
        LoadSettings();
    }

    private void InitializeComponent()
    {
        SuspendLayout();

        Text = "VoiceBoard - Meme Ses Karti";
        Size = new Size(1050, 750);
        MinimumSize = new Size(900, 700);
        StartPosition = FormStartPosition.CenterScreen;
        Icon = SystemIcons.Application;

        ResumeLayout(false);
    }

    private void SetupUI()
    {
        // Sol Panel - Arama
        var searchPanel = new Panel
        {
            Dock = DockStyle.Left,
            Width = 400,
            Padding = new Padding(10)
        };

        var searchLabel = new Label
        {
            Text = "MyInstants'ta Ara:",
            Location = new Point(10, 10),
            AutoSize = true,
            Font = new Font("Segoe UI", 10, FontStyle.Bold)
        };

        _searchBox = new TextBox
        {
            Location = new Point(10, 35),
            Size = new Size(280, 25),
            Font = new Font("Segoe UI", 10)
        };
        _searchBox.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter) SearchButton_Click(s, e); };

        _searchButton = new Button
        {
            Text = "Ara",
            Location = new Point(300, 33),
            Size = new Size(80, 28),
            FlatStyle = FlatStyle.Flat
        };
        _searchButton.Click += SearchButton_Click;

        _trendingButton = new Button
        {
            Text = "Trend Sesler",
            Location = new Point(10, 65),
            Size = new Size(370, 30),
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(255, 100, 100),
            ForeColor = Color.White
        };
        _trendingButton.Click += TrendingButton_Click;

        var searchResultsLabel = new Label
        {
            Text = "Arama Sonuclari:",
            Location = new Point(10, 105),
            AutoSize = true,
            Font = new Font("Segoe UI", 9, FontStyle.Bold)
        };

        _searchResultsList = new ListView
        {
            Location = new Point(10, 125),
            Size = new Size(370, 350),
            View = View.Details,
            FullRowSelect = true,
            GridLines = true,
            MultiSelect = false
        };
        _searchResultsList.Columns.Add("Ses Adi", 280);
        _searchResultsList.Columns.Add("", 60);
        _searchResultsList.DoubleClick += SearchResultsList_DoubleClick;

        var searchResultContextMenu = new ContextMenuStrip();
        searchResultContextMenu.Items.Add("Onizle", null, PreviewSound_Click);
        searchResultContextMenu.Items.Add("Indir ve Ekle", null, DownloadAndAdd_Click);
        _searchResultsList.ContextMenuStrip = searchResultContextMenu;

        _downloadProgress = new ProgressBar
        {
            Location = new Point(10, 485),
            Size = new Size(370, 20),
            Visible = false
        };

        searchPanel.Controls.AddRange(new Control[]
        {
            searchLabel, _searchBox, _searchButton, _trendingButton,
            searchResultsLabel, _searchResultsList, _downloadProgress
        });

        // Sag Panel - Ses Listesi ve Ayarlar
        var rightPanel = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(10)
        };

        var soundsLabel = new Label
        {
            Text = "Ses Kartim:",
            Location = new Point(10, 10),
            AutoSize = true,
            Font = new Font("Segoe UI", 10, FontStyle.Bold)
        };

        _soundsList = new ListView
        {
            Location = new Point(10, 35),
            Size = new Size(580, 300),
            View = View.Details,
            FullRowSelect = true,
            GridLines = true
        };
        _soundsList.Columns.Add("Ses Adi", 280);
        _soundsList.Columns.Add("Kisayol Tusu", 120);
        _soundsList.Columns.Add("Ses", 60);
        _soundsList.Columns.Add("Durum", 80);
        _soundsList.DoubleClick += SoundsList_DoubleClick;

        var soundsContextMenu = new ContextMenuStrip();
        soundsContextMenu.Items.Add("Cal", null, PlaySound_Click);
        soundsContextMenu.Items.Add("Kisayol Ata", null, AssignHotkey_Click);
        soundsContextMenu.Items.Add("Ses Ayarla", null, AdjustVolume_Click);
        soundsContextMenu.Items.Add(new ToolStripSeparator());
        soundsContextMenu.Items.Add("Sil", null, DeleteSound_Click);
        _soundsList.ContextMenuStrip = soundsContextMenu;

        // Ayarlar Paneli - Ses Cihazlari
        var devicesGroup = new GroupBox
        {
            Text = "Ses Cihazlari",
            Location = new Point(10, 345),
            Size = new Size(580, 100)
        };

        var micLabel = new Label
        {
            Text = "Mikrofon:",
            Location = new Point(10, 25),
            AutoSize = true
        };

        _microphoneCombo = new ComboBox
        {
            Location = new Point(100, 22),
            Size = new Size(250, 25),
            DropDownStyle = ComboBoxStyle.DropDownList
        };
        _microphoneCombo.SelectedIndexChanged += MicrophoneCombo_SelectedIndexChanged;

        var virtualCableLabel = new Label
        {
            Text = "Sanal Kablo:",
            Location = new Point(10, 55),
            AutoSize = true
        };

        _virtualCableCombo = new ComboBox
        {
            Location = new Point(100, 52),
            Size = new Size(250, 25),
            DropDownStyle = ComboBoxStyle.DropDownList
        };
        _virtualCableCombo.SelectedIndexChanged += VirtualCableCombo_SelectedIndexChanged;

        var refreshDevicesButton = new Button
        {
            Text = "Yenile",
            Location = new Point(360, 35),
            Size = new Size(60, 30)
        };
        refreshDevicesButton.Click += (s, e) => RefreshAudioDevices();

        _micPassthroughCheck = new CheckBox
        {
            Text = "Mikrofon Aktarimi (Passthrough)",
            Location = new Point(430, 25),
            AutoSize = true,
            Checked = true
        };
        _micPassthroughCheck.CheckedChanged += MicPassthrough_Changed;

        _passthroughStatusLabel = new Label
        {
            Text = "Durum: Pasif",
            Location = new Point(430, 55),
            AutoSize = true,
            ForeColor = Color.Gray
        };

        devicesGroup.Controls.AddRange(new Control[]
        {
            micLabel, _microphoneCombo,
            virtualCableLabel, _virtualCableCombo,
            refreshDevicesButton, _micPassthroughCheck, _passthroughStatusLabel
        });

        // Ayarlar Paneli - Ses Seviyeleri
        var volumeGroup = new GroupBox
        {
            Text = "Ses Ayarlari",
            Location = new Point(10, 455),
            Size = new Size(580, 100)
        };

        _playToSpeakersCheck = new CheckBox
        {
            Text = "Hoparlore Cal",
            Location = new Point(10, 25),
            AutoSize = true,
            Checked = true
        };
        _playToSpeakersCheck.CheckedChanged += PlaySettings_Changed;

        _playToVirtualCableCheck = new CheckBox
        {
            Text = "Sanal Kabloya Cal",
            Location = new Point(130, 25),
            AutoSize = true,
            Checked = true
        };
        _playToVirtualCableCheck.CheckedChanged += PlaySettings_Changed;

        var volumeLabel = new Label
        {
            Text = "Efekt Sesi:",
            Location = new Point(10, 60),
            AutoSize = true
        };

        _volumeSlider = new TrackBar
        {
            Location = new Point(80, 55),
            Size = new Size(150, 40),
            Minimum = 0,
            Maximum = 100,
            Value = 100,
            TickFrequency = 10
        };
        _volumeSlider.ValueChanged += VolumeSlider_ValueChanged;

        var volumeValueLabel = new Label
        {
            Text = "100%",
            Location = new Point(235, 60),
            Size = new Size(40, 20)
        };
        _volumeSlider.ValueChanged += (s, e) => volumeValueLabel.Text = $"{_volumeSlider.Value}%";

        var micVolumeLabel = new Label
        {
            Text = "Mikrofon:",
            Location = new Point(290, 60),
            AutoSize = true
        };

        _micVolumeSlider = new TrackBar
        {
            Location = new Point(355, 55),
            Size = new Size(150, 40),
            Minimum = 0,
            Maximum = 150,
            Value = 100,
            TickFrequency = 15
        };
        _micVolumeSlider.ValueChanged += MicVolumeSlider_ValueChanged;

        var micVolumeValueLabel = new Label
        {
            Text = "100%",
            Location = new Point(510, 60),
            Size = new Size(50, 20)
        };
        _micVolumeSlider.ValueChanged += (s, e) => micVolumeValueLabel.Text = $"{_micVolumeSlider.Value}%";

        volumeGroup.Controls.AddRange(new Control[]
        {
            _playToSpeakersCheck, _playToVirtualCableCheck,
            volumeLabel, _volumeSlider, volumeValueLabel,
            micVolumeLabel, _micVolumeSlider, micVolumeValueLabel
        });

        // Durum Cubugu
        var statusPanel = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 40
        };

        _statusLabel = new Label
        {
            Text = "Hazir - VB-Cable kurun ve mikrofon + sanal kablo secin",
            Location = new Point(10, 10),
            AutoSize = true
        };

        _stopButton = new Button
        {
            Text = "Durdur",
            Location = new Point(520, 7),
            Size = new Size(80, 26),
            FlatStyle = FlatStyle.Flat
        };
        _stopButton.Click += (s, e) => _audioService.Stop();

        statusPanel.Controls.AddRange(new Control[] { _statusLabel, _stopButton });

        rightPanel.Controls.AddRange(new Control[]
        {
            soundsLabel, _soundsList, devicesGroup, volumeGroup
        });

        Controls.AddRange(new Control[] { searchPanel, rightPanel, statusPanel });
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        _hotkeyService = new GlobalHotkeyService(Handle);
        RegisterAllHotkeys();
        RefreshAudioDevices();
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == GlobalHotkeyService.WM_HOTKEY)
        {
            _hotkeyService?.ProcessHotkey((int)m.WParam);
        }
        base.WndProc(ref m);
    }

    private void LoadSettings()
    {
        _audioService.PlayToSpeakers = _settings.PlayToSpeakers;
        _audioService.PlayToVirtualCable = _settings.PlayToVirtualCable;
        _audioService.MasterVolume = _settings.MasterVolume;
        _audioService.MicrophoneVolume = _settings.MicrophoneVolume;
        _audioService.MicrophonePassthroughEnabled = _settings.MicrophonePassthroughEnabled;

        _playToSpeakersCheck.Checked = _settings.PlayToSpeakers;
        _playToVirtualCableCheck.Checked = _settings.PlayToVirtualCable;
        _volumeSlider.Value = (int)(_settings.MasterVolume * 100);
        _micVolumeSlider.Value = (int)(_settings.MicrophoneVolume * 100);
        _micPassthroughCheck.Checked = _settings.MicrophonePassthroughEnabled;

        RefreshSoundsList();
    }

    private void SaveSettings()
    {
        _settings.PlayToSpeakers = _playToSpeakersCheck.Checked;
        _settings.PlayToVirtualCable = _playToVirtualCableCheck.Checked;
        _settings.MasterVolume = _volumeSlider.Value / 100f;
        _settings.MicrophoneVolume = _micVolumeSlider.Value / 100f;
        _settings.MicrophonePassthroughEnabled = _micPassthroughCheck.Checked;

        _settingsService.Save(_settings);
    }

    private void RefreshAudioDevices()
    {
        // Mikrofon listesi
        var inputDevices = _audioService.GetInputDevices();
        _microphoneCombo.Items.Clear();
        _microphoneCombo.Items.Add("(Seciniz)");

        foreach (var (deviceNumber, name) in inputDevices)
        {
            _microphoneCombo.Items.Add(new DeviceItem(deviceNumber, name));
        }

        _microphoneCombo.SelectedIndex = 0;

        if (!string.IsNullOrEmpty(_settings.MicrophoneDevice))
        {
            for (int i = 1; i < _microphoneCombo.Items.Count; i++)
            {
                if (_microphoneCombo.Items[i] is DeviceItem item &&
                    item.Name == _settings.MicrophoneDevice)
                {
                    _microphoneCombo.SelectedIndex = i;
                    break;
                }
            }
        }

        // Sanal kablo listesi
        var virtualDevices = _audioService.GetVirtualCableDevices();
        _virtualCableCombo.Items.Clear();
        _virtualCableCombo.Items.Add("(Seciniz)");

        foreach (var (deviceNumber, name) in virtualDevices)
        {
            _virtualCableCombo.Items.Add(new DeviceItem(deviceNumber, name));
        }

        _virtualCableCombo.SelectedIndex = 0;

        if (!string.IsNullOrEmpty(_settings.VirtualCableDevice))
        {
            for (int i = 1; i < _virtualCableCombo.Items.Count; i++)
            {
                if (_virtualCableCombo.Items[i] is DeviceItem item &&
                    item.Name == _settings.VirtualCableDevice)
                {
                    _virtualCableCombo.SelectedIndex = i;
                    break;
                }
            }
        }

        UpdatePassthroughStatus();
    }

    private void MicrophoneCombo_SelectedIndexChanged(object? sender, EventArgs e)
    {
        if (_microphoneCombo.SelectedItem is DeviceItem device)
        {
            _audioService.MicrophoneDeviceNumber = device.DeviceNumber;
            _settings.MicrophoneDevice = device.Name;
            SaveSettings();
            TryStartPassthrough();
        }
        else
        {
            _audioService.MicrophoneDeviceNumber = null;
            _settings.MicrophoneDevice = null;
            SaveSettings();
            _audioService.StopMicrophonePassthrough();
        }
        UpdatePassthroughStatus();
    }

    private void VirtualCableCombo_SelectedIndexChanged(object? sender, EventArgs e)
    {
        if (_virtualCableCombo.SelectedItem is DeviceItem device)
        {
            _audioService.VirtualCableDeviceNumber = device.DeviceNumber;
            _settings.VirtualCableDevice = device.Name;
            SaveSettings();
            TryStartPassthrough();
        }
        else
        {
            _audioService.VirtualCableDeviceNumber = null;
            _settings.VirtualCableDevice = null;
            SaveSettings();
            _audioService.StopMicrophonePassthrough();
        }
        UpdatePassthroughStatus();
    }

    private void MicPassthrough_Changed(object? sender, EventArgs e)
    {
        _audioService.MicrophonePassthroughEnabled = _micPassthroughCheck.Checked;
        SaveSettings();

        if (_micPassthroughCheck.Checked)
        {
            TryStartPassthrough();
        }
        else
        {
            _audioService.StopMicrophonePassthrough();
        }
        UpdatePassthroughStatus();
    }

    private void TryStartPassthrough()
    {
        if (_audioService.MicrophoneDeviceNumber.HasValue &&
            _audioService.VirtualCableDeviceNumber.HasValue &&
            _audioService.MicrophonePassthroughEnabled)
        {
            _audioService.StartMicrophonePassthrough();
        }
    }

    private void UpdatePassthroughStatus()
    {
        if (_audioService.IsPassthroughActive)
        {
            _passthroughStatusLabel.Text = "Durum: Aktif";
            _passthroughStatusLabel.ForeColor = Color.Green;
            _statusLabel.Text = "Hazir - Mikrofon aktarimi calisiyor";
        }
        else if (_audioService.MicrophonePassthroughEnabled)
        {
            if (!_audioService.MicrophoneDeviceNumber.HasValue)
            {
                _passthroughStatusLabel.Text = "Durum: Mikrofon sec";
                _passthroughStatusLabel.ForeColor = Color.Orange;
            }
            else if (!_audioService.VirtualCableDeviceNumber.HasValue)
            {
                _passthroughStatusLabel.Text = "Durum: Sanal kablo sec";
                _passthroughStatusLabel.ForeColor = Color.Orange;
            }
            else
            {
                _passthroughStatusLabel.Text = "Durum: Baslatiliyor...";
                _passthroughStatusLabel.ForeColor = Color.Orange;
            }
            _statusLabel.Text = "Mikrofon ve sanal kablo seciniz";
        }
        else
        {
            _passthroughStatusLabel.Text = "Durum: Devre disi";
            _passthroughStatusLabel.ForeColor = Color.Gray;
            _statusLabel.Text = "Hazir";
        }
    }

    private void MicVolumeSlider_ValueChanged(object? sender, EventArgs e)
    {
        _audioService.MicrophoneVolume = _micVolumeSlider.Value / 100f;
        SaveSettings();
    }

    private async void SearchButton_Click(object? sender, EventArgs e)
    {
        var query = _searchBox.Text.Trim();
        if (string.IsNullOrEmpty(query)) return;

        _searchCts?.Cancel();
        _searchCts = new CancellationTokenSource();

        _searchButton.Enabled = false;
        _statusLabel.Text = "Araniyor...";
        _searchResultsList.Items.Clear();

        try
        {
            var results = await _myInstantsService.SearchAsync(query, _searchCts.Token);

            foreach (var result in results)
            {
                var item = new ListViewItem(result.Title);
                item.SubItems.Add(">");
                item.Tag = result;
                _searchResultsList.Items.Add(item);
            }

            _statusLabel.Text = $"{results.Count} sonuc bulundu";
        }
        catch (OperationCanceledException)
        {
            _statusLabel.Text = "Arama iptal edildi";
        }
        catch (Exception ex)
        {
            _statusLabel.Text = $"Hata: {ex.Message}";
        }
        finally
        {
            _searchButton.Enabled = true;
        }
    }

    private async void TrendingButton_Click(object? sender, EventArgs e)
    {
        _searchCts?.Cancel();
        _searchCts = new CancellationTokenSource();

        _trendingButton.Enabled = false;
        _statusLabel.Text = "Trend sesler yukleniyor...";
        _searchResultsList.Items.Clear();

        try
        {
            var results = await _myInstantsService.GetTrendingAsync(_searchCts.Token);

            foreach (var result in results)
            {
                var item = new ListViewItem(result.Title);
                item.SubItems.Add(">");
                item.Tag = result;
                _searchResultsList.Items.Add(item);
            }

            _statusLabel.Text = $"{results.Count} trend ses yuklendi";
        }
        catch (Exception ex)
        {
            _statusLabel.Text = $"Hata: {ex.Message}";
        }
        finally
        {
            _trendingButton.Enabled = true;
        }
    }

    private void SearchResultsList_DoubleClick(object? sender, EventArgs e)
    {
        DownloadAndAdd_Click(sender, e);
    }

    private async void PreviewSound_Click(object? sender, EventArgs e)
    {
        if (_searchResultsList.SelectedItems.Count == 0) return;

        var searchResult = _searchResultsList.SelectedItems[0].Tag as SearchResult;
        if (searchResult == null) return;

        _statusLabel.Text = "Onizleme yukleniyor...";

        try
        {
            var tempPath = Path.Combine(Path.GetTempPath(), $"voiceboard_preview_{Guid.NewGuid()}.mp3");
            await _myInstantsService.DownloadSoundAsync(searchResult.SoundUrl, tempPath);

            _audioService.PlaySound(tempPath);
            _statusLabel.Text = $"Caliniyor: {searchResult.Title}";
        }
        catch (Exception ex)
        {
            _statusLabel.Text = $"Onizleme hatasi: {ex.Message}";
        }
    }

    private async void DownloadAndAdd_Click(object? sender, EventArgs e)
    {
        if (_searchResultsList.SelectedItems.Count == 0) return;

        var searchResult = _searchResultsList.SelectedItems[0].Tag as SearchResult;
        if (searchResult == null) return;

        var safeName = string.Join("_", searchResult.Title.Split(Path.GetInvalidFileNameChars()));
        var savePath = Path.Combine(_settings.SoundsFolder, $"{safeName}.mp3");

        _downloadProgress.Visible = true;
        _downloadProgress.Value = 0;
        _statusLabel.Text = "Indiriliyor...";

        try
        {
            var progress = new Progress<int>(p => _downloadProgress.Value = p);
            await _myInstantsService.DownloadSoundAsync(searchResult.SoundUrl, savePath, progress);

            var soundItem = new SoundItem
            {
                Name = searchResult.Title,
                SourceUrl = searchResult.SoundUrl,
                LocalPath = savePath
            };

            _settings.Sounds.Add(soundItem);
            SaveSettings();
            RefreshSoundsList();

            _statusLabel.Text = $"'{searchResult.Title}' eklendi!";
        }
        catch (Exception ex)
        {
            _statusLabel.Text = $"Indirme hatasi: {ex.Message}";
        }
        finally
        {
            _downloadProgress.Visible = false;
        }
    }

    private void RefreshSoundsList()
    {
        _soundsList.Items.Clear();

        foreach (var sound in _settings.Sounds)
        {
            var item = new ListViewItem(sound.Name);
            item.SubItems.Add(sound.HotkeyDisplayText);
            item.SubItems.Add($"{(int)(sound.Volume * 100)}%");
            item.SubItems.Add(sound.IsDownloaded ? "OK" : "X");
            item.Tag = sound;
            _soundsList.Items.Add(item);
        }
    }

    private void SoundsList_DoubleClick(object? sender, EventArgs e)
    {
        PlaySound_Click(sender, e);
    }

    private void PlaySound_Click(object? sender, EventArgs e)
    {
        if (_soundsList.SelectedItems.Count == 0) return;

        var sound = _soundsList.SelectedItems[0].Tag as SoundItem;
        if (sound == null || !sound.IsDownloaded) return;

        _audioService.PlaySound(sound.LocalPath, sound.Volume);
        _statusLabel.Text = $"Caliniyor: {sound.Name}";
    }

    private void AssignHotkey_Click(object? sender, EventArgs e)
    {
        if (_soundsList.SelectedItems.Count == 0) return;

        var sound = _soundsList.SelectedItems[0].Tag as SoundItem;
        if (sound == null) return;

        using var dialog = new HotkeyDialog(sound.Hotkey, sound.ModifierKeys);
        if (dialog.ShowDialog() == DialogResult.OK)
        {
            if (_registeredHotkeys.TryGetValue(sound.Id, out var oldId))
            {
                _hotkeyService?.UnregisterHotkey(oldId);
                _registeredHotkeys.Remove(sound.Id);
            }

            sound.Hotkey = dialog.SelectedKey;
            sound.ModifierKeys = dialog.SelectedModifiers;

            if (sound.Hotkey != Keys.None)
            {
                RegisterHotkeyForSound(sound);
            }

            SaveSettings();
            RefreshSoundsList();
        }
    }

    private void AdjustVolume_Click(object? sender, EventArgs e)
    {
        if (_soundsList.SelectedItems.Count == 0) return;

        var sound = _soundsList.SelectedItems[0].Tag as SoundItem;
        if (sound == null) return;

        using var dialog = new VolumeDialog(sound.Volume);
        if (dialog.ShowDialog() == DialogResult.OK)
        {
            sound.Volume = dialog.SelectedVolume;
            SaveSettings();
            RefreshSoundsList();
        }
    }

    private void DeleteSound_Click(object? sender, EventArgs e)
    {
        if (_soundsList.SelectedItems.Count == 0) return;

        var sound = _soundsList.SelectedItems[0].Tag as SoundItem;
        if (sound == null) return;

        var result = MessageBox.Show(
            $"'{sound.Name}' silinsin mi?",
            "Silme Onayi",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question);

        if (result == DialogResult.Yes)
        {
            if (_registeredHotkeys.TryGetValue(sound.Id, out var id))
            {
                _hotkeyService?.UnregisterHotkey(id);
                _registeredHotkeys.Remove(sound.Id);
            }

            try
            {
                if (File.Exists(sound.LocalPath))
                    File.Delete(sound.LocalPath);
            }
            catch { }

            _settings.Sounds.Remove(sound);
            SaveSettings();
            RefreshSoundsList();
        }
    }

    private void RegisterAllHotkeys()
    {
        foreach (var sound in _settings.Sounds.Where(s => s.Hotkey != Keys.None))
        {
            RegisterHotkeyForSound(sound);
        }
    }

    private void RegisterHotkeyForSound(SoundItem sound)
    {
        if (_hotkeyService == null || sound.Hotkey == Keys.None) return;

        var id = _hotkeyService.RegisterHotkey(sound.Hotkey, sound.ModifierKeys, () =>
        {
            if (sound.IsDownloaded)
            {
                BeginInvoke(() =>
                {
                    _audioService.PlaySound(sound.LocalPath, sound.Volume);
                    _statusLabel.Text = $"Caliniyor: {sound.Name}";
                });
            }
        });

        if (id > 0)
        {
            _registeredHotkeys[sound.Id] = id;
        }
    }

    private void PlaySettings_Changed(object? sender, EventArgs e)
    {
        _audioService.PlayToSpeakers = _playToSpeakersCheck.Checked;
        _audioService.PlayToVirtualCable = _playToVirtualCableCheck.Checked;
        SaveSettings();
    }

    private void VolumeSlider_ValueChanged(object? sender, EventArgs e)
    {
        _audioService.MasterVolume = _volumeSlider.Value / 100f;
        SaveSettings();
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        _hotkeyService?.Dispose();
        _audioService.Dispose();
        base.OnFormClosing(e);
    }
}

public class DeviceItem
{
    public int DeviceNumber { get; }
    public string Name { get; }

    public DeviceItem(int deviceNumber, string name)
    {
        DeviceNumber = deviceNumber;
        Name = name;
    }

    public override string ToString() => Name;
}
