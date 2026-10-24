using System;
using System.Text.Json;
using EffortLess.Core.Library;
using EffortLess.Core.Playback;
using Xunit;

namespace EffortLess.Core.Tests;

public sealed class RefinementPolicyTests
{
    private static TimeSpan At(double seconds) => TimeSpan.FromSeconds(seconds);

    private static void AssertRemaining(TimeSpan expected, TimeSpan? actual)
    {
        Assert.NotNull(actual);
        Assert.Equal(expected, actual.Value);
    }

    [Fact]
    public void PlayerSettings_OldJson_KeepsDefaultsForNewProperties()
    {
        const string json = """{"Volume":42,"Speed":1.5,"ResumeEnabled":false,"Window":{"X":10,"Y":20,"Width":800,"Height":600,"Maximized":false}}""";

        PlayerSettings? settings = JsonSerializer.Deserialize<PlayerSettings>(json);

        Assert.NotNull(settings);
        Assert.Equal(42d, settings.Volume);
        Assert.Equal(1.5d, settings.Speed);
        Assert.False(settings.ResumeEnabled);
        Assert.Equal(PlayerSettings.DefaultFullscreenControlsHideDelay, settings.FullscreenControlsHideDelay);
        Assert.Equal(PlayerSettings.DefaultArrowKeySeekInterval, settings.ArrowKeySeekInterval);
        Assert.False(settings.LeftPanelCollapsed);
        Assert.False(settings.RightPanelCollapsed);
        Assert.NotNull(settings.Window);
        Assert.Equal(10, settings.Window.X);
        Assert.Equal(20, settings.Window.Y);
        Assert.Equal(800, settings.Window.Width);
        Assert.Equal(600, settings.Window.Height);
        Assert.False(settings.Window.Maximized);
    }

    [Fact]
    public void PlayerSettings_RoundTripsNewProperties()
    {
        var original = new PlayerSettings
        {
            Volume = 17.5,
            Speed = 1.25,
            ResumeEnabled = false,
            Window = new WindowPlacement(12, 34, 1000, 700, true),
            FullscreenControlsHideDelay = 9.5,
            ArrowKeySeekInterval = 123,
            LeftPanelCollapsed = true,
            RightPanelCollapsed = false
        };

        string json = JsonSerializer.Serialize(original);
        PlayerSettings? restored = JsonSerializer.Deserialize<PlayerSettings>(json);

        Assert.NotNull(restored);
        Assert.Equal(original, restored);
        Assert.Equal(9.5d, restored.FullscreenControlsHideDelay);
        Assert.Equal(123d, restored.ArrowKeySeekInterval);
        Assert.True(restored.LeftPanelCollapsed);
        Assert.False(restored.RightPanelCollapsed);
    }

    [Fact]
    public void PlayerSettings_Normalize_ClampsNewRangesAndRetainsOtherFields()
    {
        var raw = new PlayerSettings
        {
            Volume = 150,
            Speed = double.PositiveInfinity,
            ResumeEnabled = false,
            Window = new WindowPlacement(5, 6, 900, 700, false),
            FullscreenControlsHideDelay = 999,
            ArrowKeySeekInterval = -10,
            LeftPanelCollapsed = true,
            RightPanelCollapsed = true
        };

        PlayerSettings normalized = raw.Normalize();

        Assert.Equal(100d, normalized.Volume);
        Assert.Equal(1d, normalized.Speed);
        Assert.False(normalized.ResumeEnabled);
        Assert.NotNull(normalized.Window);
        Assert.Equal(5, normalized.Window.X);
        Assert.Equal(PlayerSettings.MaximumFullscreenControlsHideDelay, normalized.FullscreenControlsHideDelay);
        Assert.Equal(PlayerSettings.MinimumArrowKeySeekInterval, normalized.ArrowKeySeekInterval);
        Assert.True(normalized.LeftPanelCollapsed);
        Assert.True(normalized.RightPanelCollapsed);
    }

    [Fact]
    public void PlayerSettings_Normalize_UsesDefaultsForNonFiniteNewValues()
    {
        var raw = new PlayerSettings
        {
            FullscreenControlsHideDelay = double.NaN,
            ArrowKeySeekInterval = double.NegativeInfinity,
            Window = new WindowPlacement(0, 0, 300, 100, false)
        };

        PlayerSettings normalized = raw.Normalize();

        Assert.Equal(PlayerSettings.DefaultFullscreenControlsHideDelay, normalized.FullscreenControlsHideDelay);
        Assert.Equal(PlayerSettings.DefaultArrowKeySeekInterval, normalized.ArrowKeySeekInterval);
        Assert.Null(normalized.Window);
    }

