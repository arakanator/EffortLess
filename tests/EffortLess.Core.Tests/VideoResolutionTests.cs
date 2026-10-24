using Xunit;
using EffortLess.Core.Playback;

namespace EffortLess.Core.Tests;

public class VideoResolutionTests
{
    [Theory]
    [InlineData(0L, 0)]
    [InlineData(-1L, 0)]
    [InlineData(1L, 1)]
    [InlineData(1920L, 1920)]
    [InlineData(2147483647L, int.MaxValue)]
    [InlineData(2147483648L, 0)]
    [InlineData(9223372036854775807L, 0)]
    public void NormalizeDimension_Bounds(long input, int expected)
        => Assert.Equal(expected, VideoResolution.NormalizeDimension(input));

    [Theory]
    [InlineData(null, 0)]
    [InlineData("", 0)]
    [InlineData("   ", 0)]
    [InlineData("0", 0)]
    [InlineData("-1080", 0)]
    [InlineData("1920", 1920)]
    [InlineData("1080", 1080)]
    [InlineData(" 1920 ", 1920)]
    [InlineData("2147483648", 0)]
    [InlineData("abc", 0)]
    [InlineData("1920.0", 0)]
    public void ParseDimension_Normalizes(string? input, int expected)
        => Assert.Equal(expected, VideoResolution.ParseDimension(input));

    [Theory]
    [InlineData(1280, 720, "720p")]
    [InlineData(1920, 1080, "1080p")]
    [InlineData(2560, 1440, "1440p")]
    [InlineData(3840, 2160, "2160p / 4K")]
    [InlineData(7680, 4320, "4320p / 8K")]
    [InlineData(720, 1280, "")]
    [InlineData(1920, 800, "")]
    [InlineData(0, 1080, "")]
    public void FriendlyLabel_OnlyExactStandardPairs(int width, int height, string expected)
        => Assert.Equal(expected, VideoResolution.FriendlyLabel(width, height));

    [Theory]
    [InlineData(1280, 720, "1280 × 720 (720p)")]
    [InlineData(1920, 1080, "1920 × 1080 (1080p)")]
    [InlineData(1920, 800, "1920 × 800")]
    [InlineData(0, 0, "Unavailable")]
    [InlineData(0, 1080, "Unavailable")]
    [InlineData(1920, 0, "Unavailable")]
    [InlineData(-1, -1, "Unavailable")]
    public void Format_ExactDimensionsWithOptionalLabel(int width, int height, string expected)
        => Assert.Equal(expected, VideoResolution.Format(width, height));

    [Theory]
    [InlineData(1920, 1080)]
    [InlineData(0, 0)]
    [InlineData(-5, -5)]
    public void Format_AudioOnlyWins(int width, int height)
        => Assert.Equal("Not applicable (audio only)", VideoResolution.Format(width, height, audioOnly: true));
}
