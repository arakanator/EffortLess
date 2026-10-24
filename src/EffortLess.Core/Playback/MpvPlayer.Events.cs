using System.Globalization;
using System.Runtime.InteropServices;
using EffortLess.Core.Native;

namespace EffortLess.Core.Playback;

public sealed partial class MpvPlayer
{
    private void EventLoop(IntPtr mpv)
    {
        while (!_disposed)
        {
            // The dedicated event thread sleeps until a native event or our
            // disposal wakeup; periodic idle polling serves no purpose here.
            IntPtr ptr = MpvNative.mpv_wait_event(mpv, -1);
            if (_disposed)
                break;
            if (ptr == IntPtr.Zero)
                continue;

            MpvNative.mpv_event ev = Marshal.PtrToStructure<MpvNative.mpv_event>(ptr);

            if (ev.event_id == MpvNative.MpvEventId.Shutdown)
                break;

            HandleEvent(ev);
        }
    }

    private void HandleEvent(MpvNative.mpv_event ev)
    {
        switch (ev.event_id)
        {
            case MpvNative.MpvEventId.LogMessage:
                HandleLogMessage(ev);
                break;
            case MpvNative.MpvEventId.StartFile:
                HandleStartFile();
                break;
            case MpvNative.MpvEventId.FileLoaded:
                HandleFileLoaded();
                break;
            case MpvNative.MpvEventId.EndFile:
                HandleEndFile(ev);
                break;
            case MpvNative.MpvEventId.PropertyChange:
                HandlePropertyChange(ev);
                break;
        }
    }

    private void HandleStartFile()
    {
        lock (_gate)
        {
            if (_disposed)
                return;
            _expectStartFile = false;
            _state = _state with { Status = PlaybackStatus.Opening, Idle = false, VideoWidth = 0, VideoHeight = 0 };
        }
    }

    private void HandleFileLoaded()
    {
        PlaybackState next;
        lock (_gate)
        {
            if (_disposed)
                return;

            string? hwdec = ReadString(_mpv, "hwdec-current");
            bool paused = ReadFlag(_mpv, "pause");
            next = _state with
            {
                Status = paused ? PlaybackStatus.Paused : PlaybackStatus.Playing,
                IsPaused = paused,
                Idle = false,
                Error = null,
                Hwdec = PlaybackState.MapHwdec(hwdec, idle: false),
                HwdecName = hwdec ?? string.Empty
            };
            string? aspectText = ReadString(_mpv, "video-params/aspect");
            double.TryParse(aspectText, NumberStyles.Float, CultureInfo.InvariantCulture, out double aspect);
            var dimensions = ReadVideoDimensions();
            next = next with
            {
                VideoWidth = dimensions.Width,
                VideoHeight = dimensions.Height,
                Tracks = MpvTrackReader.Read(_mpv),
                VideoAspect = double.IsFinite(aspect) && aspect > 0 ? aspect : 0,
                EngineVersion = ReadString(_mpv, "mpv-version") ?? "",
                Container = ReadString(_mpv, "file-format") ?? ""
            };
            _state = next;
        }

        _send(next);
    }

    private void HandleEndFile(MpvNative.mpv_event ev)
    {
        if (ev.data == IntPtr.Zero)
            return;

        MpvNative.mpv_event_end_file endFile =
            Marshal.PtrToStructure<MpvNative.mpv_event_end_file>(ev.data);

        PlaybackState? next = null;
        string? log = null;

        lock (_gate)
        {
            if (_disposed)
                return;

            if (_expectStartFile)
                return;

            if (endFile.reason == MpvNative.MpvEndFileReason.Eof)
            {
                next = _state with { Status = PlaybackStatus.Ended, Idle = true, IsPaused = false };
            }
            else if (endFile.reason == MpvNative.MpvEndFileReason.Error)
            {
                string message = endFile.error == MpvNative.MpvError.Success
                    ? "Playback failed."
                    : $"Playback failed (mpv error {(int)endFile.error}).";
                next = _state with { Status = PlaybackStatus.Failed, Idle = true, Error = message };
                log = message;
            }
            else
            {
                next = _state with { Idle = true };
            }

            next = next with { Hwdec = HwdecStatus.Idle, HwdecName = string.Empty };

            if (next is not null)
                _state = next;
        }

        if (next is not null)
        {
            _send(next);
            if (log is not null)
                _log(log);
        }
    }

