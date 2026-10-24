using System.Globalization;

namespace EffortLess.Core.Playback;

public static class VideoResolution
{
    public static int NormalizeDimension(long value)
        => value > 0 && value <= int.MaxValue ? (int)value : 0;

    public static int ParseDimension(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return 0;

        return long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out long parsed)
            ? NormalizeDimension(parsed)
            : 0;
    }

    public static string FriendlyLabel(int width, int height)
    {
        return (width, height) switch
        {
            (1280, 720) => "720p",
            (1920, 1080) => "1080p",
            (2560, 1440) => "1440p",
            (3840, 2160) => "2160p / 4K",
            (7680, 4320) => "4320p / 8K",
            _ => string.Empty
        };
    }

    public static string Format(int width, int height, bool audioOnly = false)
    {
        if (audioOnly)
            return "Not applicable (audio only)";

        if (width <= 0 || height <= 0)
            return "Unavailable";

        string label = FriendlyLabel(width, height);
        string dimensions = string.Concat(
            width.ToString(CultureInfo.InvariantCulture),
            " × ",
            height.ToString(CultureInfo.InvariantCulture));

        return label.Length == 0
            ? dimensions
            : string.Concat(dimensions, " (", label, ")");
    }
}
