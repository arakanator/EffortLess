using System.ComponentModel;
using System.Runtime.CompilerServices;
using Microsoft.UI.Xaml.Media;

namespace EffortLess.App;

/// <summary>Replaceable library presentation, detached from native playback.</summary>
public sealed class MediaLibraryRow(Guid? id, string path, double duration)
    : INotifyPropertyChanged
{
    public Guid? Id { get; } = id;
    public string Path { get; } = path;
    public string Title => System.IO.Path.GetFileName(Path);
    public string DurationLabel => double.IsFinite(duration) && duration > 0 && duration < TimeSpan.MaxValue.TotalSeconds - 1
        ? TimeSpan.FromSeconds(duration).ToString(duration >= 3600 ? @"h\:mm\:ss" : @"mm\:ss")
        : "Local media";
    private ImageSource? _thumbnail;
    public ImageSource? Thumbnail
    {
        get => _thumbnail;
        set { _thumbnail = value; Changed(); }
    }
    internal bool ThumbnailRequested { get; set; }
    private bool _isCurrent;
    public bool IsCurrent
    {
        get => _isCurrent;
        set { if (_isCurrent == value) return; _isCurrent = value; Changed(); }
    }
    public event PropertyChangedEventHandler? PropertyChanged;
    private void Changed([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
