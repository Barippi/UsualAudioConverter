using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace UsualAudioConverter;

public class AudioItem : INotifyPropertyChanged
{
    private string _status = "待機中";
    private string _progress = "0%";
    private string _errorLog = string.Empty;

    public string FileName { get; set; } = string.Empty;
    public string SourcePath { get; set; } = string.Empty;
    public string DestinationPath { get; set; } = string.Empty;

    public string Status
    {
        get => _status;
        set
        {
            if (_status != value)
            {
                _status = value;
                OnPropertyChanged();
            }
        }
    }

    public string Progress
    {
        get => _progress;
        set
        {
            if (_progress != value)
            {
                _progress = value;
                OnPropertyChanged();
            }
        }
    }

    public string ErrorLog
    {
        get => _errorLog;
        set
        {
            if (_errorLog != value)
            {
                _errorLog = value;
                OnPropertyChanged();
            }
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
