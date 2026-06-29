using System.Windows;
using System.Windows.Input;

namespace TapeSplitterWpf;

public partial class MainWindow : Window
{
    private readonly MainViewModel _vm;

    public MainWindow()
    {
        InitializeComponent();
        _vm = new MainViewModel();
        DataContext = _vm;

        // Apply saved theme before the window is shown
        var saved = AppSettings.Load().Theme;
        var mode  = saved switch { "Light" => ThemeMode.Light, "Dark" => ThemeMode.Dark, _ => ThemeMode.System };
        ThemeManager.Apply(mode);
        UpdateThemeChecks(mode);

        // Wire waveform events
        waveform1.MarkersChanged += m => _vm.OnMarkersChanged(m, _vm.Side1, 1);
        waveform2.MarkersChanged += m => _vm.OnMarkersChanged(m, _vm.Side2, 2);

        // Push envelope data into the right WaveformControl when detection finishes
        _vm.WaveformDataReady += (envelope, totalSecs, sideNum) =>
        {
            Dispatcher.Invoke(() =>
            {
                var wf = sideNum == 1 ? waveform1 : waveform2;
                wf.LoadEnvelope(envelope, totalSecs);
            });
        };

        // Push marker positions into the waveform (after auto-detect)
        _vm.MarkersRequested += (markers, sideNum) =>
        {
            Dispatcher.Invoke(() =>
            {
                var wf = sideNum == 1 ? waveform1 : waveform2;
                wf.SetMarkers(markers.Select(m => m));
            });
        };

        // Push exclusion overlays into both waveforms when checkboxes change
        _vm.ExclusionsChanged += (ex1, ex2) =>
        {
            Dispatcher.Invoke(() =>
            {
                waveform1.SetExcluded(ex1);
                waveform2.SetExcluded(ex2);
            });
        };
    }

    // ── Drop zones ────────────────────────────────────────────────────────────

    private void Drop_DragEnter(object sender, DragEventArgs e)
        => e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;

    private void Drop1_Drop(object sender, DragEventArgs e)
    {
        if (e.Data.GetData(DataFormats.FileDrop) is string[] files && files.Length > 0)
            _vm.DropFile(files[0], _vm.Side1, 1);
    }

    private void Drop2_Drop(object sender, DragEventArgs e)
    {
        if (e.Data.GetData(DataFormats.FileDrop) is string[] files && files.Length > 0)
            _vm.DropFile(files[0], _vm.Side2, 2);
    }

    private void Drop1_Click(object sender, MouseButtonEventArgs e) => _vm.BrowseFile(_vm.Side1, 1);
    private void Drop2_Click(object sender, MouseButtonEventArgs e) => _vm.BrowseFile(_vm.Side2, 2);

    // ── Tab sync ──────────────────────────────────────────────────────────────

    private void WaveTab_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        => _vm.ActiveSideIndex = waveTab.SelectedIndex;

    // ── Misc ──────────────────────────────────────────────────────────────────

    private void MenuExit_Click(object sender, RoutedEventArgs e) => Close();

    // ── Theme ─────────────────────────────────────────────────────────────────

    private void ThemeLight_Click(object sender, RoutedEventArgs e)  => ApplyAndSave(ThemeMode.Light);
    private void ThemeDark_Click(object sender, RoutedEventArgs e)   => ApplyAndSave(ThemeMode.Dark);
    private void ThemeSystem_Click(object sender, RoutedEventArgs e) => ApplyAndSave(ThemeMode.System);

    private void ApplyAndSave(ThemeMode mode)
    {
        ThemeManager.Apply(mode);
        UpdateThemeChecks(mode);
        var s = AppSettings.Load();
        s.Theme = mode.ToString();
        s.Save();
    }

    private void UpdateThemeChecks(ThemeMode mode)
    {
        menuThemeLight.IsChecked  = mode == ThemeMode.Light;
        menuThemeDark.IsChecked   = mode == ThemeMode.Dark;
        menuThemeSystem.IsChecked = mode == ThemeMode.System;
    }

    // Needed to allow MouseDown on the WaveformControl inside the TabItem
    private void Waveform_MouseDown(object sender, MouseButtonEventArgs e) => e.Handled = false;
}
