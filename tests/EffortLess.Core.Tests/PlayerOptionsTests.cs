using EffortLess.Core.Playback;
using Xunit;

namespace EffortLess.Core.Tests;

public class PlayerOptionsTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ValidateMediaPath_RejectsNullOrEmpty(string? path)
    {
        Assert.Throws<ArgumentException>(() => PlayerOptions.ValidateMediaPath(path));
    }

    [Fact]
    public void ValidateMediaPath_RejectsInvalidPathCharacters()
    {
        Assert.Throws<ArgumentException>(() => PlayerOptions.ValidateMediaPath(@"C:\media\" + '\0' + ".mkv"));
    }

    [Fact]
    public void ValidateMediaPath_AcceptsNormalPath()
    {
        PlayerOptions.ValidateMediaPath(@"C:\media\clip.mkv");
    }

    [Theory]
    [InlineData(150, 100)]
    [InlineData(-5, 0)]
    [InlineData(50, 50)]
    [InlineData(double.NaN, 0)]
    public void ClampVolume_ClampsToRange(double input, double expected)
    {
        Assert.Equal(expected, PlayerOptions.ClampVolume(input));
    }

    [Theory]
    [InlineData(10, 10)]
    [InlineData(-10, -10)]
    [InlineData(double.NaN, 0)]
    [InlineData(double.PositiveInfinity, 0)]
    public void ClampSeekSeconds_KeepsFiniteValues(double input, double expected)
    {
        Assert.Equal(expected, PlayerOptions.ClampSeekSeconds(input));
    }
}
