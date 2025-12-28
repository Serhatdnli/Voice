namespace VoiceBoard;

public class VolumeDialog : Form
{
    public float SelectedVolume { get; private set; }

    private TrackBar _volumeSlider;
    private Label _volumeLabel;

    public VolumeDialog(float currentVolume)
    {
        SelectedVolume = currentVolume;
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        Text = "Ses Seviyesi Ayarla";
        Size = new Size(350, 150);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterParent;

        var label = new Label
        {
            Text = "Ses Seviyesi:",
            Location = new Point(20, 20),
            AutoSize = true
        };

        _volumeSlider = new TrackBar
        {
            Location = new Point(20, 45),
            Size = new Size(220, 40),
            Minimum = 0,
            Maximum = 200, // 200% kadar arttırılabilir
            Value = (int)(SelectedVolume * 100),
            TickFrequency = 20
        };

        _volumeLabel = new Label
        {
            Text = $"{_volumeSlider.Value}%",
            Location = new Point(250, 50),
            AutoSize = true,
            Font = new Font("Segoe UI", 10, FontStyle.Bold)
        };

        _volumeSlider.ValueChanged += (s, e) =>
        {
            _volumeLabel.Text = $"{_volumeSlider.Value}%";
            SelectedVolume = _volumeSlider.Value / 100f;
        };

        var okButton = new Button
        {
            Text = "Tamam",
            Location = new Point(140, 80),
            Size = new Size(80, 30),
            DialogResult = DialogResult.OK
        };

        var cancelButton = new Button
        {
            Text = "İptal",
            Location = new Point(230, 80),
            Size = new Size(80, 30),
            DialogResult = DialogResult.Cancel
        };

        Controls.AddRange(new Control[]
        {
            label, _volumeSlider, _volumeLabel, okButton, cancelButton
        });

        AcceptButton = okButton;
        CancelButton = cancelButton;
    }
}
