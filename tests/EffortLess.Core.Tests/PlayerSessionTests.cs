using EffortLess.Core.Library;
using EffortLess.Core.Playback;
using Xunit;

namespace EffortLess.Core.Tests;

public sealed class PlayerSessionTests
{
    [Fact]
    public async Task QueueAdvancesOnceAtEofAndRejectsInvalidOpenWithoutReplacingIt()
    {
        using var files = new SessionFiles();
        FakeEngine? engine = null;
        await using var session = new PlayerSession((send, _) => engine = new FakeEngine(send), files.Directory);
        await session.InitializeAsync();
        await session.OpenAsync(new[] { files.First, files.Second });
        Assert.Equal(files.First, engine!.State.FilePath);
        engine.Publish(engine.State with { Status = PlaybackStatus.Ended, Position = 100, Duration = 100 });
        await session.SetResumeAsync(true); // FIFO barrier after the EOF notification.
        Assert.Equal(files.Second, engine.State.FilePath);
        Assert.Equal(2, engine.Opens);
        Assert.Equal(1, session.State.CurrentIndex);
        await Assert.ThrowsAsync<ArgumentException>(() => session.OpenAsync(new[] { Path.Combine(files.Directory, "bad.txt") }));
        Assert.Equal(2, session.State.Queue.Count);
        Assert.Equal(files.Second, engine.State.FilePath);
    }

    [Fact]
    public async Task ResumeUsesMetadataAndSurvivesAnImmediateRestart()
    {
        using var files = new SessionFiles();
        var info = new FileInfo(files.First);
        await new JsonLocalStore<PlayerSession.HistoryDocument>(Path.Combine(files.Directory, "history.json"))
            .SaveAsync(new() { Records = new() { new(files.First, info.Length, info.LastWriteTimeUtc.Ticks, 42, 120, DateTime.UtcNow) } });
        FakeEngine? engine = null;
        await using var session = new PlayerSession((send, _) => engine = new FakeEngine(send), files.Directory);
        await session.InitializeAsync();
        await session.OpenAsync(new[] { files.First });
        await session.SetResumeAsync(true);
        Assert.Equal(42, engine!.LastSeek);
        Assert.Equal(files.First, session.State.Recent.Single().Path);
    }

    [Fact]
    public async Task DisposalDrainsAcceptedCommandsBeforeDestroyingEngine()
    {
        using var files = new SessionFiles();
        FakeEngine? engine = null;
        var session = new PlayerSession((send, _) => engine = new FakeEngine(send), files.Directory);
        await session.InitializeAsync();
        var commands = Enumerable.Range(0, 100).Select(i => session.SetVolumeAsync(i)).ToArray();
        await session.DisposeAsync();
        await Task.WhenAll(commands);
        Assert.True(engine!.Disposed);
        Assert.Equal(0, engine.CommandsAfterDispose);
        await Assert.ThrowsAsync<ObjectDisposedException>(() => session.PlayPauseAsync());
        var saved = await new JsonLocalStore<PlayerSettings>(Path.Combine(files.Directory, "settings.json")).LoadAsync();
        Assert.Equal(99, saved.Volume);
    }

    [Fact]
    public async Task FinishedMediaDoesNotResumeAnOlderSavedPositionAfterDurationIsCleared()
    {
        using var files = new SessionFiles();
        FakeEngine? engine = null;
        await using var session = new PlayerSession((send, _) => engine = new FakeEngine(send), files.Directory);
        await session.InitializeAsync();
        await session.OpenAsync(new[] { files.First });
        engine!.Publish(engine.State with { Position = 42 });
        await session.NextAsync(); // Saves position without changing the only item.
        engine.Publish(engine.State with { Status = PlaybackStatus.Ended, Duration = 0, Position = 0 });
        await session.SetResumeAsync(true);
        var saved = await new JsonLocalStore<PlayerSession.HistoryDocument>(Path.Combine(files.Directory, "history.json")).LoadAsync();
        Assert.Equal(0, new MediaHistory(saved.Records).GetResumePosition(files.First));
    }

