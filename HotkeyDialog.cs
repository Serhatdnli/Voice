namespace VoiceBoard;

public class HotkeyDialog : Form
{
    public Keys SelectedKey { get; private set; }
    public Keys SelectedModifiers { get; private set; }

    private Label _hotkeyLabel;
    private Button _clearButton;
    private Button _okButton;
    private Button _cancelButton;

    public HotkeyDialog(Keys currentKey, Keys currentModifiers)
    {
        SelectedKey = currentKey;
        SelectedModifiers = currentModifiers;

        InitializeComponent();
        UpdateHotkeyLabel();
    }

    private void InitializeComponent()
    {
        Text = "Kısayol Tuşu Ata";
        Size = new Size(350, 180);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterParent;
        KeyPreview = true;

        var instructionLabel = new Label
        {
            Text = "Yeni kısayol tuşu kombinasyonunu basın:",
            Location = new Point(20, 20),
            AutoSize = true
        };

        _hotkeyLabel = new Label
        {
            Text = "Atanmadı",
            Location = new Point(20, 50),
            Size = new Size(290, 30),
            Font = new Font("Segoe UI", 14, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleCenter,
            BorderStyle = BorderStyle.FixedSingle,
            BackColor = Color.White
        };

        _clearButton = new Button
        {
            Text = "Temizle",
            Location = new Point(20, 95),
            Size = new Size(80, 30)
        };
        _clearButton.Click += (s, e) =>
        {
            SelectedKey = Keys.None;
            SelectedModifiers = Keys.None;
            UpdateHotkeyLabel();
        };

        _okButton = new Button
        {
            Text = "Tamam",
            Location = new Point(140, 95),
            Size = new Size(80, 30),
            DialogResult = DialogResult.OK
        };

        _cancelButton = new Button
        {
            Text = "İptal",
            Location = new Point(230, 95),
            Size = new Size(80, 30),
            DialogResult = DialogResult.Cancel
        };

        Controls.AddRange(new Control[]
        {
            instructionLabel, _hotkeyLabel, _clearButton, _okButton, _cancelButton
        });

        AcceptButton = _okButton;
        CancelButton = _cancelButton;
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        e.Handled = true;
        e.SuppressKeyPress = true;

        // Modifier tuşları tek başına kabul etme
        if (e.KeyCode == Keys.ControlKey || e.KeyCode == Keys.ShiftKey ||
            e.KeyCode == Keys.Menu || e.KeyCode == Keys.LWin || e.KeyCode == Keys.RWin)
        {
            return;
        }

        SelectedKey = e.KeyCode;
        SelectedModifiers = Keys.None;

        if (e.Control) SelectedModifiers |= Keys.Control;
        if (e.Alt) SelectedModifiers |= Keys.Alt;
        if (e.Shift) SelectedModifiers |= Keys.Shift;

        UpdateHotkeyLabel();
        base.OnKeyDown(e);
    }

    private void UpdateHotkeyLabel()
    {
        if (SelectedKey == Keys.None)
        {
            _hotkeyLabel.Text = "Atanmadı";
            return;
        }

        var parts = new List<string>();
        if (SelectedModifiers.HasFlag(Keys.Control)) parts.Add("Ctrl");
        if (SelectedModifiers.HasFlag(Keys.Alt)) parts.Add("Alt");
        if (SelectedModifiers.HasFlag(Keys.Shift)) parts.Add("Shift");
        parts.Add(SelectedKey.ToString());

        _hotkeyLabel.Text = string.Join(" + ", parts);
    }
}