    [Fact]
    public void Policy_StartsVisibleAndHidesAfterInactivity()
    {
        var policy = new FullscreenControlsPolicy();

        Assert.True(policy.Visible);
        Assert.Null(policy.GetRemainingDelay(At(0)));

        policy.Update(fullscreen: true, playing: true, blocked: false, delay: 3, now: At(0));
        Assert.True(policy.Visible);
        AssertRemaining(TimeSpan.FromSeconds(3), policy.GetRemainingDelay(At(0)));

        Assert.False(policy.TryHide(At(2)));
        Assert.True(policy.Visible);
        AssertRemaining(TimeSpan.FromSeconds(1), policy.GetRemainingDelay(At(2)));

        Assert.True(policy.TryHide(At(3)));
        Assert.False(policy.Visible);
        Assert.Null(policy.GetRemainingDelay(At(4)));
    }

    [Fact]
    public void Policy_RepeatedSameStateTicks_DoNotRestartOrReveal()
    {
        var policy = new FullscreenControlsPolicy();
        policy.Update(fullscreen: true, playing: true, blocked: false, delay: 3, now: At(0));

        policy.Update(fullscreen: true, playing: true, blocked: false, delay: 3, now: At(1));
        Assert.True(policy.Visible);
        AssertRemaining(TimeSpan.FromSeconds(2), policy.GetRemainingDelay(At(1)));

        Assert.True(policy.TryHide(At(3)));
        Assert.False(policy.Visible);

        policy.Update(fullscreen: true, playing: true, blocked: false, delay: 3, now: At(4));
        Assert.False(policy.Visible);
        Assert.Null(policy.GetRemainingDelay(At(4)));
    }

    [Fact]
    public void KeyboardPlaybackChanges_DoNotRevealOrResetTimer()
    {
        var policy = new FullscreenControlsPolicy();
        policy.Update(true, true, false, 2, At(0));
        policy.Activity(At(0));
        policy.Update(true, false, false, 2, At(1));
        AssertRemaining(At(1), policy.GetRemainingDelay(At(1)));
        Assert.True(policy.TryHide(At(2))); // Paused still hides.
        policy.Update(true, true, false, 2, At(3));
        policy.Update(true, false, false, 2, At(4));
        Assert.False(policy.Visible); // Keyboard pause/resume/seek/EOF cannot reveal.
        Assert.True(policy.TryHide(At(5)));
    }

    [Fact]
    public void OpenFlyout_HoldsUntilClosed_WithoutStartingNewTimeout()
    {
        var policy = new FullscreenControlsPolicy();
        policy.Update(true, true, false, 2, At(0));
        policy.Activity(At(0));
        policy.Update(true, false, true, 2, At(1));
        Assert.False(policy.TryHide(At(10)));
        Assert.True(policy.Visible);
        policy.Update(true, false, false, 2, At(10));
        Assert.True(policy.TryHide(At(10)));
    }

    [Fact]
    public void KeyboardEnteringFullscreen_StartsHidden_ExitShowsWindowedControls()
    {
        var policy = new FullscreenControlsPolicy();
        policy.Update(false, true, false, 2, At(0));
        policy.Update(true, true, false, 2, At(1));
        Assert.False(policy.Visible);
        policy.Activity(At(2));
        Assert.True(policy.Visible);
        Assert.True(policy.TryHide(At(4)));
        policy.Update(false, false, false, 2, At(5));
        Assert.True(policy.Visible);
    }

    [Fact]
    public void DelayChanges_UseLastPointerTimeWithoutRevealing()
    {
        var policy = new FullscreenControlsPolicy();
        policy.Update(true, true, false, 3, At(0));
        Assert.True(policy.TryHide(At(3)));
        policy.Update(true, false, false, 10, At(4));
        Assert.False(policy.Visible);
        policy.Activity(At(5));
        AssertRemaining(At(10), policy.GetRemainingDelay(At(5)));
    }

    [Fact]
    public void Policy_Activity_ShowsAndResetsInactivity()
    {
        var policy = new FullscreenControlsPolicy();
        policy.Update(fullscreen: true, playing: true, blocked: false, delay: 3, now: At(0));

        Assert.True(policy.TryHide(At(3)));
        Assert.False(policy.Visible);

        policy.Activity(At(10));
        Assert.True(policy.Visible);
        AssertRemaining(TimeSpan.FromSeconds(3), policy.GetRemainingDelay(At(10)));

        Assert.False(policy.TryHide(At(12)));
        Assert.True(policy.Visible);
        Assert.True(policy.TryHide(At(13)));
        Assert.False(policy.Visible);
    }

    [Fact]
    public void Policy_RemainingDelay_IsNeverNegative()
    {
        var policy = new FullscreenControlsPolicy();
        policy.Update(fullscreen: true, playing: true, blocked: false, delay: 3, now: At(10));

        AssertRemaining(TimeSpan.Zero, policy.GetRemainingDelay(At(100)));

        Assert.True(policy.TryHide(At(100)));
        Assert.False(policy.Visible);
        Assert.Null(policy.GetRemainingDelay(At(200)));
    }
}
