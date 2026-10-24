using System.Diagnostics;
using EffortLess.Core.Native;
using EffortLess.Core.Playback;
using Xunit;

namespace EffortLess.Core.Tests;

// Opt-in tests exercise the actual engine through the UI-independent session.
public sealed class NativeSessionTests
{
    [NativeSmokeFact]
    public void NativeSubtitleDefaultsMatchPriorAppearanceAndLiveChangesReset()
    {
        using var engine = new MpvPlayer(_ => { }, _ => { });
        var defaults = new SubtitleAppearance();
        var prior = engine.ReadSubtitleAppearanceProperties();
        foreach (var property in defaults.ToMpvProperties())
        {
            string actual = prior[property.Key]!;
            if (double.TryParse(property.Value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double expected))
                Assert.Equal(expected, double.Parse(actual, System.Globalization.CultureInfo.InvariantCulture), 5);
            else Assert.Equal(property.Value, actual, ignoreCase: true);
        }
        engine.SetSubtitleAppearance(defaults with { FontSize = 56, VerticalPosition = 80, TextOpacity = .5, ShadowOffset = 2 });
        var changed = engine.ReadSubtitleAppearanceProperties();
        Assert.Equal(56, double.Parse(changed["sub-font-size"]!, System.Globalization.CultureInfo.InvariantCulture));
        Assert.Equal("force", changed["sub-ass-override"]);
        Assert.Equal("#80FFFFFF", changed["sub-color"], ignoreCase: true);
        engine.SetSubtitleAppearance(defaults);
        Assert.Equal(prior.OrderBy(p => p.Key), engine.ReadSubtitleAppearanceProperties().OrderBy(p => p.Key));
    }

    [NativeSmokeFact]
    public async Task NativeResolutionUpdatesAcross720p1080p4kAndAudioOnly()
    {
        using var directory = new TestDirectory();
        await using var session = new PlayerSession((send, log) => new MpvPlayer(send, log), directory.Path);
        await session.InitializeAsync();
        foreach (var (name, width, height, label) in new[]
        {
            ("resolution-720p.mp4", 1280, 720, "720p"),
            ("resolution-1080p.mp4", 1920, 1080, "1080p"),
            ("resolution-4k.mp4", 3840, 2160, "2160p / 4K")
        })
        {
            string path = Fixture(name);
            await session.OpenAsync(new[] { path });
            await Wait(session, s => s.Playback.FilePath == path &&
                s.Playback.Status == PlaybackStatus.Playing && s.Playback.VideoWidth == width && s.Playback.VideoHeight == height);
            await session.PlayPauseAsync();
            await Wait(session, s => s.Playback.IsPaused);
            Assert.Equal(label, VideoResolution.FriendlyLabel(session.State.Playback.VideoWidth, session.State.Playback.VideoHeight));
        }
        string audio = Fixture("audio-only.flac");
        await session.OpenAsync(new[] { audio });
        await Wait(session, s => s.Playback.FilePath == audio && s.Playback.Status == PlaybackStatus.Playing &&
            s.Playback.Tracks.Any(t => t.Kind == MediaTrackKind.Audio));
        Assert.Equal(0, session.State.Playback.VideoWidth);
        Assert.Equal(0, session.State.Playback.VideoHeight);
        Assert.Equal("Not applicable (audio only)", VideoResolution.Format(0, 0, audioOnly: true));
    }

    private static string Fixture(string name)
    {
        Assert.True(File.Exists(MpvLoader.GetExpectedLibraryPath(AppContext.BaseDirectory)), "Native library missing.");
        string? directory = Environment.GetEnvironmentVariable("EFFORTLESS_TEST_MEDIA_DIRECTORY");
        Assert.False(string.IsNullOrWhiteSpace(directory), "Set EFFORTLESS_TEST_MEDIA_DIRECTORY.");
        string path = Path.GetFullPath(Path.Combine(directory!, name));
        Assert.True(File.Exists(path), "Fixture missing: " + path);
        return path;
    }

