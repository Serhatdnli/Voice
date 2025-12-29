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
    [DllImport("user32.dll")]
    private static extern bool ReleaseCapture();
    [DllImport("user32.dll")]
    private static extern int SendMessage(IntPtr hWnd, int Msg, int wParam, int lParam);
    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    private const int WM_HOTKEY = 0x0312;
    private const int WM_NCLBUTTONDOWN = 0xA1;
    private const int HT_CAPTION = 0x2;
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

    // Mode tracking
    private bool _isMiniMode = false;
    private bool _autoSwitchEnabled = true;
    private Size _normalSize;
    private Point _normalLocation;

    // Colors - Modern Discord-like theme
    private readonly Color _bgDark = Color.FromArgb(32, 34, 37);
    private readonly Color _bgDarker = Color.FromArgb(24, 25, 28);
    private readonly Color _bgCard = Color.FromArgb(47, 49, 54);
    private readonly Color _bgCardHover = Color.FromArgb(64, 68, 75);
    private readonly Color _accent = Color.FromArgb(88, 101, 242);
    private readonly Color _accentHover = Color.FromArgb(71, 82, 196);
    private readonly Color _textPrimary = Color.FromArgb(255, 255, 255);
    private readonly Color _textSecondary = Color.FromArgb(185, 187, 190);
    private readonly Color _textMuted = Color.FromArgb(114, 118, 125);
    private readonly Color _success = Color.FromArgb(87, 242, 135);
    private readonly Color _danger = Color.FromArgb(237, 66, 69);
    private readonly Color _warning = Color.FromArgb(250, 168, 26);

    // UI Controls - Full Mode
    private Panel _titleBar = null!;
    private Panel _mainPanel = null!;
    private TextBox _searchBox = null!;
    private FlowLayoutPanel _resultsList = null!;
    private FlowLayoutPanel _favoritesList = null!;
    private Label _statusLabel = null!;
    private TrackBar _volumeSlider = null!;
    private CheckBox _autoSwitchCheck = null!;
    private TabControl _tabControl = null!;

    // UI Controls - Mini Mode
    private Panel _miniPanel = null!;
    private TextBox _miniSearchBox = null!;
    private FlowLayoutPanel _miniSoundList = null!;
    private Label _miniStatus = null!;
    private bool _miniShowingFavorites = true;

    // Currently playing sound name
    private string? _currentlyPlaying = null;

    public MainForm()
    {
        _myInstantsService = new MyInstantsService();
        _audioService = new AudioService();
        _settingsService = new SettingsService();
        _settings = _settingsService.Load();

        _audioService.OnSoundFinished += () =>
        {
            _currentlyPlaying = null;
            if (InvokeRequired) BeginInvoke(UpdateStatus);
            else UpdateStatus();
        };

        _audioService.OnSoundStarted += () =>
        {
            if (InvokeRequired) BeginInvoke(UpdateStatus);
            else UpdateStatus();
        };

        InitializeComponent();
        BuildFullModeUI();
        BuildMiniModeUI();
        ShowFullMode();
    }

    private void InitializeComponent()
    {
        SuspendLayout();
        Text = "VoiceBoard";
        Size = new Size(500, 600);
        MinimumSize = new Size(450, 500);
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.None;
        BackColor = _bgDark;
        DoubleBuffered = true;
        ResumeLayout(false);
    }

    #region Full Mode UI

    private void BuildFullModeUI()
    {
        // Title Bar
        _titleBar = new Panel
        {
            Dock = DockStyle.Top,
            Height = 40,
            BackColor = _bgDarker,
            Padding = new Padding(12, 0, 8, 0)
        };
        _titleBar.MouseDown += DragWindow;

        var logo = new Label
        {
            Text = "VoiceBoard",
            Font = new Font("Segoe UI Semibold", 11),
            ForeColor = _textPrimary,
            AutoSize = true,
            Location = new Point(12, 10)
        };
        logo.MouseDown += DragWindow;

        var minimizeBtn = CreateIconButton("—", _titleBar.Width - 90, 5);
        minimizeBtn.Click += (s, e) => WindowState = FormWindowState.Minimized;
        minimizeBtn.Anchor = AnchorStyles.Top | AnchorStyles.Right;

        var miniBtn = CreateIconButton("▭", _titleBar.Width - 55, 5);
        miniBtn.Click += (s, e) => ShowMiniMode();
        miniBtn.Anchor = AnchorStyles.Top | AnchorStyles.Right;

        var closeBtn = CreateIconButton("✕", _titleBar.Width - 20, 5);
        closeBtn.Click += (s, e) => Close();
        closeBtn.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        closeBtn.ForeColor = _danger;

        _titleBar.Controls.AddRange(new Control[] { logo, minimizeBtn, miniBtn, closeBtn });

        // Main Panel
        _mainPanel = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = _bgDark,
            Padding = new Padding(16)
        };

        // Search Section
        var searchSection = new Panel
        {
            Dock = DockStyle.Top,
            Height = 50,
            BackColor = _bgDark
        };

        _searchBox = new TextBox
        {
            Location = new Point(0, 8),
            Size = new Size(380, 32),
            Font = new Font("Segoe UI", 11),
            BackColor = _bgCard,
            ForeColor = _textPrimary,
            BorderStyle = BorderStyle.None
        };
        _searchBox.KeyDown += async (s, e) =>
        {
            if (e.KeyCode == Keys.Enter)
            {
                e.SuppressKeyPress = true;
                await SearchAsync(_searchBox.Text);
            }
        };

        // Wrap in panel for padding
        var searchWrapper = new Panel
        {
            Location = new Point(0, 8),
            Size = new Size(390, 34),
            BackColor = _bgCard,
            Padding = new Padding(10, 6, 10, 6)
        };
        _searchBox.Dock = DockStyle.Fill;
        _searchBox.Location = Point.Empty;
        _searchBox.Size = new Size(370, 22);
        searchWrapper.Controls.Add(_searchBox);

        var searchBtn = CreateButton("Ara", 70, 34);
        searchBtn.Location = new Point(400, 8);
        searchBtn.Click += async (s, e) => await SearchAsync(_searchBox.Text);

        searchSection.Controls.AddRange(new Control[] { searchWrapper, searchBtn });

        // Tabs
        _tabControl = new TabControl
        {
            Dock = DockStyle.Fill,
            Font = new Font("Segoe UI", 10),
            Padding = new Point(12, 6)
        };

        var resultsTab = new TabPage("Sonuclar") { BackColor = _bgDark, Padding = new Padding(0, 8, 0, 0) };
        _resultsList = CreateSoundList();
        resultsTab.Controls.Add(_resultsList);

        var favoritesTab = new TabPage("Favoriler") { BackColor = _bgDark, Padding = new Padding(0, 8, 0, 0) };
        _favoritesList = CreateSoundList();
        favoritesTab.Controls.Add(_favoritesList);

        _tabControl.TabPages.AddRange(new[] { resultsTab, favoritesTab });

        // Bottom Panel
        var bottomPanel = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 100,
            BackColor = _bgDarker,
            Padding = new Padding(12, 8, 12, 8)
        };

        // Volume Row
        var volIcon = new Label
        {
            Text = "🔊",
            Font = new Font("Segoe UI", 10),
            Location = new Point(12, 12),
            AutoSize = true
        };

        _volumeSlider = new TrackBar
        {
            Location = new Point(45, 6),
            Size = new Size(180, 30),
            Minimum = 0,
            Maximum = 100,
            Value = (int)(_settings.MasterVolume * 100),
            TickStyle = TickStyle.None,
            BackColor = _bgDarker
        };
        _volumeSlider.ValueChanged += (s, e) =>
        {
            _audioService.MasterVolume = _volumeSlider.Value / 100f;
            _settings.MasterVolume = _audioService.MasterVolume;
            _settingsService.Save(_settings);
        };

        _autoSwitchCheck = new CheckBox
        {
            Text = "Otomatik Mini Mod",
            Font = new Font("Segoe UI", 9),
            ForeColor = _textSecondary,
            Location = new Point(240, 10),
            AutoSize = true,
            Checked = _autoSwitchEnabled
        };
        _autoSwitchCheck.CheckedChanged += (s, e) => _autoSwitchEnabled = _autoSwitchCheck.Checked;

        var stopBtn = CreateButton("Durdur", 75, 30);
        stopBtn.Location = new Point(390, 8);
        stopBtn.BackColor = _danger;
        stopBtn.Click += (s, e) => { _audioService.Stop(); _currentlyPlaying = null; UpdateStatus(); };

        // Audio Mode Status indicator
        var audioModeStatus = new Label
        {
            Font = new Font("Segoe UI", 8),
            Location = new Point(12, 50),
            AutoSize = true
        };

        if (_audioService.HasLoopbackDevice)
        {
            audioModeStatus.Text = $"● Loopback: {_audioService.SelectedLoopbackDevice}";
            audioModeStatus.ForeColor = _success;
        }
        else
        {
            audioModeStatus.Text = "○ Loopback cihazi bulunamadi";
            audioModeStatus.ForeColor = _warning;
        }

        // Status Row
        _statusLabel = new Label
        {
            Text = GetStatusText(),
            Font = new Font("Segoe UI", 9),
            ForeColor = _textMuted,
            Location = new Point(12, 68),
            Size = new Size(450, 25)
        };

        bottomPanel.Controls.AddRange(new Control[] { volIcon, _volumeSlider, _autoSwitchCheck, stopBtn, audioModeStatus, _statusLabel });

        _mainPanel.Controls.Add(_tabControl);
        _mainPanel.Controls.Add(searchSection);
        _mainPanel.Controls.Add(bottomPanel);

        Controls.Add(_mainPanel);
        Controls.Add(_titleBar);

        // Load data
        LoadFavorites();
        _ = LoadTrendingAsync();
    }

    private FlowLayoutPanel CreateSoundList()
    {
        return new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            BackColor = _bgDark,
            Padding = new Padding(0)
        };
    }

    #endregion

    #region Mini Mode UI

    private void BuildMiniModeUI()
    {
        _miniPanel = new Panel
        {
            Location = new Point(0, 0),
            Size = new Size(280, 180),
            BackColor = _bgDarker,
            Visible = false
        };
        _miniPanel.MouseDown += DragWindow;

        // Mini Header
        var miniHeader = new Panel
        {
            Dock = DockStyle.Top,
            Height = 30,
            BackColor = _bgDarker
        };
        miniHeader.MouseDown += DragWindow;

        var miniLogo = new Label
        {
            Text = "🎵 VoiceBoard",
            Font = new Font("Segoe UI Semibold", 9),
            ForeColor = _textPrimary,
            Location = new Point(8, 7),
            AutoSize = true
        };
        miniLogo.MouseDown += DragWindow;

        var expandBtn = CreateIconButton("◱", 220, 2);
        expandBtn.Size = new Size(25, 24);
        expandBtn.Click += (s, e) => ShowFullMode();

        var miniCloseBtn = CreateIconButton("✕", 248, 2);
        miniCloseBtn.Size = new Size(25, 24);
        miniCloseBtn.ForeColor = _danger;
        miniCloseBtn.Click += (s, e) => Close();

        miniHeader.Controls.AddRange(new Control[] { miniLogo, expandBtn, miniCloseBtn });

        // Mini Search
        var miniSearchPanel = new Panel
        {
            Dock = DockStyle.Top,
            Height = 32,
            BackColor = _bgDarker,
            Padding = new Padding(6, 2, 6, 2)
        };

        _miniSearchBox = new TextBox
        {
            Dock = DockStyle.Fill,
            Font = new Font("Segoe UI", 9),
            BackColor = _bgCard,
            ForeColor = _textPrimary,
            BorderStyle = BorderStyle.None
        };
        _miniSearchBox.KeyDown += async (s, e) =>
        {
            if (e.KeyCode == Keys.Enter)
            {
                e.SuppressKeyPress = true;
                await MiniSearchAsync(_miniSearchBox.Text);
            }
        };
        _miniSearchBox.GotFocus += (s, e) => _miniShowingFavorites = false;

        var miniSearchWrapper = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = _bgCard,
            Padding = new Padding(6, 4, 6, 4)
        };
        _miniSearchBox.Dock = DockStyle.Fill;
        miniSearchWrapper.Controls.Add(_miniSearchBox);
        miniSearchPanel.Controls.Add(miniSearchWrapper);

        // Mini Sound List
        _miniSoundList = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            BackColor = _bgDark,
            Padding = new Padding(4, 2, 4, 2)
        };

        // Mini Bottom
        var miniBottom = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 35,
            BackColor = _bgDarker,
            Padding = new Padding(6)
        };

        _miniStatus = new Label
        {
            Text = "Hazir",
            Font = new Font("Segoe UI", 8),
            ForeColor = _textMuted,
            Location = new Point(6, 10),
            Size = new Size(150, 18)
        };

        var miniFavBtn = new Button
        {
            Text = "★",
            Size = new Size(28, 24),
            Location = new Point(160, 5),
            FlatStyle = FlatStyle.Flat,
            BackColor = _warning,
            ForeColor = _bgDark,
            Font = new Font("Segoe UI", 9),
            Cursor = Cursors.Hand
        };
        miniFavBtn.FlatAppearance.BorderSize = 0;
        miniFavBtn.Click += (s, e) => { _miniShowingFavorites = true; LoadMiniSounds(); };

        var miniStopBtn = new Button
        {
            Text = "■",
            Size = new Size(28, 24),
            Location = new Point(192, 5),
            FlatStyle = FlatStyle.Flat,
            BackColor = _danger,
            ForeColor = _textPrimary,
            Font = new Font("Segoe UI", 9),
            Cursor = Cursors.Hand
        };
        miniStopBtn.FlatAppearance.BorderSize = 0;
        miniStopBtn.Click += (s, e) => { _audioService.Stop(); _currentlyPlaying = null; UpdateStatus(); };

        var miniVolBtn = new Button
        {
            Text = "🔊",
            Size = new Size(28, 24),
            Location = new Point(224, 5),
            FlatStyle = FlatStyle.Flat,
            BackColor = _bgCard,
            ForeColor = _textPrimary,
            Font = new Font("Segoe UI", 8),
            Cursor = Cursors.Hand
        };
        miniVolBtn.FlatAppearance.BorderSize = 0;
        miniVolBtn.Click += (s, e) => ShowVolumePopup();

        miniBottom.Controls.AddRange(new Control[] { _miniStatus, miniFavBtn, miniStopBtn, miniVolBtn });

        _miniPanel.Controls.Add(_miniSoundList);
        _miniPanel.Controls.Add(miniSearchPanel);
        _miniPanel.Controls.Add(miniBottom);
        _miniPanel.Controls.Add(miniHeader);

        Controls.Add(_miniPanel);
    }

    #endregion

    #region Mode Switching

    private void ShowFullMode()
    {
        if (!_isMiniMode) return;

        _miniPanel.Visible = false;
        _mainPanel.Visible = true;
        _titleBar.Visible = true;

        Size = _normalSize.IsEmpty ? new Size(500, 600) : _normalSize;
        if (!_normalLocation.IsEmpty) Location = _normalLocation;

        MinimumSize = new Size(450, 500);
        TopMost = false;
        Opacity = 1.0;

        _isMiniMode = false;
        LoadFavorites();
    }

    private void ShowMiniMode()
    {
        if (_isMiniMode) return;

        _normalSize = Size;
        _normalLocation = Location;

        _mainPanel.Visible = false;
        _titleBar.Visible = false;
        _miniPanel.Visible = true;

        MinimumSize = Size.Empty;
        Size = new Size(280, 180);
        Location = new Point(Screen.PrimaryScreen!.WorkingArea.Width - 300, 20);
        TopMost = true;
        Opacity = 0.97;

        _isMiniMode = true;
        _miniPanel.BringToFront();
        _miniShowingFavorites = true;
        LoadMiniSounds();
    }

    protected override void OnDeactivate(EventArgs e)
    {
        base.OnDeactivate(e);

        if (_autoSwitchEnabled && !_isMiniMode && GetForegroundWindow() != Handle)
        {
            BeginInvoke(ShowMiniMode);
        }
    }

    #endregion

    #region UI Helpers

    private Button CreateButton(string text, int w, int h)
    {
        var btn = new Button
        {
            Text = text,
            Size = new Size(w, h),
            FlatStyle = FlatStyle.Flat,
            BackColor = _accent,
            ForeColor = _textPrimary,
            Font = new Font("Segoe UI Semibold", 9),
            Cursor = Cursors.Hand
        };
        btn.FlatAppearance.BorderSize = 0;
        btn.MouseEnter += (s, e) => btn.BackColor = _accentHover;
        btn.MouseLeave += (s, e) => btn.BackColor = _accent;
        return btn;
    }

    private Button CreateIconButton(string text, int x, int y)
    {
        var btn = new Button
        {
            Text = text,
            Size = new Size(30, 28),
            Location = new Point(x, y),
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.Transparent,
            ForeColor = _textSecondary,
            Font = new Font("Segoe UI", 10),
            Cursor = Cursors.Hand
        };
        btn.FlatAppearance.BorderSize = 0;
        btn.FlatAppearance.MouseOverBackColor = _bgCardHover;
        return btn;
    }

    private Panel CreateSoundCard(string name, string url, bool isFavorite, SoundItem? item = null, bool mini = false)
    {
        var width = mini ? 255 : 440;
        var height = mini ? 32 : 48;
        var card = new Panel
        {
            Size = new Size(width, height),
            BackColor = _bgCard,
            Margin = new Padding(0, 2, 0, 2),
            Cursor = Cursors.Hand,
            Tag = item ?? new SoundItem { Name = name, SourceUrl = url }
        };

        var maxLen = mini ? 20 : 28;
        var displayName = name.Length > maxLen ? name[..(maxLen - 3)] + "..." : name;
        var nameLabel = new Label
        {
            Text = displayName,
            Font = new Font("Segoe UI", mini ? 8 : 10),
            ForeColor = _textPrimary,
            Location = new Point(8, mini ? 8 : 8),
            AutoSize = true
        };

        Label? hotkeyLabel = null;
        if (!mini && item?.Hotkey != Keys.None)
        {
            hotkeyLabel = new Label
            {
                Text = item.HotkeyDisplayText,
                Font = new Font("Segoe UI", 8),
                ForeColor = _textMuted,
                Location = new Point(8, 28),
                AutoSize = true
            };
        }

        var playBtn = new Button
        {
            Text = "▶",
            Size = new Size(mini ? 28 : 38, mini ? 24 : 34),
            Location = new Point(width - (mini ? 38 : 130), mini ? 4 : 7),
            FlatStyle = FlatStyle.Flat,
            BackColor = _success,
            ForeColor = _bgDark,
            Font = new Font("Segoe UI", mini ? 8 : 10),
            Cursor = Cursors.Hand
        };
        playBtn.FlatAppearance.BorderSize = 0;
        playBtn.Click += async (s, e) => await PlaySoundAsync(name, url, item);

        card.Controls.Add(nameLabel);
        card.Controls.Add(playBtn);
        if (hotkeyLabel != null) card.Controls.Add(hotkeyLabel);

        if (!mini)
        {
            if (isFavorite)
            {
                var removeBtn = new Button
                {
                    Text = "−",
                    Size = new Size(38, 34),
                    Location = new Point(width - 85, 7),
                    FlatStyle = FlatStyle.Flat,
                    BackColor = _danger,
                    ForeColor = _textPrimary,
                    Font = new Font("Segoe UI", 12),
                    Cursor = Cursors.Hand
                };
                removeBtn.FlatAppearance.BorderSize = 0;
                removeBtn.Click += (s, e) => RemoveFavorite(item!);

                var hotkeyBtn = new Button
                {
                    Text = "⌨",
                    Size = new Size(38, 34),
                    Location = new Point(width - 45, 7),
                    FlatStyle = FlatStyle.Flat,
                    BackColor = _bgCardHover,
                    ForeColor = _textPrimary,
                    Font = new Font("Segoe UI", 10),
                    Cursor = Cursors.Hand
                };
                hotkeyBtn.FlatAppearance.BorderSize = 0;
                hotkeyBtn.Click += (s, e) => SetHotkey(item!);

                card.Controls.AddRange(new Control[] { removeBtn, hotkeyBtn });
            }
            else
            {
                var addBtn = new Button
                {
                    Text = "+",
                    Size = new Size(38, 34),
                    Location = new Point(width - 85, 7),
                    FlatStyle = FlatStyle.Flat,
                    BackColor = _success,
                    ForeColor = _bgDark,
                    Font = new Font("Segoe UI", 12),
                    Cursor = Cursors.Hand
                };
                addBtn.FlatAppearance.BorderSize = 0;
                addBtn.Click += (s, e) => AddFavorite(name, url);
                card.Controls.Add(addBtn);
            }
        }

        card.MouseEnter += (s, e) => card.BackColor = _bgCardHover;
        card.MouseLeave += (s, e) => card.BackColor = _bgCard;
        nameLabel.MouseEnter += (s, e) => card.BackColor = _bgCardHover;
        nameLabel.MouseLeave += (s, e) => card.BackColor = _bgCard;

        card.DoubleClick += async (s, e) => await PlaySoundAsync(name, url, item);
        nameLabel.DoubleClick += async (s, e) => await PlaySoundAsync(name, url, item);

        return card;
    }

    private void DragWindow(object? sender, MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left)
        {
            ReleaseCapture();
            SendMessage(Handle, WM_NCLBUTTONDOWN, HT_CAPTION, 0);
        }
    }

    #endregion

    #region Sound Operations

    private async Task SearchAsync(string query)
    {
        query = query.Trim();
        if (string.IsNullOrEmpty(query)) return;

        _searchCts?.Cancel();
        _searchCts = new CancellationTokenSource();

        SetStatus("Araniyor...", true);
        _resultsList.Controls.Clear();

        try
        {
            var results = await _myInstantsService.SearchAsync(query, _searchCts.Token);

            foreach (var r in results.Take(50))
                _resultsList.Controls.Add(CreateSoundCard(r.Title, r.SoundUrl, false));

            SetStatus($"{results.Count} sonuc bulundu", false);

            if (!_isMiniMode)
                _tabControl.SelectedIndex = 0;
        }
        catch (Exception ex)
        {
            SetStatus($"Hata: {ex.Message}", false);
        }
    }

    private async Task MiniSearchAsync(string query)
    {
        query = query.Trim();
        if (string.IsNullOrEmpty(query))
        {
            _miniShowingFavorites = true;
            LoadMiniSounds();
            return;
        }

        SetStatus("Araniyor...", true);
        _miniSoundList.Controls.Clear();

        try
        {
            var results = await _myInstantsService.SearchAsync(query);

            foreach (var r in results.Take(20))
                _miniSoundList.Controls.Add(CreateSoundCard(r.Title, r.SoundUrl, false, null, mini: true));

            SetStatus($"{results.Count} sonuc", false);
            _miniShowingFavorites = false;
        }
        catch (Exception ex)
        {
            SetStatus($"Hata: {ex.Message}", false);
        }
    }

    private void LoadMiniSounds()
    {
        _miniSoundList.Controls.Clear();

        if (_miniShowingFavorites)
        {
            foreach (var item in _settings.Sounds)
                _miniSoundList.Controls.Add(CreateSoundCard(item.Name, item.SourceUrl, true, item, mini: true));
        }
    }

    private async Task LoadTrendingAsync()
    {
        SetStatus("Trend yukleniyor...", true);

        try
        {
            var results = await _myInstantsService.GetTrendingAsync();

            _resultsList.Controls.Clear();
            foreach (var r in results)
                _resultsList.Controls.Add(CreateSoundCard(r.Title, r.SoundUrl, false));

            SetStatus($"{results.Count} trend ses yuklendi", false);
        }
        catch
        {
            SetStatus("Trend yuklenemedi", false);
        }
    }

    private async Task PlaySoundAsync(string name, string url, SoundItem? item = null)
    {
        try
        {
            _currentlyPlaying = name;
            SetStatus($"▶ {name}", true);

            string path;
            if (item?.IsDownloaded == true)
            {
                path = item.LocalPath;
            }
            else
            {
                SetStatus($"Indiriliyor: {name}", true);
                path = Path.Combine(Path.GetTempPath(), $"vb_{Guid.NewGuid()}.mp3");
                await _myInstantsService.DownloadSoundAsync(url, path);
                _currentlyPlaying = name;
                SetStatus($"▶ {name}", true);
            }

            _audioService.PlaySound(path, item?.Volume ?? 1.0f);
        }
        catch (Exception ex)
        {
            _currentlyPlaying = null;
            SetStatus($"Hata: {ex.Message}", false);
        }
    }

    private string GetStatusText()
    {
        if (_currentlyPlaying != null)
            return $"▶ {_currentlyPlaying}";

        var mode = _audioService.HasLoopbackDevice ? "DirectSound ✓" : "Hoparlor";
        return $"Hazir • {mode} • Ctrl+Shift+V";
    }

    private void SetStatus(string text, bool isPlaying)
    {
        if (isPlaying)
        {
            _statusLabel.Text = text;
            _statusLabel.ForeColor = _success;
            if (_miniStatus != null)
            {
                var shortText = text.Length > 22 ? text[..19] + "..." : text;
                _miniStatus.Text = shortText;
                _miniStatus.ForeColor = _success;
            }
        }
        else
        {
            UpdateStatus();
        }
    }

    private void UpdateStatus()
    {
        var text = GetStatusText();
        var color = _currentlyPlaying != null ? _success : _textMuted;

        _statusLabel.Text = text;
        _statusLabel.ForeColor = color;

        if (_miniStatus != null)
        {
            var shortText = _currentlyPlaying != null
                ? $"▶ {(_currentlyPlaying.Length > 18 ? _currentlyPlaying[..15] + "..." : _currentlyPlaying)}"
                : "Hazir";
            _miniStatus.Text = shortText;
            _miniStatus.ForeColor = color;
        }
    }

    #endregion

    #region Favorites

    private void LoadFavorites()
    {
        _favoritesList.Controls.Clear();
        foreach (var item in _settings.Sounds)
        {
            _favoritesList.Controls.Add(CreateSoundCard(item.Name, item.SourceUrl, true, item));
            if (item.Hotkey != Keys.None) RegisterItemHotkey(item);
        }
    }

    private void AddFavorite(string name, string url)
    {
        var safeName = string.Join("_", name.Split(Path.GetInvalidFileNameChars()));
        var path = Path.Combine(_settings.SoundsFolder, $"{safeName}.mp3");

        var item = new SoundItem { Name = name, SourceUrl = url, LocalPath = path };
        _settings.Sounds.Add(item);
        _settingsService.Save(_settings);

        _ = Task.Run(async () =>
        {
            try { await _myInstantsService.DownloadSoundAsync(url, path); }
            catch { }
        });

        LoadFavorites();
        if (_isMiniMode) LoadMiniSounds();
        SetStatus($"'{name}' eklendi", false);
    }

    private void RemoveFavorite(SoundItem item)
    {
        UnregisterItemHotkey(item);
        _settings.Sounds.Remove(item);
        _settingsService.Save(_settings);
        try { if (File.Exists(item.LocalPath)) File.Delete(item.LocalPath); } catch { }

        LoadFavorites();
        if (_isMiniMode) LoadMiniSounds();
        SetStatus($"'{item.Name}' silindi", false);
    }

    private void SetHotkey(SoundItem item)
    {
        using var dlg = new ModernHotkeyDialog(item.Hotkey, item.ModifierKeys);
        dlg.TopMost = true;
        if (dlg.ShowDialog(this) == DialogResult.OK)
        {
            UnregisterItemHotkey(item);
            item.Hotkey = dlg.SelectedKey;
            item.ModifierKeys = dlg.SelectedModifiers;
            _settingsService.Save(_settings);
            if (item.Hotkey != Keys.None) RegisterItemHotkey(item);
            LoadFavorites();
            SetStatus($"Kisayol: {item.HotkeyDisplayText}", false);
        }
    }

    private void ShowVolumePopup()
    {
        using var dlg = new ModernVolumeDialog(_audioService.MasterVolume);
        dlg.TopMost = true;
        if (dlg.ShowDialog(this) == DialogResult.OK)
        {
            _audioService.MasterVolume = dlg.SelectedVolume;
            _settings.MasterVolume = dlg.SelectedVolume;
            _volumeSlider.Value = (int)(dlg.SelectedVolume * 100);
            _settingsService.Save(_settings);
        }
    }

    #endregion

    #region Hotkeys

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

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        RegisterHotKey(Handle, HOTKEY_SHOW_HIDE, MOD_CONTROL | MOD_SHIFT | MOD_NOREPEAT, (uint)Keys.V);

        // DirectSound'u window handle ile başlat
        _audioService.InitializeDirectSoundCapture(Handle);
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
                _ = PlaySoundAsync(item.Name, item.SourceUrl, item);
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

    #endregion
}

