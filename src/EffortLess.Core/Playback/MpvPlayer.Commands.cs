using System.Globalization;
using System.Runtime.InteropServices;
using EffortLess.Core.Native;

namespace EffortLess.Core.Playback;

public sealed partial class MpvPlayer
{
    public void Open(string path)
    {
        PlayerOptions.ValidateMediaPath(path);
        EnsureStarted();

        PlaybackState next;
        string? log = null;
        lock (_gate)
        {
            ThrowIfUnavailable();

            MpvNative.MpvError error = MpvNative.Command(_mpv, "loadfile", path, "replace");
            if (error != MpvNative.MpvError.Success)
            {
                next = _state with
                {
                    Status = PlaybackStatus.Failed,
                    FilePath = path,
                    Idle = false,
                    Error = $"loadfile failed ({(int)error})"
                };
                log = $"loadfile failed: {(int)error}";
            }
            else
            {
                MpvNative.MpvError pauseError =
                    MpvNative.mpv_set_property_string(_mpv, "pause", "no");
                if (pauseError != MpvNative.MpvError.Success)
                    log = $"set pause failed: {(int)pauseError}";

                _expectStartFile = true;
                next = _state with
                {
                    Status = PlaybackStatus.Opening,
                    FilePath = path,
                    Position = 0,
                    Duration = 0,
                    IsPaused = false,
                    Hwdec = HwdecStatus.Idle,
                    HwdecName = string.Empty,
                    Idle = false,
                    Error = null
                };
                next = next with { Tracks = Array.Empty<MediaTrack>(), VideoAspect = 0, Container = "" };
            }

            next = next with { VideoWidth = 0, VideoHeight = 0 };
            _state = next;
        }

        _send(next);
        if (log is not null)
            _log(log);
    }

    public void PlayPause()
    {
        EnsureStarted();

        PlaybackState next;
        string? log = null;
        lock (_gate)
        {
            ThrowIfUnavailable();

            bool restart = _state.Status == PlaybackStatus.Ended &&
                           !string.IsNullOrEmpty(_state.FilePath);

            if (restart)
            {
                MpvNative.MpvError error = MpvNative.Command(_mpv, "loadfile", _state.FilePath!, "replace");
                if (error != MpvNative.MpvError.Success)
                {
                    next = _state with
                    {
                        Status = PlaybackStatus.Failed,
                        Idle = false,
                        Error = $"loadfile failed ({(int)error})"
                    };
                    log = $"loadfile failed: {(int)error}";
                }
                else
                {
                    MpvNative.MpvError pauseError =
                        MpvNative.mpv_set_property_string(_mpv, "pause", "no");
                    if (pauseError != MpvNative.MpvError.Success)
                        log = $"set pause failed: {(int)pauseError}";

                    _expectStartFile = true;
                    next = _state with
                    {
                        Status = PlaybackStatus.Opening,
                        IsPaused = false,
                        Idle = false,
                        Error = null
                    };
                }
            }
            else
            {
                bool paused = ReadFlag(_mpv, "pause");
                MpvNative.MpvError error =
                    MpvNative.mpv_set_property_string(_mpv, "pause", paused ? "no" : "yes");
                if (error != MpvNative.MpvError.Success)
                {
                    next = _state;
                    log = $"set pause failed: {(int)error}";
                }
                else
                {
                    bool nextPaused = !paused;
                    PlaybackStatus status = nextPaused ? PlaybackStatus.Paused : PlaybackStatus.Playing;
                    if (_state.Status is not (PlaybackStatus.Playing or PlaybackStatus.Paused))
                        status = _state.Status;
                    next = _state with { IsPaused = nextPaused, Status = status };
                }
            }

            _state = next;
        }

        _send(next);
        if (log is not null)
            _log(log);
    }

    public void Seek(double seconds)
    {
        double value = PlayerOptions.ClampSeekSeconds(seconds);
        EnsureStarted();

        string? log = null;
        lock (_gate)
        {
            ThrowIfUnavailable();
            MpvNative.MpvError error =
                MpvNative.Command(_mpv, "seek", FormatDouble(value), "relative+exact");
            if (error != MpvNative.MpvError.Success)
                log = $"seek failed: {(int)error}";
        }

        if (log is not null)
            _log(log);
    }