    private void HandlePropertyChange(MpvNative.mpv_event ev)
    {
        if (ev.data == IntPtr.Zero)
            return;

        MpvNative.mpv_event_property property =
            Marshal.PtrToStructure<MpvNative.mpv_event_property>(ev.data);

        string? name = ReadUtf8(property.name);
        if (string.IsNullOrEmpty(name))
            return;

        PlaybackState? next = null;

        lock (_gate)
        {
            if (_disposed)
                return;

            switch (name)
            {
                case "video-params/w":
                case "video-params/h":
                    if (_state.Status is not (PlaybackStatus.Playing or PlaybackStatus.Paused)) break;
                    if (property.format == MpvNative.MpvFormat.Int64 && property.data != IntPtr.Zero &&
                        VideoResolution.NormalizeDimension(Marshal.ReadInt64(property.data)) > 0)
                    {
                        var dimensions = ReadVideoDimensions();
                        next = _state with { VideoWidth = dimensions.Width, VideoHeight = dimensions.Height };
                    }
                    else
                        next = _state with { VideoWidth = 0, VideoHeight = 0 };
                    break;
                case "speed":
                    if (property.format == MpvNative.MpvFormat.Double && property.data != IntPtr.Zero)
                        next = _state with { Speed = Marshal.PtrToStructure<double>(property.data) };
                    break;
                case "track-list":
                    next = _state with { Tracks = property.format == MpvNative.MpvFormat.Node && property.data != IntPtr.Zero
                        ? MpvTrackReader.Parse(Marshal.PtrToStructure<MpvNative.MpvNode>(property.data)) : Array.Empty<MediaTrack>() };
                    break;
                case "aid":
                case "sid":
                    if (_state.Status is PlaybackStatus.Playing or PlaybackStatus.Paused)
                        next = _state with { Tracks = MpvTrackReader.Read(_mpv) };
                    break;
                case "pause":
                    if (property.format == MpvNative.MpvFormat.Flag && property.data != IntPtr.Zero)
                    {
                        bool paused = Marshal.ReadInt32(property.data) != 0;
                        PlaybackStatus status = paused ? PlaybackStatus.Paused : PlaybackStatus.Playing;
                        if (_state.Status is not (PlaybackStatus.Playing or PlaybackStatus.Paused))
                            status = _state.Status;
                        next = _state with { IsPaused = paused, Status = status };
                    }
                    break;
                case "time-pos":
                    if (property.format == MpvNative.MpvFormat.Double && property.data != IntPtr.Zero)
                    {
                        next = _state with { Position = Marshal.PtrToStructure<double>(property.data) };
                    }
                    else if (property.format == MpvNative.MpvFormat.None)
                    {
                        next = _state with { Position = 0 };
                    }
                    break;
                case "duration":
                    if (property.format == MpvNative.MpvFormat.Double && property.data != IntPtr.Zero)
                    {
                        next = _state with { Duration = Marshal.PtrToStructure<double>(property.data) };
                    }
                    else if (property.format == MpvNative.MpvFormat.None)
                    {
                        next = _state with { Duration = 0 };
                    }
                    break;
                case "volume":
                    if (property.format == MpvNative.MpvFormat.Double && property.data != IntPtr.Zero)
                    {
                        next = _state with { Volume = Marshal.PtrToStructure<double>(property.data) };
                    }
                    break;
                case "hwdec-current":
                    if (property.format == MpvNative.MpvFormat.String && property.data != IntPtr.Zero)
                    {
                        string? hwdec = ReadStringPointer(property.data);
                        next = _state with
                        {
                            Hwdec = PlaybackState.MapHwdec(hwdec, _state.Idle),
                            HwdecName = hwdec ?? string.Empty
                        };
                    }
                    else if (property.format == MpvNative.MpvFormat.None)
                    {
                        next = _state with
                        {
                            Hwdec = PlaybackState.MapHwdec(null, _state.Idle),
                            HwdecName = string.Empty
                        };
                    }
                    break;
            }

            if (next is not null)
                _state = next;
        }

        if (next is not null)
            _send(next);
    }

    private (int Width, int Height) ReadVideoDimensions() =>
        (VideoResolution.ParseDimension(ReadString(_mpv, "video-params/w")),
         VideoResolution.ParseDimension(ReadString(_mpv, "video-params/h")));

    private void HandleLogMessage(MpvNative.mpv_event ev)
    {
        if (ev.data == IntPtr.Zero)
            return;

        MpvNative.mpv_event_log_message message =
            Marshal.PtrToStructure<MpvNative.mpv_event_log_message>(ev.data);

        string? text = ReadUtf8(message.text);
        if (!string.IsNullOrWhiteSpace(text))
            _log(text);
    }

}
