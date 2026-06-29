using System.IO;
using System.Text.Json;

namespace TapeSplitterWpf;

public class AppSettings
{
    public string FfmpegPath { get; set; } = "";
    public string Theme      { get; set; } = "System";
    public string Artist     { get; set; } = "";
    public string Album      { get; set; } = "";
    public string Year       { get; set; } = "";
    public string Genre      { get; set; } = "";

    private static readonly string _path = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "TapeSplitter", "settings.json");

    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(_path))
                return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(_path)) ?? new();
        }
        catch { }
        return new();
    }

    public void Save()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        File.WriteAllText(_path, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
    }
}
