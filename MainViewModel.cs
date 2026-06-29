using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Microsoft.Win32;
using NAudio.Wave;

namespace TapeSplitterWpf;

public class MainViewModel : INotifyPropertyChanged
{
    // ── Per-side state ────────────────────────────────────────────────────────
    public class SideState
    {
        public string? SourceFile;
        public double  TotalSecs;
        public List<TrackSegment> Tracks = [];
        public string  FileLabel  { get; set; } = "";
        public string  DurLabel   { get; set; } = "";
        public string  DropLabel  { get; set; } = "";
        public bool    IsLoaded   => SourceFile != null;
    }

    public SideState Side1 { get; } = new() { DropLabel = "SIDE 1  —  drop an MP3 here, or click to browse" };
    public SideState Side2 { get; } = new() { DropLabel = "SIDE 2  —  drop an MP3 here, or click to browse" };

    // ── Tracks ────────────────────────────────────────────────────────────────
    public ObservableCollection<TrackSegment> AllTracks { get; } = [];

    // Fires when excluded ranges need to be pushed to the waveform controls
    public event Action<IReadOnlyList<(double, double)>, IReadOnlyList<(double, double)>>? ExclusionsChanged;
    // Fires when markers should be pushed to the waveform controls
    public event Action<IReadOnlyList<double>, int>? MarkersRequested;

    // ── Settings ──────────────────────────────────────────────────────────────
    private int    _thresholdDb  = -25;
    private int    _minSilenceMs = 1500;
    private int    _minTrackSec  = 5;

    public int ThresholdDb  { get => _thresholdDb;  set { _thresholdDb  = value; OnPropertyChanged(); OnPropertyChanged(nameof(ThresholdLabel)); } }
    public int MinSilenceMs { get => _minSilenceMs; set { _minSilenceMs = value; OnPropertyChanged(); } }
    public int MinTrackSec  { get => _minTrackSec;  set { _minTrackSec  = value; OnPropertyChanged(); } }
    public string ThresholdLabel => $"{_thresholdDb} dB";

    // ── Album tags ────────────────────────────────────────────────────────────
    private string _artist = "", _album = "", _year = "", _genre = "";
    public string Artist { get => _artist; set { _artist = value; OnPropertyChanged(); } }
    public string Album  { get => _album;  set { _album  = value; OnPropertyChanged(); } }
    public string Year   { get => _year;   set { _year   = value; OnPropertyChanged(); } }
    public string Genre  { get => _genre;  set { _genre  = value; OnPropertyChanged(); } }

    // ── Status ────────────────────────────────────────────────────────────────
    private string _status   = "Ready";
    private double _progress = 0;
    private bool   _isBusy  = false;
    private string _trackCountLabel = "";

    public string Status          { get => _status;          set { _status          = value; OnPropertyChanged(); } }
    public double Progress        { get => _progress;        set { _progress        = value; OnPropertyChanged(); } }
    public bool   IsBusy          { get => _isBusy;          set { _isBusy          = value; OnPropertyChanged(); OnPropertyChanged(nameof(IsNotBusy)); } }
    public bool   IsNotBusy       => !_isBusy;
    public string TrackCountLabel { get => _trackCountLabel; set { _trackCountLabel = value; OnPropertyChanged(); } }

    // ── Output ────────────────────────────────────────────────────────────────
    private string _outputFolder     = "";
    private bool   _exportDone       = false;
    public  bool   ExportDone        { get => _exportDone; set { _exportDone = value; OnPropertyChanged(); } }

    // ── Active side (bound to TabControl.SelectedIndex) ───────────────────────
    private int _activeSide = 0;   // 0 = side 1, 1 = side 2
    public int ActiveSideIndex
    {
        get => _activeSide;
        set { _activeSide = value; OnPropertyChanged(); }
    }
    private SideState ActiveSide => _activeSide == 1 ? Side2 : Side1;

    // ── Commands ──────────────────────────────────────────────────────────────
    public ICommand LoadSide1Command       { get; }
    public ICommand LoadSide2Command       { get; }
    public ICommand DetectCommand          { get; }
    public ICommand ClearMarkersCommand    { get; }
    public ICommand ExportCommand          { get; }
    public ICommand OpenOutputFolderCommand{ get; }
    public ICommand ChooseOutputFolderCommand { get; }
    public ICommand OpenSettingsCommand    { get; }

