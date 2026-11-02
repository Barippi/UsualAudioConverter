using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace UsualAudioConverter;

public class AudioItem : INotifyPropertyChanged
{
    public string FileName { get; set; } = string.Empty;
    public string SourcePath { get; set; } = string.Empty;
    public string DestinationPath { get; set; } = string.Empty;

    private string _status = "待機中";
    public string Status
    {
        get => _status;
        set { _status = value; OnPropertyChanged(); }
    }

    private string _progress = "--:--";
    public string Progress
    {
        get => _progress;
        set { _progress = value; OnPropertyChanged(); }
    }

    public string? ErrorLog { get; set; }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
