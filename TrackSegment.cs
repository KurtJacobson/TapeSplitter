using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace TapeSplitterWpf;

public class TrackSegment : INotifyPropertyChanged
{
    private string _name = "";
    private bool   _isExported = true;

    public int      Number  { get; set; }
    public int      Side    { get; set; } = 1;
    public TimeSpan Start   { get; set; }
    public TimeSpan End     { get; set; }
    public TimeSpan Duration => End - Start;

    public string Name
    {
        get => _name;
        set { _name = value; OnPropertyChanged(); }
    }

    public bool IsExported
    {
        get => _isExported;
        set { _isExported = value; OnPropertyChanged(); }
    }

    public string SideDisplay     => $"Side {Side}";
    public string StartDisplay    => Format(Start);
    public string EndDisplay      => Format(End);
    public string DurationDisplay => Format(Duration);

    private static string Format(TimeSpan t) =>
        $"{(int)t.TotalMinutes}:{t.Seconds:D2}.{t.Milliseconds / 100}";

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? n = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
}