    public void SeekAbsolute(double seconds)
    {
        double value = PlayerOptions.ClampSeekSeconds(seconds);
        EnsureStarted();

        string? log = null;
        lock (_gate)
        {
            ThrowIfUnavailable();
            MpvNative.MpvError error =
                MpvNative.Command(_mpv, "seek", FormatDouble(value), "absolute+exact");
            if (error != MpvNative.MpvError.Success)
                log = $"seek failed: {(int)error}";
        }

        if (log is not null)
            _log(log);
    }

    public void SetVolume(double volume)
    {
        double value = PlayerOptions.ClampVolume(volume);
        EnsureStarted();

        string? log = null;
        lock (_gate)
        {
            ThrowIfUnavailable();
            MpvNative.MpvError error =
                MpvNative.mpv_set_property_string(_mpv, "volume", FormatDouble(value));
            if (error != MpvNative.MpvError.Success)
                log = $"set volume failed: {(int)error}";
        }

        if (log is not null)
            _log(log);
    }

    public void RefreshHwdec()
    {
        EnsureStarted();

        PlaybackState next;
        lock (_gate)
        {
            ThrowIfUnavailable();
            string? hwdec = ReadString(_mpv, "hwdec-current");
            next = _state with
            {
                Hwdec = PlaybackState.MapHwdec(hwdec, _state.Idle),
                HwdecName = hwdec ?? string.Empty
            };
            _state = next;
        }

        _send(next);
    }

    public void SetSpeed(double speed)
    {
        if (!double.IsFinite(speed) || speed < 0.25 || speed > 2) throw new ArgumentOutOfRangeException(nameof(speed));
        SetFeatureProperty("speed", FormatDouble(speed));
    }

    public void SetSubtitleAppearance(SubtitleAppearance appearance)
    {
        ArgumentNullException.ThrowIfNull(appearance);
        EnsureStarted();
        lock (_gate)
        {
            ThrowIfUnavailable();
            foreach (var property in appearance.Normalize().ToMpvProperties())
            {
                var error = MpvNative.mpv_set_property_string(_mpv, property.Key, property.Value);
                if (error != MpvNative.MpvError.Success)
                    throw new InvalidOperationException($"Cannot change {property.Key} (mpv {(int)error}).");
            }
        }
    }

    internal IReadOnlyDictionary<string, string?> ReadSubtitleAppearanceProperties()
    {
        EnsureStarted();
        lock (_gate)
        {
            ThrowIfUnavailable();
            return new SubtitleAppearance().ToMpvProperties().Keys.ToDictionary(name => name, name => ReadString(_mpv, name));
        }
    }

    public void SelectAudioTrack(long id)
    {
        if (!State.Tracks.Any(t => t.Kind == MediaTrackKind.Audio && t.Id == id)) throw new ArgumentException("Audio track is no longer available.");
        SetFeatureProperty("aid", id.ToString(CultureInfo.InvariantCulture));
    }

    public void SelectSubtitleTrack(long? id)
    {
        if (id is not null && !State.Tracks.Any(t => t.Kind == MediaTrackKind.Subtitle && t.Id == id)) throw new ArgumentException("Subtitle track is no longer available.");
        SetFeatureProperty("sid", id?.ToString(CultureInfo.InvariantCulture) ?? "no");
    }

    public void LoadSubtitle(string path)
    {
        string extension = Path.GetExtension(path).ToLowerInvariant();
        if (extension is not (".srt" or ".ass" or ".ssa") || !File.Exists(path)) throw new ArgumentException("Choose an existing SRT, ASS or SSA subtitle file.");
        FeatureCommand("sub-add", Path.GetFullPath(path), "select");
    }

    public void CaptureFrame(string path)
    {
        if (!State.Tracks.Any(t => t.Kind == MediaTrackKind.Video)) throw new InvalidOperationException("No video frame is available.");
        FeatureCommand("screenshot-to-file", Path.GetFullPath(path), "video");
    }

    private void SetFeatureProperty(string name, string value)
    {
        EnsureStarted();
        lock (_gate)
        {
            ThrowIfUnavailable();
            var error = MpvNative.mpv_set_property_string(_mpv, name, value);
            if (error != MpvNative.MpvError.Success) throw new InvalidOperationException($"Cannot change {name} (mpv {(int)error}).");
        }
    }

    private void FeatureCommand(params string[] args)
    {
        EnsureStarted();
        lock (_gate)
        {
            ThrowIfUnavailable();
            var error = MpvNative.Command(_mpv, args);
            if (error != MpvNative.MpvError.Success) throw new InvalidOperationException($"{args[0]} failed (mpv {(int)error}).");
        }
    }

}