    [Fact]
    public async Task TrackAndSubtitleActionsRejectAFileChangedWhileItsMenuOrPickerWasOpen()
    {
        using var files = new SessionFiles();
        await using var session = new PlayerSession((send, _) => new FakeEngine(send), files.Directory);
        await session.InitializeAsync();
        await session.OpenAsync(new[] { files.First, files.Second });
        await session.NextAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(() => session.SelectAudioAsync(1, files.First));
        await Assert.ThrowsAsync<InvalidOperationException>(() => session.SelectSubtitleAsync(null, files.First));
        await Assert.ThrowsAsync<InvalidOperationException>(() => session.LoadSubtitleAsync("missing.srt", files.First));
        await session.SelectAudioAsync(1, files.Second);
    }

    [Fact]
    public async Task PreferencesPersistAndArrowSeekingIsSymmetricAndClamped()
    {
        using var files = new SessionFiles();
        FakeEngine? engine = null;
        await using (var session = new PlayerSession((send, _) => engine = new FakeEngine(send), files.Directory))
        {
            await session.InitializeAsync();
            await session.OpenAsync(new[] { files.First });
            engine!.Publish(engine.State with { Position = 60 });
            await session.SeekByConfiguredIntervalAsync(false);
            Assert.Equal(55, engine.LastSeek);
            await session.SeekByConfiguredIntervalAsync(true);
            Assert.Equal(60, engine.LastSeek);
            await session.SetPlaybackPreferencesAsync(7, 10);
            await session.SeekByConfiguredIntervalAsync(false);
            Assert.Equal(50, engine.LastSeek);
            await session.SeekByConfiguredIntervalAsync(true);
            Assert.Equal(60, engine.LastSeek);
            await session.SetPlaybackPreferencesAsync(7, 2.5);
            await session.SeekByConfiguredIntervalAsync(false);
            Assert.Equal(57.5, engine.LastSeek);
            await session.SeekByConfiguredIntervalAsync(true);
            Assert.Equal(60, engine.LastSeek);
            await session.SetPlaybackPreferencesAsync(7, 10);
            engine.Publish(engine.State with { Position = 2 });
            await session.SeekByConfiguredIntervalAsync(false);
            Assert.Equal(0, engine.LastSeek);
            engine.Publish(engine.State with { Position = 118 });
            await session.SeekByConfiguredIntervalAsync(true);
            Assert.Equal(120, engine.LastSeek);
            await session.SetPanelPreferencesAsync(true, true);
            await session.SetVolumeAsync(42);
        }
        await using var restarted = new PlayerSession((send, _) => new FakeEngine(send), files.Directory);
        await restarted.InitializeAsync();
        Assert.Equal(7, restarted.State.Settings.FullscreenControlsHideDelay);
        Assert.Equal(10, restarted.State.Settings.ArrowKeySeekInterval);
        Assert.True(restarted.State.Settings.LeftPanelCollapsed);
        Assert.True(restarted.State.Settings.RightPanelCollapsed);
        Assert.Equal(42, restarted.State.Settings.Volume);
    }

    [Fact]
    public async Task InitializingOlderSettingsBackfillsDefaultsWithoutLosingPreferences()
    {
        using var files = new SessionFiles();
        string path = Path.Combine(files.Directory, "settings.json");
        await File.WriteAllTextAsync(path, """{"Volume":42,"Speed":1.5,"ResumeEnabled":false}""");
        await using var session = new PlayerSession((send, _) => new FakeEngine(send), files.Directory);
        await session.InitializeAsync();
        var saved = await new JsonLocalStore<PlayerSettings>(path).LoadAsync();
        Assert.Equal(42, saved.Volume);
        Assert.Equal(1.5, saved.Speed);
        Assert.False(saved.ResumeEnabled);
        Assert.Equal(3, saved.FullscreenControlsHideDelay);
        Assert.Equal(5, saved.ArrowKeySeekInterval);
        Assert.Contains("FullscreenControlsHideDelay", await File.ReadAllTextAsync(path));
        Assert.Contains("ArrowKeySeekInterval", await File.ReadAllTextAsync(path));
    }