    private CancellationTokenSource? _cts;
    private AppSettings _settings;

    public MainViewModel()
    {
        _settings = AppSettings.Load();
        Artist = _settings.Artist;
        Album  = _settings.Album;
        Year   = _settings.Year;
        Genre  = _settings.Genre;

        LoadSide1Command        = new RelayCommand(() => BrowseFile(Side1, 1),       () => IsNotBusy);
        LoadSide2Command        = new RelayCommand(() => BrowseFile(Side2, 2),       () => IsNotBusy);
        DetectCommand           = new RelayCommand(() => _ = DetectAsync(),          () => IsNotBusy && ActiveSide.SourceFile != null);
        ClearMarkersCommand     = new RelayCommand(ClearActiveMarkers,               () => IsNotBusy);
        ExportCommand           = new RelayCommand(() => _ = ExportAsync(),          () => IsNotBusy && AllTracks.Count > 0);
        OpenOutputFolderCommand = new RelayCommand(OpenOutputFolder,                 () => ExportDone);
        ChooseOutputFolderCommand = new RelayCommand(ChooseOutputFolder,             () => IsNotBusy);
        OpenSettingsCommand     = new RelayCommand(OpenSettingsDialog,               () => IsNotBusy);
    }

    // ── File loading ──────────────────────────────────────────────────────────

    public void BrowseFile(SideState side, int sideNum)
    {
        var dlg = new OpenFileDialog
        {
            Title  = $"Select Side {sideNum} audio file",
            Filter = "Audio Files (*.mp3;*.mp2)|*.mp3;*.mp2|All Files (*.*)|*.*",
        };
        if (dlg.ShowDialog() == true)
            _ = LoadFileAsync(dlg.FileName, side, sideNum);
    }

    public void DropFile(string path, SideState side, int sideNum) =>
        _ = LoadFileAsync(path, side, sideNum);

    private async Task LoadFileAsync(string path, SideState side, int sideNum)
    {
        side.SourceFile = path;
        side.Tracks.Clear();
        _outputFolder = System.IO.Path.GetDirectoryName(path) ?? "";

        try
        {
            using var reader = new AudioFileReader(path);
            side.TotalSecs   = reader.TotalTime.TotalSeconds;
            side.FileLabel   = System.IO.Path.GetFileName(path);
            side.DurLabel    = $"{(int)reader.TotalTime.TotalMinutes}:{reader.TotalTime.Seconds:D2}  ·  {new System.IO.FileInfo(path).Length / 1048576.0:F1} MB";
            side.DropLabel   = $"Side {sideNum}  ✓  loaded";
        }
        catch (Exception ex)
        {
            side.SourceFile = null;
            side.DropLabel  = $"SIDE {sideNum}  —  drop an MP3 here, or click to browse";
            side.FileLabel  = ""; side.DurLabel = "";
            System.Windows.MessageBox.Show(ex.Message, "Could not open file");
            return;
        }

        // Switch to this side's tab
        ActiveSideIndex = sideNum - 1;
        OnPropertyChanged(nameof(Side1));
        OnPropertyChanged(nameof(Side2));

        // Load envelope in background — the WaveformControl subscribes to WaveformDataReady
        SetBusy(true, $"Loading Side {sideNum} waveform…");
        var prog = new Progress<int>(p => { Progress = p; Status = $"Loading waveform… {p}%"; });
        float[] envelope = await Task.Run(() => BuildEnvelope(path, prog));
        WaveformDataReady?.Invoke(envelope, side.TotalSecs, sideNum);
        SetBusy(false, $"Side {sideNum} loaded.");
        RebuildAllTracks();
    }

    // The window subscribes to this to push envelope data into the WaveformControl
    public event Action<float[], double, int>? WaveformDataReady;

