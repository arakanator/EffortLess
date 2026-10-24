using System;

namespace EffortLess.Core.Playback;

public enum PlaybackStatus
{
    Idle,
    Opening,
    Playing,
    Paused,
    Ended,
    Failed,
    Disposed
}

public enum HwdecStatus
{
    Idle,
    Unknown,
    Software,
    Hardware
}

public sealed record PlaybackState
{
    public PlaybackStatus Status { get; init; }
    public string? FilePath { get; init; }
    public double Position { get; init; }
    public double Duration { get; init; }
    public bool IsPaused { get; init; }
    public double Volume { get; init; }
    public double Speed { get; init; } = 1;
    public IReadOnlyList<MediaTrack> Tracks { get; init; } = Array.Empty<MediaTrack>();
    public double VideoAspect { get; init; }
    public int VideoWidth { get; init; }
    public int VideoHeight { get; init; }
    public string EngineVersion { get; init; } = "";
    public string Container { get; init; } = "";
    public HwdecStatus Hwdec { get; init; }
    public string HwdecName { get; init; } = string.Empty;
    public bool Idle { get; init; }
    public string? Error { get; init; }
    public bool EngineUnavailable { get; init; }

    public static PlaybackState CreateIdle() => new()
    {
        Status = PlaybackStatus.Idle,
        Volume = 100,
        Hwdec = HwdecStatus.Idle,
        Idle = true
    };

    public static HwdecStatus MapHwdec(string? name, bool idle)
    {
        if (string.IsNullOrEmpty(name))
            return idle ? HwdecStatus.Idle : HwdecStatus.Unknown;

        if (string.Equals(name, "no", StringComparison.OrdinalIgnoreCase))
            return HwdecStatus.Software;
        if (name is "auto" or "auto-safe" or "auto-unsafe" or "unknown")
            return HwdecStatus.Unknown;

        return HwdecStatus.Hardware;
    }
}