// Modern Hotkey Dialog
public class ModernHotkeyDialog : Form
{
    private readonly Color _bgDark = Color.FromArgb(32, 34, 37);
    private readonly Color _bgCard = Color.FromArgb(47, 49, 54);
    private readonly Color _accent = Color.FromArgb(88, 101, 242);
    private readonly Color _textPrimary = Color.White;
    private readonly Color _textMuted = Color.FromArgb(114, 118, 125);

    public Keys SelectedKey { get; private set; }
    public Keys SelectedModifiers { get; private set; }
    private Label _display = null!;

    public ModernHotkeyDialog(Keys key, Keys modifiers)
    {
        SelectedKey = key;
        SelectedModifiers = modifiers;
        BuildUI();
    }

    private void BuildUI()
    {
        Size = new Size(300, 160);
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.CenterParent;
        BackColor = _bgDark;
        KeyPreview = true;

        var title = new Label
        {
            Text = "Kisayol Ata",
            Font = new Font("Segoe UI Semibold", 11),
            ForeColor = _textPrimary,
            Location = new Point(20, 15),
            AutoSize = true
        };

        var hint = new Label
        {
            Text = "Yeni kisayol tusuna basin:",
            Font = new Font("Segoe UI", 9),
            ForeColor = _textMuted,
            Location = new Point(20, 45),
            AutoSize = true
        };

        _display = new Label
        {
            Text = GetDisplayText(),
            Font = new Font("Segoe UI Semibold", 12),
            ForeColor = _accent,
            BackColor = _bgCard,
            Size = new Size(260, 35),
            Location = new Point(20, 70),
            TextAlign = ContentAlignment.MiddleCenter
        };

        var clearBtn = new Button
        {
            Text = "Temizle",
            Size = new Size(75, 30),
            Location = new Point(20, 115),
            FlatStyle = FlatStyle.Flat,
            BackColor = _bgCard,
            ForeColor = _textPrimary,
            Font = new Font("Segoe UI", 9)
        };
        clearBtn.FlatAppearance.BorderSize = 0;
        clearBtn.Click += (s, e) => { SelectedKey = Keys.None; SelectedModifiers = Keys.None; UpdateDisplay(); };

        var okBtn = new Button
        {
            Text = "Tamam",
            Size = new Size(75, 30),
            Location = new Point(115, 115),
            FlatStyle = FlatStyle.Flat,
            BackColor = _accent,
            ForeColor = _textPrimary,
            Font = new Font("Segoe UI Semibold", 9),
            DialogResult = DialogResult.OK
        };
        okBtn.FlatAppearance.BorderSize = 0;

        var cancelBtn = new Button
        {
            Text = "Iptal",
            Size = new Size(75, 30),
            Location = new Point(205, 115),
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(237, 66, 69),
            ForeColor = _textPrimary,
            Font = new Font("Segoe UI", 9),
            DialogResult = DialogResult.Cancel
        };
        cancelBtn.FlatAppearance.BorderSize = 0;

        Controls.AddRange(new Control[] { title, hint, _display, clearBtn, okBtn, cancelBtn });
        AcceptButton = okBtn;
        CancelButton = cancelBtn;
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        e.Handled = true;
        e.SuppressKeyPress = true;

        if (e.KeyCode == Keys.ControlKey || e.KeyCode == Keys.ShiftKey ||
            e.KeyCode == Keys.Menu || e.KeyCode == Keys.LWin || e.KeyCode == Keys.RWin)
            return;

        SelectedKey = e.KeyCode;
        SelectedModifiers = Keys.None;
        if (e.Control) SelectedModifiers |= Keys.Control;
        if (e.Alt) SelectedModifiers |= Keys.Alt;
        if (e.Shift) SelectedModifiers |= Keys.Shift;

        UpdateDisplay();
        base.OnKeyDown(e);
    }

