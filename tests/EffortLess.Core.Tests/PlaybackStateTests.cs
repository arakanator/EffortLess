using EffortLess.Core.Playback;
using Xunit;

namespace EffortLess.Core.Tests;

public class PlaybackStateTests
{
    [Fact]
    public void CreateIdle_HasExpectedDefaults()
    {
        PlaybackState state = PlaybackState.CreateIdle();

        Assert.Equal(PlaybackStatus.Idle, state.Status);
        Assert.True(state.Idle);
        Assert.Equal(100, state.Volume);
        Assert.Equal(HwdecStatus.Idle, state.Hwdec);
        Assert.Null(state.FilePath);
        Assert.Null(state.Error);
    }

    [Fact]
    public void MapHwdec_OnlyLiteralNoIsSoftware()
    {
        Assert.Equal(HwdecStatus.Software, PlaybackState.MapHwdec("no", idle: false));
        Assert.Equal(HwdecStatus.Hardware, PlaybackState.MapHwdec("d3d11va-copy", idle: false));
        Assert.Equal(HwdecStatus.Hardware, PlaybackState.MapHwdec("nvdec", idle: false));
    }

    [Fact]
    public void MapHwdec_EmptyIsIdleWithoutDecoderOrUnknownWhenLoaded()
    {
        Assert.Equal(HwdecStatus.Idle, PlaybackState.MapHwdec(null, idle: true));
        Assert.Equal(HwdecStatus.Idle, PlaybackState.MapHwdec("", idle: true));
        Assert.Equal(HwdecStatus.Unknown, PlaybackState.MapHwdec(null, idle: false));
        Assert.Equal(HwdecStatus.Unknown, PlaybackState.MapHwdec("", idle: false));
    }
}