    private static float[] BuildEnvelope(string path, IProgress<int>? prog)
    {
        using var reader = new AudioFileReader(path);
        int ch = reader.WaveFormat.Channels, sr = reader.WaveFormat.SampleRate;
        long totalFrames = reader.Length / (reader.WaveFormat.BitsPerSample / 8) / ch;
        int framesPerCol = sr / 50;   // 50 columns per second
        int totalCols    = (int)(reader.TotalTime.TotalSeconds * 50) + 1;
        var env = new float[totalCols];
        float[] buf = new float[framesPerCol * ch * 4];
        int col = 0; float peak = 0; long pos = 0; int read;
        while ((read = reader.Read(buf, 0, buf.Length)) > 0)
        {
            int frames = read / ch;
            for (int f = 0; f < frames && col < totalCols; f++)
            {
                float p = 0;
                for (int c = 0; c < ch; c++) p = Math.Max(p, Math.Abs(buf[f * ch + c]));
                peak = Math.Max(peak, p);
                if ((pos + f + 1) % framesPerCol == 0) { env[col++] = peak; peak = 0; }
            }
            pos += frames;
            prog?.Report((int)(pos * 100 / Math.Max(1, totalFrames)));
        }
        return env;
    }

    // ── Detection ─────────────────────────────────────────────────────────────

    private async Task DetectAsync()
    {
        var side    = ActiveSide;
        int sideNum = _activeSide + 1;

        SetBusy(true, $"Scanning Side {sideNum}…");
        _cts = new CancellationTokenSource();
        int rawCount = 0;
        var prog = new Progress<int>(p => { Progress = p; Status = $"Scanning Side {sideNum}… {p}%"; });

        List<TrackSegment> tracks;
        try
        {
            tracks = await Task.Run(() =>
                SilenceDetector.Detect(side.SourceFile!, ThresholdDb, MinSilenceMs / 1000.0,
                    prog, _cts.Token, n => rawCount = n));
            tracks = tracks.Where(t => t.Duration.TotalSeconds >= MinTrackSec).ToList();
        }
        catch (OperationCanceledException) { SetBusy(false, "Cancelled."); return; }
        catch (Exception ex)
        {
            SetBusy(false, "Detection error.");
            System.Windows.MessageBox.Show(ex.Message, "Detection failed");
            return;
        }

        if (tracks.Count == 0)
        {
            string msg = rawCount == 0
                ? "No silence regions found — try raising the threshold."
                : $"{rawCount} region(s) found but filtered out — reduce Min Silence or Min Track.";
            SetBusy(false, msg);
            return;
        }

        side.Tracks = tracks;
        // Push inter-track marker positions to the waveform control
        var markers = tracks.Skip(1).Select(t => t.Start.TotalSeconds).ToList();
        MarkersRequested?.Invoke(markers, sideNum);
        RebuildAllTracks();
        SetBusy(false, $"Side {sideNum}: {tracks.Count} tracks found.");
    }

    // ── Marker changes (called from window code-behind) ───────────────────────

    public void OnMarkersChanged(IReadOnlyList<double> markerSecs, SideState side, int sideNum)
    {
        var oldNames = side.Tracks.ToDictionary(t => t.Number, t => t.Name);
        var splits   = markerSecs.OrderBy(s => s).ToList();
        var starts   = new List<double> { 0 }; starts.AddRange(splits);
        var ends     = new List<double>(splits) { side.TotalSecs };

        side.Tracks = starts.Zip(ends, (s, e) => (s, e))
            .Select((seg, i) => new TrackSegment
            {
                Number     = i + 1,
                Name       = oldNames.GetValueOrDefault(i + 1, $"Track {i + 1:D2}"),
                Start      = TimeSpan.FromSeconds(seg.s),
                End        = TimeSpan.FromSeconds(seg.e),
                Side       = sideNum,
                IsExported = true,
            }).ToList();

        RebuildAllTracks();
    }

    private void ClearActiveMarkers()
    {
        MarkersRequested?.Invoke([], _activeSide + 1);
        var side = ActiveSide;
        side.Tracks.Clear();
        RebuildAllTracks();
    }

    // ── Track list rebuild ────────────────────────────────────────────────────

    private void RebuildAllTracks()
    {
        int num = 1;
        foreach (var t in Side1.Tracks) { t.Side = 1; t.Number = num++; }
        foreach (var t in Side2.Tracks) { t.Side = 2; t.Number = num++; }

        AllTracks.Clear();
        foreach (var t in Side1.Tracks.Concat(Side2.Tracks))
        {
            t.PropertyChanged -= OnTrackPropertyChanged;
            t.PropertyChanged += OnTrackPropertyChanged;
            AllTracks.Add(t);
        }

        int total = AllTracks.Count;
        TrackCountLabel = total == 0 ? "" :
            $"{total} track{(total != 1 ? "s" : "")}   ({Side1.Tracks.Count} on Side 1, {Side2.Tracks.Count} on Side 2)";

        PushExclusions();
    }