    [Fact]
    public async Task SubtitleSettings_ApplyPersistAcrossLaunchAndReset()
    {
        using var files = new SessionFiles();
        FakeEngine? engine = null;
        var expected = new SubtitleAppearance { FontFamily = "Arial", FontSize = 54, TextOpacity = .5, ShadowOffset = 2 }.Normalize();
        await using (var session = new PlayerSession((send, _) => engine = new FakeEngine(send), files.Directory))
        {
            await session.InitializeAsync();
            await session.SetSubtitleAppearanceAsync(expected); // Also safe before loading any subtitles.
            Assert.Equal(expected, engine!.LastSubtitleAppearance);
            Assert.Equal(expected, session.State.Settings.SubtitleAppearance);
        }
        await using (var session = new PlayerSession((send, _) => engine = new FakeEngine(send), files.Directory))
        {
            await session.InitializeAsync();
            Assert.Equal(expected, session.State.Settings.SubtitleAppearance);
            await session.OpenAsync(new[] { files.First });
            Assert.Equal(expected, engine!.LastSubtitleAppearance);
            await session.ResetSubtitleAppearanceAsync();
            Assert.Equal(new SubtitleAppearance(), session.State.Settings.SubtitleAppearance);
            Assert.Equal(new SubtitleAppearance(), engine.LastSubtitleAppearance);
        }
    }

    private sealed class FakeEngine(Action<PlaybackState> send) : IPlaybackEngine
    {
        public PlaybackState State { get; private set; } = PlaybackState.CreateIdle();
        public bool Disposed { get; private set; }
        public int CommandsAfterDispose { get; private set; }
        public int Opens { get; private set; }
        public double LastSeek { get; private set; }
        public SubtitleAppearance? LastSubtitleAppearance { get; private set; }
        public void Publish(PlaybackState state) { State = state; send(state); }
        private void Check() { if (Disposed) { CommandsAfterDispose++; throw new ObjectDisposedException(nameof(FakeEngine)); } }
        public void Open(string path) { Check(); Opens++; Publish(State with { FilePath = path, Position = 0, Duration = 120, Status = PlaybackStatus.Playing }); }
        public void PlayPause() { Check(); Publish(State with { IsPaused = !State.IsPaused }); }
        public void Seek(double value) => SeekAbsolute(State.Position + value);
        public void SeekAbsolute(double value) { Check(); LastSeek = value; Publish(State with { Position = value }); }
        public void SetVolume(double value) { Check(); Publish(State with { Volume = value }); }
        public void SetSpeed(double value) { Check(); Publish(State with { Speed = value }); }
        public void SetSubtitleAppearance(SubtitleAppearance appearance) { Check(); LastSubtitleAppearance = appearance; }
        public void SelectAudioTrack(long id) => Check();
        public void SelectSubtitleTrack(long? id) => Check();
        public void LoadSubtitle(string path) => Check();
        public void CaptureFrame(string path) => Check();
        public void Dispose() => Disposed = true;
    }

    private sealed class SessionFiles : IDisposable
    {
        public string Directory { get; } = Path.Combine(Path.GetTempPath(), "EffortLess.SessionTests", Guid.NewGuid().ToString("N"));
        public string First => Path.Combine(Directory, "first café.mp4");
        public string Second => Path.Combine(Directory, "second 東京.mkv");
        public SessionFiles() { System.IO.Directory.CreateDirectory(Directory); File.WriteAllText(First, "fixture"); File.WriteAllText(Second, "fixture"); }
        public void Dispose()
        {
            string root = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "EffortLess.SessionTests")) + Path.DirectorySeparatorChar;
            if (!Path.GetFullPath(Directory).StartsWith(root, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException();
            System.IO.Directory.Delete(Directory, true);
        }
    }
}
