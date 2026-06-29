# Tape Track Splitter

A Windows desktop app for splitting digitized cassette tape recordings into individual tracks. Load the MP3 for Side 1 and Side 2, place split markers on the waveform (manually or via auto-detection), name each track, and export them as lossless-cut files with embedded ID3 tags.

---

## Features

- **Dual-side workflow** — load Side 1 and Side 2 separately; tracks are numbered sequentially across both sides
- **Interactive waveform** — click to place a split marker, drag to move it, right-click to remove
- **Auto-detection** — configurable silence threshold and minimum gap length; a 400 ms spike-tolerant algorithm handles brief tape hiss bursts inside gaps
- **Track list** — edit track names before export; uncheck rows to skip them; unchecked tracks are dimmed on the waveform
- **Lossless export via FFmpeg** — uses `-c copy` so no audio is re-encoded; automatically detects whether the source stream is MP2 or MP3 and chooses the correct container
- **ID3 tagging** — artist, album, year, genre, track number, and title written to every exported file
- **Light / Dark / System theme** — switchable under View → Theme; waveform colors follow the theme; preference is persisted

---

## Requirements

| Requirement | Version |
|---|---|
| Windows | 10 or later (64-bit) |
| .NET | 8.0 |
| FFmpeg | Any recent build ([ffmpeg.org](https://ffmpeg.org/download.html)) |

FFmpeg is only needed for export. Detection and waveform display work without it.

---

## Getting started

### Option A — build from source

```
git clone https://github.com/your-username/tape-track-splitter.git
cd tape-track-splitter
dotnet run
```

### Option B — publish a self-contained executable

```
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o publish
```

The resulting `publish\TapeSplitterWpf.exe` runs without a separate .NET installation.

---

## FFmpeg setup

1. Download a Windows build from [ffmpeg.org](https://ffmpeg.org/download.html) (the GPL shared or static build both work)
2. Either:
   - Add the `bin\` folder to your system `PATH`, **or**
   - Open **Tools → Settings** in the app and paste the full path to `ffmpeg.exe`
3. Use **Test** in the Settings dialog to confirm it is found

The app will detect FFmpeg automatically at these locations even without configuring the path:

- `ffmpeg` (on PATH)
- `C:\ffmpeg\bin\ffmpeg.exe`
- `C:\Program Files\ffmpeg\bin\ffmpeg.exe`

---

## Workflow

1. **Load files** — drag an MP3 onto the Side 1 or Side 2 drop zone, or use File → Open Side 1/2
2. **Place split markers** — click **Auto-Detect Tracks** or click directly on the waveform; drag markers to fine-tune, right-click to delete
3. **Name tracks** — edit the Track Name column in the list
4. **Fill in tags** — enter Artist, Album, Year, and Genre in the Album Tags panel
5. **Choose what to export** — uncheck any tracks you want to skip
6. **Export** — click **Export Checked Tracks**; files are written to the same folder as the source MP3 by default, or choose a different folder with **Output Folder…**

---

## Auto-detection tips

The detector works in 100 ms blocks and tolerates brief loud spikes inside a silence gap (tape hiss bursts, plosives). If it finds nothing:

| Symptom | Fix |
|---|---|
| "0 silence regions found" | Raise the **Silence Level** slider (less negative = more sensitive) |
| Regions found but filtered | Lower **Min Silence** (the gap may be shorter than expected) or lower **Min Track** |
| Too many false splits | Raise **Min Silence** so brief dips within a song are ignored |

Cassette tapes digitized at 32 kHz often have a noise floor around −36 dB. A threshold of **−25 dB** with **1 500 ms** minimum silence works well for tapes with clear gaps between tracks.

---

## Project structure

```
TapeSplitterWpf/
├── Themes/
│   ├── DarkTheme.xaml        # Dark palette — window, controls, waveform
│   └── LightTheme.xaml       # Light palette — window, controls, waveform
├── App.xaml                   # Application resources + IntegerTextBox template
├── AppSettings.cs             # JSON settings (FFmpeg path, album tags, theme)
├── IntegerTextBox.cs          # Themed numeric spinner control
├── MainViewModel.cs           # All app logic — MVVM, no UI dependencies
├── MainWindow.xaml            # Declarative layout, data-bound to MainViewModel
├── MainWindow.xaml.cs         # Thin code-behind: drag-drop, waveform events, theme menu
├── RelayCommand.cs            # ICommand implementation
├── SettingsWindow.xaml/.cs    # FFmpeg path dialog
├── SilenceDetector.cs         # Audio analysis + FFmpeg export
├── ThemeManager.cs            # Swaps ResourceDictionary, detects Windows theme
├── TrackSegment.cs            # INotifyPropertyChanged track model
└── WaveformControl.cs         # Custom FrameworkElement — renders waveform + markers
```

---

## Dependencies

| Package | Purpose |
|---|---|
| [NAudio](https://github.com/naudio/NAudio) | MP3/MP2 decoding, waveform envelope computation |
| FFmpeg (external) | Lossless segment export with ID3 tagging |

---

## License

MIT — see [LICENSE](LICENSE).
