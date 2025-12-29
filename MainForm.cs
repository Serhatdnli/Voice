using System.Runtime.InteropServices;
using VoiceBoard.Models;
using VoiceBoard.Services;

namespace VoiceBoard;

public partial class MainForm : Form
{
    [DllImport("user32.dll")]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);
    [DllImport("user32.dll")]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    private const int WM_HOTKEY = 0x0312;
    private const uint MOD_CONTROL = 0x0002;
    private const uint MOD_SHIFT = 0x0004;
    private const uint MOD_ALT = 0x0001;
    private const uint MOD_NOREPEAT = 0x4000;
    private const int HOTKEY_SHOW_HIDE = 9999;

    private readonly MyInstantsService _myInstantsService;
    private readonly AudioService _audioService;
    private readonly SettingsService _settingsService;
    private AppSettings _settings;

    private readonly Dictionary<int, SoundItem> _hotkeyMap = new();
    private int _nextHotkeyId = 1;
    private CancellationTokenSource? _searchCts;
    private bool _isMiniMode = false;
    private Size _normalSize;
    private Point _normalLocation;

    // Modern UI Colors
    private readonly Color _bgDark = Color.FromArgb(30, 30, 35);
    private readonly Color _bgCard = Color.FromArgb(45, 45, 50);
    private readonly Color _bgHover = Color.FromArgb(60, 60, 70);
    private readonly Color _accent = Color.FromArgb(88, 101, 242);
    private readonly Color _accentHover = Color.FromArgb(71, 82, 196);
    private readonly Color _textPrimary = Color.White;
    private readonly Color _textSecondary = Color.FromArgb(180, 180, 180);
    private readonly Color _success = Color.FromArgb(87, 242, 135);
    private readonly Color _danger = Color.FromArgb(237, 66, 69);

    // UI Controls
    private TextBox _searchBox = null!;
    private FlowLayoutPanel _searchResults = null!;
    private FlowLayoutPanel _favoritesList = null!;
    private Label _statusLabel = null!;
    private TrackBar _volumeSlider = null!;
    private Label _volumeLabel = null!;
    private CheckBox _alwaysOnTopCheck = null!;
    private Button _miniModeBtn = null!;
    private Panel _mainPanel = null!;
    private Panel _miniBar = null!;
    private TabControl _tabControl = null!;

    public MainForm()
    {
        _myInstantsService = new MyInstantsService();
        _audioService = new AudioService();
        _settingsService = new SettingsService();
        _settings = _settingsService.Load();

        _audioService.OnSoundFinished += () =>
        {
            if (InvokeRequired)
                BeginInvoke(UpdatePlaybackStatus);
            else
                UpdatePlaybackStatus();
        };

        InitializeComponent();
        SetupModernUI();
    }

    private void InitializeComponent()
    {
        SuspendLayout();
        Text = "VoiceBoard";
        Size = new Size(480, 620);
        MinimumSize = new Size(400, 500);
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedSingle;
        BackColor = _bgDark;
        ForeColor = _textPrimary;
        MaximizeBox = false;
        ResumeLayout(false);
    }

    private void SetupModernUI()
    {
        _mainPanel = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = _bgDark,
            Padding = new Padding(10)
        };

        // Header
        var header = new Panel { Dock = DockStyle.Top, Height = 45, BackColor = _bgDark };

        var title = new Label
        {
            Text = "VoiceBoard",
            Font = new Font("Segoe UI", 14, FontStyle.Bold),
            ForeColor = _textPrimary,
            Location = new Point(5, 10),
            AutoSize = true
        };

        _alwaysOnTopCheck = new CheckBox
        {
            Text = "Ust",
            Font = new Font("Segoe UI", 9),
            ForeColor = _textSecondary,
            Location = new Point(320, 12),
            AutoSize = true
        };
        _alwaysOnTopCheck.CheckedChanged += (s, e) => TopMost = _alwaysOnTopCheck.Checked;

        _miniModeBtn = CreateButton("Mini", 55, 28);
        _miniModeBtn.Location = new Point(390, 8);
        _miniModeBtn.Click += ToggleMiniMode;

        header.Controls.AddRange(new Control[] { title, _alwaysOnTopCheck, _miniModeBtn });

        // Search
        var searchPanel = new Panel { Dock = DockStyle.Top, Height = 40, BackColor = _bgDark };

        _searchBox = new TextBox
        {
            Location = new Point(5, 5),
            Size = new Size(360, 28),
            Font = new Font("Segoe UI", 10),
            BackColor = _bgCard,
            ForeColor = _textPrimary,
            BorderStyle = BorderStyle.FixedSingle
        };
        _searchBox.KeyDown += async (s, e) => { if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; await SearchAsync(); } };

        var searchBtn = CreateButton("Ara", 70, 28);
        searchBtn.Location = new Point(375, 4);
        searchBtn.Click += async (s, e) => await SearchAsync();

        searchPanel.Controls.AddRange(new Control[] { _searchBox, searchBtn });

        // Tabs
        _tabControl = new TabControl
        {
            Dock = DockStyle.Fill,
            Font = new Font("Segoe UI", 9)
        };

        var searchTab = new TabPage("Arama") { BackColor = _bgDark };
        _searchResults = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            BackColor = _bgDark,
            Padding = new Padding(0, 5, 0, 5)
        };
        searchTab.Controls.Add(_searchResults);

        var favTab = new TabPage("Favoriler") { BackColor = _bgDark };
        _favoritesList = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            BackColor = _bgDark,
            Padding = new Padding(0, 5, 0, 5)
        };
        favTab.Controls.Add(_favoritesList);

        _tabControl.TabPages.AddRange(new[] { searchTab, favTab });

        // Bottom
        var bottom = new Panel { Dock = DockStyle.Bottom, Height = 70, BackColor = _bgCard };

        var volLabel = new Label
        {
            Text = "Ses:",
            Font = new Font("Segoe UI", 9),
            ForeColor = _textSecondary,
            Location = new Point(10, 12),
            AutoSize = true
        };

        _volumeSlider = new TrackBar
        {
            Location = new Point(45, 5),
            Size = new Size(150, 30),
            Minimum = 0,
            Maximum = 100,
            Value = (int)(_settings.MasterVolume * 100),
            TickFrequency = 10,
            BackColor = _bgCard
        };
        _volumeSlider.ValueChanged += (s, e) =>
        {
            _volumeLabel.Text = $"{_volumeSlider.Value}%";
            _audioService.MasterVolume = _volumeSlider.Value / 100f;
            _settings.MasterVolume = _audioService.MasterVolume;
            _settingsService.Save(_settings);
        };

        _volumeLabel = new Label
        {
            Text = $"{_volumeSlider.Value}%",
            Font = new Font("Segoe UI", 9, FontStyle.Bold),
            ForeColor = _accent,
            Location = new Point(200, 12),
            AutoSize = true
        };

        var stopBtn = CreateButton("Durdur", 70, 28);
        stopBtn.Location = new Point(270, 8);
        stopBtn.BackColor = _danger;
        stopBtn.Click += (s, e) => { _audioService.Stop(); UpdatePlaybackStatus(); };

        var trendBtn = CreateButton("Trend", 70, 28);
        trendBtn.Location = new Point(350, 8);
        trendBtn.BackColor = Color.FromArgb(255, 100, 100);
        trendBtn.Click += async (s, e) => await LoadTrendingAsync();

        _statusLabel = new Label
        {
            Text = "Hazir | Ctrl+Shift+V: Goster/Gizle",
            Font = new Font("Segoe UI", 8),
            ForeColor = _textSecondary,
            Location = new Point(10, 45),
            Size = new Size(420, 20)
        };

        bottom.Controls.AddRange(new Control[] { volLabel, _volumeSlider, _volumeLabel, stopBtn, trendBtn, _statusLabel });

        _mainPanel.Controls.Add(_tabControl);
        _mainPanel.Controls.Add(searchPanel);
        _mainPanel.Controls.Add(header);
        _mainPanel.Controls.Add(bottom);

        Controls.Add(_mainPanel);

        // Load
        LoadFavorites();
        _ = LoadTrendingAsync();
    }

    private Button CreateButton(string text, int w, int h)
    {
        var btn = new Button
        {
            Text = text,
            Size = new Size(w, h),
            FlatStyle = FlatStyle.Flat,
            BackColor = _accent,
            ForeColor = _textPrimary,
            Font = new Font("Segoe UI", 9, FontStyle.Bold),
            Cursor = Cursors.Hand
        };
        btn.FlatAppearance.BorderSize = 0;
        btn.MouseEnter += (s, e) => { if (btn.BackColor == _accent) btn.BackColor = _accentHover; };
        btn.MouseLeave += (s, e) => { if (btn.BackColor == _accentHover) btn.BackColor = _accent; };
        return btn;
    }

    private Panel CreateSoundCard(string name, string url, bool isFav, SoundItem? item = null)
    {
        var card = new Panel
        {
            Size = new Size(430, 45),
            BackColor = _bgCard,
            Margin = new Padding(0, 2, 0, 2),
            Tag = item ?? new SoundItem { Name = name, SourceUrl = url }
        };

        var lbl = new Label
        {
            Text = name.Length > 30 ? name[..27] + "..." : name,
            Font = new Font("Segoe UI", 9),
            ForeColor = _textPrimary,
            Location = new Point(8, 5),
            Size = new Size(180, 18)
        };

        var hkLbl = new Label
        {
            Text = item?.HotkeyDisplayText ?? "",
            Font = new Font("Segoe UI", 7),
            ForeColor = _textSecondary,
            Location = new Point(8, 25),
            AutoSize = true
        };

        var playBtn = CreateButton("Cal", 45, 30);
        playBtn.Location = new Point(195, 7);
        playBtn.Click += async (s, e) => await PlayAsync(name, url, item);

        var favBtn = CreateButton(isFav ? "-" : "+", 30, 30);
        favBtn.Location = new Point(245, 7);
        favBtn.BackColor = isFav ? _danger : _success;
        favBtn.Click += (s, e) =>
        {
            if (isFav) RemoveFavorite(item!);
            else AddFavorite(name, url);
        };

        var hkBtn = CreateButton("Tus", 40, 30);
        hkBtn.Location = new Point(280, 7);
        hkBtn.BackColor = _bgHover;
        hkBtn.Visible = isFav;
        hkBtn.Click += (s, e) => SetHotkey(item!, hkLbl);

        var volBtn = CreateButton("Vol", 40, 30);
        volBtn.Location = new Point(325, 7);
        volBtn.BackColor = _bgHover;
        volBtn.Visible = isFav;
        volBtn.Click += (s, e) => SetVolume(item!);

        // Hover
        card.MouseEnter += (s, e) => card.BackColor = _bgHover;
        card.MouseLeave += (s, e) => card.BackColor = _bgCard;
        lbl.MouseEnter += (s, e) => card.BackColor = _bgHover;
        lbl.MouseLeave += (s, e) => card.BackColor = _bgCard;

        // Double click to play
        card.DoubleClick += async (s, e) => await PlayAsync(name, url, item);
        lbl.DoubleClick += async (s, e) => await PlayAsync(name, url, item);

        card.Controls.AddRange(new Control[] { lbl, hkLbl, playBtn, favBtn, hkBtn, volBtn });
        return card;
    }

    private async Task PlayAsync(string name, string url, SoundItem? item = null)
    {
        try
        {
            _statusLabel.Text = $"Caliniyor: {name}";
            _statusLabel.ForeColor = _success;

            string path;
            if (item?.IsDownloaded == true)
            {
                path = item.LocalPath;
            }
            else
            {
                path = Path.Combine(Path.GetTempPath(), $"vb_{Guid.NewGuid()}.mp3");
                _statusLabel.Text = $"Indiriliyor: {name}";
                await _myInstantsService.DownloadSoundAsync(url, path);
                _statusLabel.Text = $"Caliniyor: {name}";
            }

            _audioService.PlaySound(path, item?.Volume ?? 1.0f);
        }
        catch (Exception ex)
        {
            _statusLabel.Text = $"Hata: {ex.Message}";
            _statusLabel.ForeColor = _danger;
        }
    }

    private void UpdatePlaybackStatus()
    {
        if (!_audioService.IsPlaying)
        {
            _statusLabel.Text = "Hazir | Ctrl+Shift+V: Goster/Gizle";
            _statusLabel.ForeColor = _textSecondary;
        }
    }

    private async Task SearchAsync()
    {
        var q = _searchBox.Text.Trim();
        if (string.IsNullOrEmpty(q)) return;

        _searchCts?.Cancel();
        _searchCts = new CancellationTokenSource();

        _statusLabel.Text = "Araniyor...";
        _searchResults.Controls.Clear();

        try
        {
            var results = await _myInstantsService.SearchAsync(q, _searchCts.Token);
            foreach (var r in results.Take(50))
                _searchResults.Controls.Add(CreateSoundCard(r.Title, r.SoundUrl, false));

            _statusLabel.Text = $"{results.Count} sonuc";
            _statusLabel.ForeColor = _textSecondary;
            _tabControl.SelectedIndex = 0;
        }
        catch (Exception ex)
        {
            _statusLabel.Text = $"Hata: {ex.Message}";
            _statusLabel.ForeColor = _danger;
        }
    }

    private async Task LoadTrendingAsync()
    {
        _statusLabel.Text = "Trend yukleniyor...";
        _searchResults.Controls.Clear();

        try
        {
            var results = await _myInstantsService.GetTrendingAsync();
            foreach (var r in results)
                _searchResults.Controls.Add(CreateSoundCard(r.Title, r.SoundUrl, false));

            _statusLabel.Text = $"{results.Count} trend ses";
            _statusLabel.ForeColor = _textSecondary;
            _tabControl.SelectedIndex = 0;
        }
        catch (Exception ex)
        {
            _statusLabel.Text = $"Trend yuklenemedi: {ex.Message}";
        }
    }

    private void AddFavorite(string name, string url)
    {
        var safe = string.Join("_", name.Split(Path.GetInvalidFileNameChars()));
        var path = Path.Combine(_settings.SoundsFolder, $"{safe}.mp3");

        var item = new SoundItem { Name = name, SourceUrl = url, LocalPath = path };
        _settings.Sounds.Add(item);
        _settingsService.Save(_settings);

        _ = Task.Run(async () =>
        {
            try { await _myInstantsService.DownloadSoundAsync(url, path); }
            catch { }
        });

        LoadFavorites();
        _statusLabel.Text = $"'{name}' eklendi";
    }

    private void RemoveFavorite(SoundItem item)
    {
        UnregisterItemHotkey(item);
        _settings.Sounds.Remove(item);
        _settingsService.Save(_settings);
        try { if (File.Exists(item.LocalPath)) File.Delete(item.LocalPath); } catch { }
        LoadFavorites();
        _statusLabel.Text = $"'{item.Name}' silindi";
    }

    private void LoadFavorites()
    {
        _favoritesList.Controls.Clear();
        foreach (var item in _settings.Sounds)
        {
            _favoritesList.Controls.Add(CreateSoundCard(item.Name, item.SourceUrl, true, item));
            if (item.Hotkey != Keys.None) RegisterItemHotkey(item);
        }
    }

    private void SetHotkey(SoundItem item, Label lbl)
    {
        using var dlg = new HotkeyDialog(item.Hotkey, item.ModifierKeys);
        if (dlg.ShowDialog() == DialogResult.OK)
        {
            UnregisterItemHotkey(item);
            item.Hotkey = dlg.SelectedKey;
            item.ModifierKeys = dlg.SelectedModifiers;
            _settingsService.Save(_settings);
            if (item.Hotkey != Keys.None) RegisterItemHotkey(item);
            lbl.Text = item.HotkeyDisplayText;
            _statusLabel.Text = $"Kisayol: {item.HotkeyDisplayText}";
        }
    }

    private void SetVolume(SoundItem item)
    {
        using var dlg = new VolumeDialog(item.Volume);
        if (dlg.ShowDialog() == DialogResult.OK)
        {
            item.Volume = dlg.SelectedVolume;
            _settingsService.Save(_settings);
            _statusLabel.Text = $"Ses: {(int)(item.Volume * 100)}%";
        }
    }

    private void RegisterItemHotkey(SoundItem item)
    {
        uint mod = MOD_NOREPEAT;
        if (item.ModifierKeys.HasFlag(Keys.Control)) mod |= MOD_CONTROL;
        if (item.ModifierKeys.HasFlag(Keys.Alt)) mod |= MOD_ALT;
        if (item.ModifierKeys.HasFlag(Keys.Shift)) mod |= MOD_SHIFT;

        var id = _nextHotkeyId++;
        if (RegisterHotKey(Handle, id, mod, (uint)item.Hotkey))
            _hotkeyMap[id] = item;
    }

    private void UnregisterItemHotkey(SoundItem item)
    {
        var entry = _hotkeyMap.FirstOrDefault(x => x.Value == item);
        if (entry.Key != 0)
        {
            UnregisterHotKey(Handle, entry.Key);
            _hotkeyMap.Remove(entry.Key);
        }
    }

    private void ToggleMiniMode(object? s, EventArgs e)
    {
        if (!_isMiniMode)
        {
            _normalSize = Size;
            _normalLocation = Location;
            _mainPanel.Visible = false;

            FormBorderStyle = FormBorderStyle.None;
            Size = new Size(280, 40);
            TopMost = true;
            Opacity = 0.92;
            Location = new Point(10, 10);

            _miniBar = new Panel { Dock = DockStyle.Fill, BackColor = _bgCard };
            var lbl = new Label
            {
                Text = "VoiceBoard",
                Font = new Font("Segoe UI", 9, FontStyle.Bold),
                ForeColor = _textPrimary,
                Location = new Point(10, 10),
                AutoSize = true
            };
            var closeBtn = CreateButton("X", 28, 28);
            closeBtn.Location = new Point(245, 6);
            closeBtn.BackColor = _danger;
            closeBtn.Click += ToggleMiniMode;

            _miniBar.Controls.AddRange(new Control[] { lbl, closeBtn });
            Controls.Add(_miniBar);

            _isMiniMode = true;
        }
        else
        {
            Controls.Remove(_miniBar);
            _miniBar?.Dispose();

            FormBorderStyle = FormBorderStyle.FixedSingle;
            Size = _normalSize;
            Location = _normalLocation;
            TopMost = _alwaysOnTopCheck.Checked;
            Opacity = 1.0;
            _mainPanel.Visible = true;

            _isMiniMode = false;
        }
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        RegisterHotKey(Handle, HOTKEY_SHOW_HIDE, MOD_CONTROL | MOD_SHIFT | MOD_NOREPEAT, (uint)Keys.V);
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == WM_HOTKEY)
        {
            var id = (int)m.WParam;
            if (id == HOTKEY_SHOW_HIDE)
            {
                if (WindowState == FormWindowState.Minimized || !Visible)
                {
                    Show();
                    WindowState = FormWindowState.Normal;
                    Activate();
                }
                else
                {
                    WindowState = FormWindowState.Minimized;
                }
            }
            else if (_hotkeyMap.TryGetValue(id, out var item))
            {
                _ = PlayAsync(item.Name, item.SourceUrl, item);
            }
        }
        base.WndProc(ref m);
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        UnregisterHotKey(Handle, HOTKEY_SHOW_HIDE);
        foreach (var id in _hotkeyMap.Keys) UnregisterHotKey(Handle, id);
        _audioService.Dispose();
        base.OnFormClosing(e);
    }
}

public class DeviceItem
{
    public int DeviceNumber { get; }
    public string Name { get; }
    public DeviceItem(int deviceNumber, string name) { DeviceNumber = deviceNumber; Name = name; }
    public override string ToString() => Name;
}
