namespace EffortLess.Core.Playback;

public static class PlayerOptions
{
    public const double MinVolume = 0;
    public const double MaxVolume = 100;

    public static void ValidateMediaPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("Media path must not be empty.", nameof(path));

        if (path.IndexOfAny(Path.GetInvalidPathChars()) >= 0)
            throw new ArgumentException("Media path contains invalid characters.", nameof(path));
    }

    public static double ClampVolume(double volume)
    {
        if (double.IsNaN(volume))
            return MinVolume;
        return Math.Clamp(volume, MinVolume, MaxVolume);
    }

    public static double ClampSeekSeconds(double seconds)
        => double.IsFinite(seconds) ? seconds : 0;
}
