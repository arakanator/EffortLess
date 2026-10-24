namespace EffortLess.Core.Playback;

// Application services depend on this contract, never on a XAML control or HWND.
public interface IPlaybackEngine : IDisposable
{
    PlaybackState State { get; }
    void Open(string path);
    void PlayPause();
    void Seek(double seconds);
    void SeekAbsolute(double seconds);
    void SetVolume(double volume);
    void SetSpeed(double speed);
    void SetSubtitleAppearance(SubtitleAppearance appearance);
    void SelectAudioTrack(long id);
    void SelectSubtitleTrack(long? id);
    void LoadSubtitle(string path);
    void CaptureFrame(string path);
}
