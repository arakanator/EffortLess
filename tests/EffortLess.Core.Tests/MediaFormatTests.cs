using EffortLess.Core.Playback;
using Xunit;

namespace EffortLess.Core.Tests;

public sealed class MediaFormatTests
{
    [Theory]
    [InlineData(@"C:\videos\movie.mp4", "MP4")]
    [InlineData(@"C:\videos\film.mkv", "MKV")]
    [InlineData(@"C:\videos\clip.MOV", "MOV")]
    [InlineData(@"C:\videos\clip.webm", "WEBM")]
    [InlineData(@"C:\videos\clip.avi", "AVI")]
    [InlineData("file:///C:/videos/movie.mp4", "MP4")]
    [InlineData("https://example.com/movie.mp4", "Stream")]
    [InlineData("rtsp://example.com/feed", "Stream")]
    [InlineData(@"C:\videos\noextension", "Unknown")]
    [InlineData(null, "Unknown")]
    public void UsesFileExtensionRatherThanDecoderAliases(string? source, string expected)
        => Assert.Equal(expected, MediaFormat.ForSource(source));
}