    private void OnTrackPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(TrackSegment.IsExported))
            PushExclusions();
    }

    private void PushExclusions()
    {
        var ex1 = AllTracks.Where(t => t.Side == 1 && !t.IsExported)
                            .Select(t => (t.Start.TotalSeconds, t.End.TotalSeconds)).ToList();
        var ex2 = AllTracks.Where(t => t.Side == 2 && !t.IsExported)
                            .Select(t => (t.Start.TotalSeconds, t.End.TotalSeconds)).ToList();
        ExclusionsChanged?.Invoke(ex1, ex2);
    }

    // ── Export ────────────────────────────────────────────────────────────────

    private async Task ExportAsync()
    {
        var toExport = AllTracks.Where(t => t.IsExported).ToList();
        if (toExport.Count == 0)
        {
            System.Windows.MessageBox.Show("No tracks are checked for export.", "Nothing selected");
            return;
        }

        if (string.IsNullOrEmpty(_outputFolder))
            _outputFolder = Side1.SourceFile != null
                ? System.IO.Path.GetDirectoryName(Side1.SourceFile)!
                : Environment.GetFolderPath(Environment.SpecialFolder.Desktop);

        System.IO.Directory.CreateDirectory(_outputFolder);

        try { SilenceDetector.FindFfmpeg(_settings.FfmpegPath); }
        catch (Exception ex) { System.Windows.MessageBox.Show(ex.Message, "FFmpeg not found"); return; }

        _settings.Artist = Artist; _settings.Album = Album;
        _settings.Year   = Year;   _settings.Genre = Genre;
        _settings.Save();

        SetBusy(true, "Exporting…");
        _cts = new CancellationTokenSource();
        int done = 0;

        try
        {
            foreach (var track in toExport)
            {
                string src  = track.Side == 2 && Side2.SourceFile != null ? Side2.SourceFile : (Side1.SourceFile ?? "");
                string safe = string.Concat(track.Name.Select(c => System.IO.Path.GetInvalidFileNameChars().Contains(c) ? '_' : c));
                string dest = System.IO.Path.Combine(_outputFolder, $"{track.Number:D2} - {safe}.mp3");
                Status = $"Exporting {done + 1}/{toExport.Count}  —  {track.Name}";

                int captured = done;
                var prog = new Progress<int>(p => Progress = (captured + p / 100.0) * 100.0 / toExport.Count);
                await Task.Run(() => SilenceDetector.ExportTrack(src, track, dest,
                    _settings.FfmpegPath, Artist, Album, Year, Genre, prog, _cts.Token));
                done++;
            }
        }
        catch (OperationCanceledException) { SetBusy(false, "Cancelled."); return; }
        catch (Exception ex) { SetBusy(false, "Export error."); System.Windows.MessageBox.Show(ex.Message, "Export failed"); return; }

        ExportDone = true;
        SetBusy(false, $"Exported {done} file{(done != 1 ? "s" : "")}  →  {_outputFolder}");
        System.Windows.MessageBox.Show($"Exported {done} file{(done != 1 ? "s" : "")} to:\n{_outputFolder}", "Done");
    }

    private void OpenOutputFolder()
    {
        if (System.IO.Directory.Exists(_outputFolder))
            System.Diagnostics.Process.Start("explorer.exe", _outputFolder);
    }

    private void ChooseOutputFolder()
    {
        var dlg = new System.Windows.Forms.FolderBrowserDialog { Description = "Output folder", SelectedPath = _outputFolder };
        if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            _outputFolder = dlg.SelectedPath;
    }

    private void OpenSettingsDialog()
    {
        var win = new SettingsWindow(_settings) { Owner = System.Windows.Application.Current.MainWindow };
        if (win.ShowDialog() == true)
        {
            _settings = AppSettings.Load();
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private void SetBusy(bool busy, string status)
    {
        IsBusy   = busy;
        Status   = status;
        Progress = busy ? 0 : 0;
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? n = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
}