    [NativeSmokeFact]
    public async Task NativeEofAdvancesQueueAndPreviousReturnsToFirst()
    {
        string first = Fixture("h264.mp4"), second = Fixture("vp9.webm");
        using var directory = new TestDirectory();
        await using var session = new PlayerSession((send, log) => new MpvPlayer(send, log), directory.Path);
        await session.InitializeAsync();
        await session.OpenAsync(new[] { first, second });
        await Wait(session, s => s.CurrentIndex == 0 && s.Playback.Status == PlaybackStatus.Playing && s.Playback.Duration > 0);
        await session.SetSpeedAsync(2);
        await session.SeekAsync(session.State.Playback.Duration - 0.25, true);
        await Wait(session, s => s.CurrentIndex == 1 && s.Playback.FilePath == second && s.Playback.Status == PlaybackStatus.Playing);
        await session.PlayPauseAsync();
        await Wait(session, s => s.Playback.Status == PlaybackStatus.Paused && s.Playback.Speed == 2);
        Assert.Equal(2, session.State.Queue.Count);
        Assert.Equal(second, session.State.Queue[1].Path);
        await session.PreviousAsync();
        await Wait(session, s => s.CurrentIndex == 0 && s.Playback.FilePath == first && s.Playback.Status == PlaybackStatus.Playing);
        await session.PlayPauseAsync();
        await Wait(session, s => s.Playback.Status == PlaybackStatus.Paused);
    }

    [NativeSmokeFact]
    public async Task NativeSessionRecoversFromCorruptMediaAndDrainsCommandsOnShutdown()
    {
        string corrupt = Fixture("corrupt.mp4"), valid = Fixture("h264.mp4");
        using var directory = new TestDirectory();
        MpvPlayer? engine = null;
        await using var session = new PlayerSession((send, log) => engine = new MpvPlayer(send, log), directory.Path);
        await session.InitializeAsync();
        await session.OpenAsync(new[] { corrupt });
        await Wait(session, s => s.Playback.Status == PlaybackStatus.Failed);
        await session.OpenAsync(new[] { valid });
        await Wait(session, s => s.Playback.Status == PlaybackStatus.Playing && s.Playback.Duration > 0);
        var commands = Enumerable.Range(0, 30).Select(i => i % 2 == 0
            ? session.PlayPauseAsync() : session.SeekAsync(i * 0.05, true)).ToArray();
        await Task.WhenAll(commands).WaitAsync(TimeSpan.FromSeconds(12));
        await Wait(session, s => s.Playback.Status is PlaybackStatus.Playing or PlaybackStatus.Paused && double.IsFinite(s.Playback.Position));
        if (session.State.Playback.IsPaused) await session.PlayPauseAsync();
        await Wait(session, s => s.Playback.Status == PlaybackStatus.Playing);
        await session.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(12));
        Assert.Equal(PlaybackStatus.Disposed, engine!.State.Status);
    }

    private static async Task Wait(PlayerSession session, Func<SessionState, bool> predicate)
    {
        var watch = Stopwatch.StartNew();
        while (!predicate(session.State))
        {
            Assert.True(watch.Elapsed < TimeSpan.FromSeconds(12), "Timed out: " + session.State.Playback);
            await Task.Delay(20);
        }
    }
    private sealed class TestDirectory : IDisposable
    {
        private readonly string _root = System.IO.Path.GetFullPath(System.IO.Path.Combine(System.IO.Path.GetTempPath(), "EffortLess.NativeSessionTests"));
        public string Path { get; }
        public TestDirectory()
        {
            Path = System.IO.Path.Combine(_root, Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
        }
        public void Dispose()
        {
            if (!System.IO.Path.GetFullPath(Path).StartsWith(_root + System.IO.Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Invalid test cleanup target.");
            Directory.Delete(Path, true);
        }
    }
}
