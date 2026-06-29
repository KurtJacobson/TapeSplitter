using NAudio.Wave;

namespace TapeSplitterWpf;

public static class SilenceDetector
{
    /// <summary>
    /// Scans an MP3 file and returns track segments separated by silence.
    /// </summary>
    public static List<TrackSegment> Detect(
        string filePath,
        double thresholdDb,
        double minSilenceSeconds,
        IProgress<int>? progress = null,
        CancellationToken cancel = default,
        Action<int>? silenceRegionsFound = null)
    {
        float threshold = (float)Math.Pow(10.0, thresholdDb / 20.0);

        using var reader = new AudioFileReader(filePath);
        int channels   = reader.WaveFormat.Channels;
        int sampleRate = reader.WaveFormat.SampleRate;
        long totalFrames = reader.Length / (reader.WaveFormat.BitsPerSample / 8) / channels;

        // Work in 100ms blocks — measure peak per block, then find runs of quiet blocks.
        // This avoids per-sample false triggers from brief musical dips.
        int blockFrames    = sampleRate / 10;   // 100 ms
        int minQuietBlocks = (int)Math.Ceiling(minSilenceSeconds * 10); // blocks needed
        // Allow up to 2 loud blocks (200ms) inside a gap before ending it — handles
        // tape hiss bursts / plosive artefacts within an inter-track gap.
        const int maxLoudStreak = 2;

        float[] buf = new float[blockFrames * channels];
        var blockPeaks = new List<float>();
        int read;
        long framesRead = 0;

        while ((read = reader.Read(buf, 0, buf.Length)) > 0)
        {
            cancel.ThrowIfCancellationRequested();
            int frames = read / channels;
            float peak = 0f;
            for (int i = 0; i < read; i++) peak = Math.Max(peak, Math.Abs(buf[i]));
            blockPeaks.Add(peak);
            framesRead += frames;
            progress?.Report((int)(framesRead * 100 / Math.Max(1, totalFrames)));
        }

        // Find silence regions using block peaks with a short loud-streak tolerance
        var silenceRegions = new List<(int startBlock, int endBlock)>();
        bool inSil      = false;
        int  silStart   = 0;
        int  loudStreak = 0;

        for (int i = 0; i < blockPeaks.Count; i++)
        {
            bool quiet = blockPeaks[i] < threshold;
            if (!inSil)
            {
                if (quiet) { inSil = true; silStart = i; loudStreak = 0; }
            }
            else
            {
                if (quiet)
                {
                    loudStreak = 0;
                }
                else
                {
                    loudStreak++;
                    if (loudStreak > maxLoudStreak)
                    {
                        int silEnd = i - loudStreak;
                        if (silEnd - silStart >= minQuietBlocks)
                            silenceRegions.Add((silStart, silEnd));
                        inSil = false;
                    }
                }
            }
        }
        if (inSil)
        {
            int silEnd = blockPeaks.Count - loudStreak;
            if (silEnd - silStart >= minQuietBlocks)
                silenceRegions.Add((silStart, silEnd));
        }

        silenceRegionsFound?.Invoke(silenceRegions.Count);

        // Convert block indices to time
        TimeSpan BlockToTime(int b) => TimeSpan.FromSeconds(b * 0.1);

        var tracks   = new List<TrackSegment>();
        int trackStartBlock = 0;
        int num = 1;

        // Skip tape leader (silence in first 15 seconds)
        if (silenceRegions.Count > 0 && silenceRegions[0].startBlock < 150)
        {
            trackStartBlock = silenceRegions[0].endBlock;
            silenceRegions.RemoveAt(0);
        }

        int totalBlocks = blockPeaks.Count;

        foreach (var (gapStart, gapEnd) in silenceRegions)
        {
            int mid = (gapStart + gapEnd) / 2;
            if (mid - trackStartBlock >= 10) // > 1 s of audio before this gap
            {
                tracks.Add(new TrackSegment
                {
                    Number = num,
                    Name   = $"Track {num:D2}",
                    Start  = BlockToTime(trackStartBlock),
                    End    = BlockToTime(mid),
                });
                num++;
            }
            trackStartBlock = mid;
        }

        if (totalBlocks - trackStartBlock >= 10)
        {
            tracks.Add(new TrackSegment
            {
                Number = num,
                Name   = $"Track {num:D2}",
                Start  = BlockToTime(trackStartBlock),
                End    = BlockToTime(totalBlocks),
            });
        }

        return tracks;
    }

    /// <summary>
    /// Returns the actual audio codec name ("mp3", "mp2", "aac", etc.) by probing with FFmpeg.
    /// </summary>
    private static string Meta(string key, string val)
    {
        if (string.IsNullOrWhiteSpace(val)) return "";
        // Escape backslashes, quotes, and = for ffmpeg metadata values
        val = val.Replace("\\", "\\\\").Replace("'", "\\'").Replace("=", "\\=");
        return $" -metadata {key}=\"{val}\"";
    }

    private static string BuildArgs(string src, string start, string dur, string output,
        TrackSegment track, string artist, string album, string year, string genre)
    {
        string meta = Meta("title",  track.Name)
                    + Meta("track",  track.Number.ToString())
                    + Meta("artist", artist)
                    + Meta("album",  album)
                    + Meta("date",   year)
                    + Meta("genre",  genre);
        return $"-y -ss {start} -i \"{src}\" -t {dur} -c copy{meta} \"{output}\"";
    }