    private void UpdateDisplay() => _display.Text = GetDisplayText();

    private string GetDisplayText()
    {
        if (SelectedKey == Keys.None) return "Atanmadi";
        var parts = new List<string>();
        if (SelectedModifiers.HasFlag(Keys.Control)) parts.Add("Ctrl");
        if (SelectedModifiers.HasFlag(Keys.Alt)) parts.Add("Alt");
        if (SelectedModifiers.HasFlag(Keys.Shift)) parts.Add("Shift");
        parts.Add(SelectedKey.ToString());
        return string.Join(" + ", parts);
    }
}

// Modern Volume Dialog
public class ModernVolumeDialog : Form
{
    private readonly Color _bgDark = Color.FromArgb(32, 34, 37);
    private readonly Color _bgCard = Color.FromArgb(47, 49, 54);
    private readonly Color _accent = Color.FromArgb(88, 101, 242);
    private readonly Color _textPrimary = Color.White;

    public float SelectedVolume { get; private set; }
    private Label _display = null!;
    private TrackBar _slider = null!;

    public ModernVolumeDialog(float volume)
    {
        SelectedVolume = volume;
        BuildUI();
    }

    private void BuildUI()
    {
        Size = new Size(300, 140);
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.CenterParent;
        BackColor = _bgDark;

        var title = new Label
        {
            Text = "Ses Seviyesi",
            Font = new Font("Segoe UI Semibold", 11),
            ForeColor = _textPrimary,
            Location = new Point(20, 15),
            AutoSize = true
        };

        _slider = new TrackBar
        {
            Location = new Point(20, 45),
            Size = new Size(200, 30),
            Minimum = 0,
            Maximum = 100,
            Value = (int)(SelectedVolume * 100),
            TickStyle = TickStyle.None,
            BackColor = _bgDark
        };
        _slider.ValueChanged += (s, e) =>
        {
            SelectedVolume = _slider.Value / 100f;
            _display.Text = $"{_slider.Value}%";
        };

        _display = new Label
        {
            Text = $"{_slider.Value}%",
            Font = new Font("Segoe UI Semibold", 12),
            ForeColor = _accent,
            Location = new Point(230, 50),
            AutoSize = true
        };

        var okBtn = new Button
        {
            Text = "Tamam",
            Size = new Size(120, 30),
            Location = new Point(20, 90),
            FlatStyle = FlatStyle.Flat,
            BackColor = _accent,
            ForeColor = _textPrimary,
            Font = new Font("Segoe UI Semibold", 9),
            DialogResult = DialogResult.OK
        };
        okBtn.FlatAppearance.BorderSize = 0;

        var cancelBtn = new Button
        {
            Text = "Iptal",
            Size = new Size(120, 30),
            Location = new Point(160, 90),
            FlatStyle = FlatStyle.Flat,
            BackColor = _bgCard,
            ForeColor = _textPrimary,
            Font = new Font("Segoe UI", 9),
            DialogResult = DialogResult.Cancel
        };
        cancelBtn.FlatAppearance.BorderSize = 0;

        Controls.AddRange(new Control[] { title, _slider, _display, okBtn, cancelBtn });
        AcceptButton = okBtn;
        CancelButton = cancelBtn;
    }
}
