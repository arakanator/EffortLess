namespace EffortLess.Core.Library;

public sealed record WindowPlacement(int X, int Y, int Width, int Height, bool Maximized)
{
    public const int MinimumWidth = 420;
    public const int MinimumHeight = 260;
    public const int MaximumWidth = 16384;
    public const int MaximumHeight = 16384;
    public const int MinimumCoordinate = -65536;
    public const int MaximumCoordinate = 65536;

    [System.Text.Json.Serialization.JsonIgnore]
    public bool IsReasonable =>
        X >= MinimumCoordinate && X <= MaximumCoordinate &&
        Y >= MinimumCoordinate && Y <= MaximumCoordinate &&
        Width >= MinimumWidth && Width <= MaximumWidth &&
        Height >= MinimumHeight && Height <= MaximumHeight;

    public static WindowPlacement? Sanitize(WindowPlacement? placement) =>
        placement is not null && placement.IsReasonable ? placement : null;
}

public sealed record PlayerSettings
{
    public const double MinimumVolume = 0;
    public const double MaximumVolume = 100;

    public const double DefaultFullscreenControlsHideDelay = 3;
    public const double MinimumFullscreenControlsHideDelay = 1;
    public const double MaximumFullscreenControlsHideDelay = 30;

    public const double DefaultArrowKeySeekInterval = 5;
    public const double MinimumArrowKeySeekInterval = 1;
    public const double MaximumArrowKeySeekInterval = 300;

    public double Volume { get; init; } = 100;
    public double Speed { get; init; } = 1;
    public bool ResumeEnabled { get; init; } = true;
    public WindowPlacement? Window { get; init; }
    public double FullscreenControlsHideDelay { get; init; } = DefaultFullscreenControlsHideDelay;
    public double ArrowKeySeekInterval { get; init; } = DefaultArrowKeySeekInterval;
    public bool LeftPanelCollapsed { get; init; }
    public bool RightPanelCollapsed { get; init; }
    public EffortLess.Core.Playback.SubtitleAppearance SubtitleAppearance { get; init; } = new();

    public PlayerSettings Normalize() => new()
    {
        Volume = NormalizeVolume(Volume),
        Speed = NormalizeSpeed(Speed),
        ResumeEnabled = ResumeEnabled,
        Window = WindowPlacement.Sanitize(Window),
        FullscreenControlsHideDelay = NormalizeFullscreenControlsHideDelay(FullscreenControlsHideDelay),
        ArrowKeySeekInterval = NormalizeArrowKeySeekInterval(ArrowKeySeekInterval),
        LeftPanelCollapsed = LeftPanelCollapsed,
        RightPanelCollapsed = RightPanelCollapsed,
        SubtitleAppearance = (SubtitleAppearance ?? new()).Normalize()
    };

    public static double NormalizeVolume(double volume) =>
        double.IsFinite(volume) ? Math.Clamp(volume, MinimumVolume, MaximumVolume) : 100;

    public static double NormalizeSpeed(double speed)
    {
        if (!double.IsFinite(speed))
            return 1;

        foreach (double supported in MediaFiles.Speeds)
        {
            if (speed == supported)
                return supported;
        }

        return 1;
    }

    public static double NormalizeFullscreenControlsHideDelay(double delay) =>
        double.IsFinite(delay)
            ? Math.Clamp(delay, MinimumFullscreenControlsHideDelay, MaximumFullscreenControlsHideDelay)
            : DefaultFullscreenControlsHideDelay;

    public static double NormalizeArrowKeySeekInterval(double interval) =>
        double.IsFinite(interval)
            ? Math.Clamp(interval, MinimumArrowKeySeekInterval, MaximumArrowKeySeekInterval)
            : DefaultArrowKeySeekInterval;
}