    public static string ProbeAudioCodec(string sourceFile, string ffmpegOverride = "")
    {
        string ffmpeg = FindFfmpeg(ffmpegOverride);
        string dir    = System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(ffmpeg)) ?? "";
        var psi = new System.Diagnostics.ProcessStartInfo
        {
            FileName  = ffmpeg, Arguments = $"-i \"{sourceFile}\"",
            WorkingDirectory       = dir,
            RedirectStandardError  = true,
            RedirectStandardOutput = true,
            UseShellExecute = false, CreateNoWindow = true,
        };
        using var p = System.Diagnostics.Process.Start(psi)!;
        string stderr = p.StandardError.ReadToEnd();
        p.WaitForExit();

        // e.g. "Stream #0:0: Audio: mp2," or "Audio: mp3 (mp3float),"
        var m = System.Text.RegularExpressions.Regex.Match(stderr, @"Audio:\s+(\w+)");
        return m.Success ? m.Groups[1].Value.ToLowerInvariant() : "mp3";
    }

    /// <summary>
    /// Exports a track segment using FFmpeg (-c copy = lossless, no re-encode).
    /// Probes the source codec and uses the correct output container automatically.
    /// </summary>
    public static void ExportTrack(
        string sourceFile,
        TrackSegment track,
        string outputPath,
        string ffmpegOverride = "",
        string artist = "", string album = "", string year = "", string genre = "",
        IProgress<int>? progress = null,
        CancellationToken cancel = default)
    {
        string ffmpeg = FindFfmpeg(ffmpegOverride);

        string start = track.Start.TotalSeconds.ToString("F3", System.Globalization.CultureInfo.InvariantCulture);
        string dur   = track.Duration.TotalSeconds.ToString("F3", System.Globalization.CultureInfo.InvariantCulture);

        string ffmpegDir = System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(ffmpeg)) ?? "";

        // Match output container to the actual encoded codec so -c copy always works
        string codec = ProbeAudioCodec(sourceFile, ffmpegOverride);
        string ext   = codec switch { "mp2" => ".mp2", "aac" => ".m4a", "flac" => ".flac", _ => ".mp3" };
        outputPath   = System.IO.Path.ChangeExtension(outputPath, ext);

        var psi = new System.Diagnostics.ProcessStartInfo
        {
            FileName         = ffmpeg,
            Arguments        = BuildArgs(sourceFile, start, dur, outputPath, track, artist, album, year, genre),
            WorkingDirectory = ffmpegDir,
            RedirectStandardError  = true,
            RedirectStandardOutput = true,
            UseShellExecute        = false,
            CreateNoWindow         = true,
        };

        using var proc = System.Diagnostics.Process.Start(psi)
            ?? throw new Exception("Failed to start FFmpeg.");

        double totalSec = track.Duration.TotalSeconds;
        var stderrLines = new System.Collections.Generic.List<string>();

        proc.ErrorDataReceived += (_, e) =>
        {
            if (e.Data is null) return;
            lock (stderrLines) stderrLines.Add(e.Data);
            if (progress is null) return;
            var m = System.Text.RegularExpressions.Regex.Match(e.Data, @"time=(\d+):(\d+):([\d.]+)");
            if (m.Success)
            {
                double elapsed = int.Parse(m.Groups[1].Value) * 3600
                               + int.Parse(m.Groups[2].Value) * 60
                               + double.Parse(m.Groups[3].Value, System.Globalization.CultureInfo.InvariantCulture);
                progress.Report((int)Math.Min(99, elapsed * 100 / totalSec));
            }
        };
        proc.BeginErrorReadLine();

        while (!proc.WaitForExit(200))
            cancel.ThrowIfCancellationRequested();

        // Flush remaining async stderr callbacks
        proc.WaitForExit();
        progress?.Report(100);

        if (proc.ExitCode != 0)
        {
            string stderr;
            lock (stderrLines) stderr = string.Join("\n", stderrLines.TakeLast(15));
            throw new Exception($"FFmpeg exited with code {proc.ExitCode}.\n\nCommand:\n{psi.FileName} {psi.Arguments}\n\nOutput:\n{stderr}");
        }
    }

    public static string FindFfmpeg(string overridePath = "")
    {
        // Check common locations and PATH
        var candidates = new List<string>();
        if (!string.IsNullOrWhiteSpace(overridePath)) candidates.Add(overridePath);
        candidates.AddRange(new[]
        {
            "ffmpeg",
            @"C:\ffmpeg\bin\ffmpeg.exe",
            @"C:\Program Files\ffmpeg\bin\ffmpeg.exe",
            @"C:\Program Files (x86)\ffmpeg\bin\ffmpeg.exe",
        });

        foreach (var candidate in candidates)
        {
            try
            {
                string dir = System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(candidate)) ?? "";
                var p = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = candidate, Arguments = "-version",
                    WorkingDirectory = dir,
                    RedirectStandardOutput = true, RedirectStandardError = true,
                    UseShellExecute = false, CreateNoWindow = true,
                });
                p?.WaitForExit(2000);
                if (p?.ExitCode == 0) return candidate;
            }
            catch { }
        }

        throw new Exception(
            "FFmpeg not found. Install it from https://ffmpeg.org/download.html and ensure ffmpeg.exe is on your PATH.");
    }
}

