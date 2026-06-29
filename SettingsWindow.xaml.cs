using System.Windows;
using System.Windows.Media;
using Microsoft.Win32;

namespace TapeSplitterWpf;

public partial class SettingsWindow : Window
{
    private readonly AppSettings _settings;

    public SettingsWindow(AppSettings settings)
    {
        InitializeComponent();
        _settings = settings;
        ffmpegBox.Text = settings.FfmpegPath;
    }

    private void Browse_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog { Title = "Locate ffmpeg.exe", Filter = "ffmpeg.exe|ffmpeg.exe|Executables|*.exe" };
        if (!string.IsNullOrWhiteSpace(ffmpegBox.Text))
            try { dlg.InitialDirectory = System.IO.Path.GetDirectoryName(ffmpegBox.Text); } catch { }
        if (dlg.ShowDialog() == true) ffmpegBox.Text = dlg.FileName;
    }

    private void Test_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            string found = SilenceDetector.FindFfmpeg(ffmpegBox.Text.Trim());
            statusTxt.Foreground = Brushes.Green;
            statusTxt.Text = $"✓  Found: {found}";
        }
        catch (Exception ex)
        {
            statusTxt.Foreground = Brushes.Red;
            statusTxt.Text = $"✗  {ex.Message}";
        }
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        _settings.FfmpegPath = ffmpegBox.Text.Trim();
        _settings.Save();
        DialogResult = true;
    }
}
